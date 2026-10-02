package com.filemonitoring.agent

import android.app.Application
import android.util.Log
import com.filemonitoring.agent.security.SecureStorage
import com.filemonitoring.agent.service.AgentForegroundService
import com.filemonitoring.agent.sync.SyncWorker

class FileMonitoringApp : Application() {

    override fun onCreate() {
        super.onCreate()
        Log.i("FileMonitoringApp", "Application initialized")

        val storage = SecureStorage(this)
        if (storage.isRegistered && storage.isServiceEnabled) {
            Log.i("FileMonitoringApp", "Auto-starting AgentForegroundService...")
            AgentForegroundService.start(this)
        }

        SyncWorker.schedule(this)
    }
}
