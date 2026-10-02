package com.filemonitoring.agent.sync

import android.app.ActivityManager
import android.content.Context
import android.os.Debug
import android.os.Process
import android.util.Log
import com.filemonitoring.agent.api.ApiClient
import com.filemonitoring.agent.api.SendResult
import com.filemonitoring.agent.db.AppDatabase
import com.filemonitoring.agent.security.SecureStorage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlin.math.min
import kotlin.math.pow
import kotlin.random.Random

/**
 * Agent sinxronizatsiya menejeri:
 * 1. Mahalliy navbatdagi hodisalarni batch (100 tadan) qilib serverga yuborish.
 * 2. Davriy heartbeat (CPU, RAM, navbat o'lchami) yuborish.
 * 3. Tarmoq uzilganda eksponensial backoff + jitter bilan qayta urinish.
 */
class SyncManager(private val context: Context) {

    private val storage = SecureStorage(context)
    private val db = AppDatabase.getInstance(context)
    private val api = ApiClient(storage)

    private val scope = CoroutineScope(Dispatchers.IO + Job())
    private var syncJob: Job? = null
    private var consecutiveFailures = 0

    fun startSync() {
        stopSync()
        syncJob = scope.launch {
            var lastHeartbeat = 0L
            val heartbeatInterval = 60_000L // 60 soniya

            while (isActive) {
                val now = System.currentTimeMillis()

                // Heartbeat yuborish
                if (now - lastHeartbeat >= heartbeatInterval) {
                    performHeartbeat()
                    lastHeartbeat = now
                }

                // Navbatdagi hodisalarni yuborish
                val sentCount = flushQueue()

                // Agar xatolik bo'lsa backoff, aks holda 5 soniya kutish
                val waitTime = if (consecutiveFailures > 0) {
                    calculateBackoffMs()
                } else if (sentCount > 0) {
                    1_000L // Yana hodisalar bo'lsa tezroq davom etish
                } else {
                    5_000L
                }

                delay(waitTime)
            }
        }
        Log.i(TAG, "Sync loop started")
    }

    fun stopSync() {
        syncJob?.cancel()
        syncJob = null
        consecutiveFailures = 0
        Log.i(TAG, "Sync loop stopped")
    }

    /**
     * Navbatdan 100 tagacha hodisalarni olib serverga uzatadi.
     */
    suspend fun flushQueue(): Int {
        if (!storage.isRegistered) return 0

        val pendingEntities = db.eventDao().getPendingEvents(100)
        if (pendingEntities.isEmpty()) {
            consecutiveFailures = 0
            return 0
        }

        val dtoList = pendingEntities.map { it.toDto() }
        val ids = pendingEntities.map { it.eventId }

        val result = api.sendEvents(dtoList)
        return when (result) {
            SendResult.OK -> {
                db.eventDao().deleteEvents(ids)
                consecutiveFailures = 0
                Log.d(TAG, "Successfully flushed ${ids.size} events to backend")
                ids.size
            }
            SendResult.REJECT -> {
                // 400 Bad Request - bu zaharli batch, navbatni to'sib qo'ymasligi uchun o'chiriladi
                db.eventDao().deleteEvents(ids)
                consecutiveFailures = 0
                Log.w(TAG, "Batch was rejected by server (400), dropped to unblock queue")
                ids.size
            }
            SendResult.RETRY -> {
                consecutiveFailures++
                db.eventDao().incrementRetryCount(ids)
                Log.w(TAG, "Flushing failed, will retry (consecutive failures: $consecutiveFailures)")
                0
            }
        }
    }

    private suspend fun performHeartbeat() {
        if (!storage.isRegistered) return
        try {
            val qCount = db.eventDao().getCount()
            val memInfo = Debug.MemoryInfo()
            Debug.getMemoryInfo(memInfo)
            val memoryMb = memInfo.totalPss / 1024.0f

            val resp = api.sendHeartbeat(
                agentVersion = "1.0.0",
                username = null,
                cpuPercent = null,
                memoryMb = memoryMb,
                queueSize = qCount
            )

            if (resp != null) {
                Log.d(TAG, "Heartbeat success. Pending notifications: ${resp.pendingNotifications.size}")
            }
        } catch (e: Exception) {
            Log.w(TAG, "Heartbeat error: ${e.message}")
        }
    }

    private fun calculateBackoffMs(): Long {
        val exponent = min(consecutiveFailures, 6)
        val baseSec = 2.0.pow(exponent).toLong()
        val jitter = Random.nextDouble(0.8, 1.2)
        return ((baseSec * jitter) * 1000).toLong().coerceIn(2_000L, 60_000L)
    }

    companion object {
        private const val TAG = "SyncManager"
    }
}
