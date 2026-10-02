package com.filemonitoring.agent.service

import android.app.admin.DeviceAdminReceiver
import android.content.Context
import android.content.Intent
import android.util.Log
import android.widget.Toast

/**
 * Android Enterprise / Device Owner profil boshqaruvi.
 * Korporativ boshqariladigan qurilmalarda xavfsizlik va monitoring huquqlarini ta'minlaydi.
 */
class DeviceAdminReceiver : DeviceAdminReceiver() {
    override fun onEnabled(context: Context, intent: Intent) {
        super.onEnabled(context, intent)
        Log.i("DeviceAdminReceiver", "Device Admin enabled")
        Toast.makeText(context, "Monitoring Device Admin yoqildi", Toast.LENGTH_SHORT).show()
    }

    override fun onDisabled(context: Context, intent: Intent) {
        super.onDisabled(context, intent)
        Log.w("DeviceAdminReceiver", "Device Admin disabled")
        Toast.makeText(context, "Monitoring Device Admin o'chirildi", Toast.LENGTH_SHORT).show()
    }

    override fun onProfileProvisioningComplete(context: Context, intent: Intent) {
        super.onProfileProvisioningComplete(context, intent)
        Log.i("DeviceAdminReceiver", "Profile provisioning complete")
    }
}
