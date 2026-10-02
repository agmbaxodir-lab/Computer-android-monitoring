package com.filemonitoring.agent.service

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import com.filemonitoring.agent.security.SecureStorage

/**
 * Qurilma qayta ishga tushganda yoki yangilanganda agentni avtomatik yoqish.
 */
class BootCompletedReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED ||
            intent.action == Intent.ACTION_MY_PACKAGE_REPLACED) {
            val storage = SecureStorage(context)
            if (storage.isRegistered && storage.isServiceEnabled) {
                Log.i("BootReceiver", "Device rebooted, restarting AgentForegroundService...")
                AgentForegroundService.start(context)
            }
        }
    }
}
