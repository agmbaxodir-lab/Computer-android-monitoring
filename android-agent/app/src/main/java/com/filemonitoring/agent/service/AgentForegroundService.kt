package com.filemonitoring.agent.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import com.filemonitoring.agent.R
import com.filemonitoring.agent.monitor.FileMonitoringManager
import com.filemonitoring.agent.security.SecureStorage
import com.filemonitoring.agent.sync.SyncManager
import com.filemonitoring.agent.sync.SyncWorker
import com.filemonitoring.agent.ui.MainActivity

/**
 * Doimiy ishlab turuvchi monitoring va sinxronizatsiya servisi (Foreground Service).
 */
class AgentForegroundService : Service() {

    private lateinit var storage: SecureStorage
    private lateinit var monitorManager: FileMonitoringManager
    private lateinit var syncManager: SyncManager

    override fun onCreate() {
        super.onCreate()
        storage = SecureStorage(this)
        monitorManager = FileMonitoringManager(this)
        syncManager = SyncManager(this)

        createNotificationChannel()
        startForeground(NOTIFICATION_ID, buildNotification())

        Log.i(TAG, "AgentForegroundService created")
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_STOP -> {
                stopMonitoring()
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
                return START_NOT_STICKY
            }
            else -> {
                startMonitoring()
            }
        }
        return START_STICKY
    }

    private fun startMonitoring() {
        storage.isServiceEnabled = true
        monitorManager.startMonitoring()
        syncManager.startSync()
        SyncWorker.schedule(applicationContext)
        Log.i(TAG, "Monitoring and Sync started in foreground service")
    }

    private fun stopMonitoring() {
        storage.isServiceEnabled = false
        monitorManager.stopMonitoring()
        syncManager.stopSync()
        Log.i(TAG, "Monitoring and Sync stopped")
    }

    override fun onDestroy() {
        stopMonitoring()
        super.onDestroy()
        Log.i(TAG, "AgentForegroundService destroyed")
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.notification_channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "File event monitoring status indicator"
            }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(): Notification {
        val pendingIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_IMMUTABLE
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.notification_title))
            .setContentText(getString(R.string.notification_content))
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentIntent(pendingIntent)
            .setOngoing(true)
            .build()
    }

    companion object {
        private const val TAG = "AgentService"
        private const val CHANNEL_ID = "filemon_agent_channel"
        private const val NOTIFICATION_ID = 1001

        const val ACTION_START = "com.filemonitoring.agent.action.START"
        const val ACTION_STOP = "com.filemonitoring.agent.action.STOP"

        fun start(context: Context) {
            val intent = Intent(context, AgentForegroundService::class.java).apply {
                action = ACTION_START
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            val intent = Intent(context, AgentForegroundService::class.java).apply {
                action = ACTION_STOP
            }
            context.startService(intent)
        }
    }
}
