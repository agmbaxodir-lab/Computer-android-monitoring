package com.filemonitoring.agent.api

import android.os.Build
import android.util.Log
import com.filemonitoring.agent.security.SecureStorage
import com.google.gson.Gson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.logging.HttpLoggingInterceptor
import java.util.concurrent.TimeUnit

/**
 * Backend API bilan xavfsiz HTTPS muloqotini ta'minlovchi mijoz.
 */
class ApiClient(private val storage: SecureStorage) {

    private val gson = Gson()
    private val jsonMediaType = "application/json; charset=utf-8".toMediaType()

    private val client: OkHttpClient by lazy {
        val logging = HttpLoggingInterceptor().apply {
            level = HttpLoggingInterceptor.Level.BASIC
        }

        OkHttpClient.Builder()
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .writeTimeout(30, TimeUnit.SECONDS)
            .addInterceptor(logging)
            .addInterceptor(DeviceAuthInterceptor(storage))
            .build()
    }

    private fun getBaseUrl(): String {
        return storage.serverUrl.trimEnd('/')
    }

    /**
     * Qurilmani bir martalik enrollment token orqali serverda ro'yxatdan o'tkazish.
     */
    suspend fun register(
        enrollmentToken: String,
        hostname: String = "${Build.MANUFACTURER}-${Build.MODEL}",
        username: String? = null,
        osVersion: String = "Android ${Build.VERSION.RELEASE} (API ${Build.VERSION.SDK_INT})",
        agentVersion: String = "1.0.0",
        deviceModel: String = "${Build.MANUFACTURER} ${Build.MODEL}"
    ): Boolean = withContext(Dispatchers.IO) {
        try {
            val reqBody = RegisterRequest(
                enrollmentToken = enrollmentToken,
                hostname = hostname,
                username = username,
                osVersion = osVersion,
                agentVersion = agentVersion,
                platform = "Android",
                deviceModel = deviceModel
            )
            val json = gson.toJson(reqBody)
            val request = Request.Builder()
                .url("${getBaseUrl()}/api/v1/agent/register")
                .post(json.toRequestBody(jsonMediaType))
                .build()

            client.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    Log.w(TAG, "Register failed with HTTP ${response.code}")
                    return@withContext false
                }
                val bodyStr = response.body?.string() ?: return@withContext false
                val resp = gson.fromJson(bodyStr, RegisterResponse::class.java)
                storage.deviceId = resp.deviceId
                storage.deviceSecret = resp.deviceSecret
                storage.enrollmentToken = enrollmentToken
                Log.i(TAG, "Successfully registered as Android device: ${resp.deviceId}")
                return@withContext true
            }
        } catch (e: Exception) {
            Log.e(TAG, "Register error: ${e.message}", e)
            false
        }
    }

    /**
     * Serverga heartbeat yuborish va pending notificationlarni olish.
     */
    suspend fun sendHeartbeat(
        agentVersion: String = "1.0.0",
        username: String? = null,
        cpuPercent: Float? = null,
        memoryMb: Float? = null,
        queueSize: Int = 0
    ): HeartbeatResponse? = withContext(Dispatchers.IO) {
        if (!storage.isRegistered) return@withContext null
        try {
            val reqBody = HeartbeatRequest(
                agentVersion = agentVersion,
                username = username,
                cpuPercent = cpuPercent,
                memoryMb = memoryMb,
                queueSize = queueSize
            )
            val json = gson.toJson(reqBody)
            val request = Request.Builder()
                .url("${getBaseUrl()}/api/v1/agent/heartbeat")
                .post(json.toRequestBody(jsonMediaType))
                .build()

            client.newCall(request).execute().use { response ->
                if (response.isSuccessful) {
                    val bodyStr = response.body?.string() ?: return@withContext null
                    val res = gson.fromJson(bodyStr, HeartbeatResponse::class.java)
                    storage.lastHeartbeatTime = System.currentTimeMillis()
                    return@withContext res
                }
                Log.w(TAG, "Heartbeat failed with HTTP ${response.code}")
                null
            }
        } catch (e: Exception) {
            Log.w(TAG, "Heartbeat network error: ${e.message}")
            null
        }
    }

    /**
     * Hodisalar to'plamini (batch, max 500) yuborish.
     */
    suspend fun sendEvents(batch: List<EventDto>): SendResult = withContext(Dispatchers.IO) {
        if (!storage.isRegistered || batch.isEmpty()) return@withContext SendResult.REJECT
        try {
            val json = gson.toJson(batch)
            val request = Request.Builder()
                .url("${getBaseUrl()}/api/v1/agent/events")
                .post(json.toRequestBody(jsonMediaType))
                .build()

            client.newCall(request).execute().use { response ->
                when {
                    response.isSuccessful -> {
                        Log.d(TAG, "Successfully sent ${batch.size} events")
                        SendResult.OK
                    }
                    response.code == 400 -> {
                        Log.e(TAG, "Server rejected events payload (HTTP 400)")
                        SendResult.REJECT
                    }
                    else -> {
                        Log.w(TAG, "Send events error HTTP ${response.code}, retrying later")
                        SendResult.RETRY
                    }
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Send events network error: ${e.message}")
            SendResult.RETRY
        }
    }

    /**
     * Kuzatiladigan ilovalar konfiguratsiyasini olish.
     */
    suspend fun getConfig(): ConfigResponse? = withContext(Dispatchers.IO) {
        if (!storage.isRegistered) return@withContext null
        try {
            val request = Request.Builder()
                .url("${getBaseUrl()}/api/v1/agent/config")
                .get()
                .build()

            client.newCall(request).execute().use { response ->
                if (response.isSuccessful) {
                    val bodyStr = response.body?.string() ?: return@withContext null
                    return@withContext gson.fromJson(bodyStr, ConfigResponse::class.java)
                }
                null
            }
        } catch (e: Exception) {
            null
        }
    }

    companion object {
        private const val TAG = "ApiClient"
    }
}
