package com.filemonitoring.agent.monitor

import android.app.usage.UsageEvents
import android.app.usage.UsageStatsManager
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build

/**
 * Android tizimida hozirgi yoki eng so'nggi foreground bo'lgan ilovani aniqlaydi.
 * Bu fayl hodisasi qaysi ilova (Telegram, WhatsApp, Browser va h.k.) tomonidan bajarilganini aniqlashga xizmat qiladi.
 */
class AppUsageDetector(private val context: Context) {

    private val usageStatsManager =
        context.getSystemService(Context.USAGE_STATS_SERVICE) as? UsageStatsManager
    private val packageManager: PackageManager = context.packageManager

    // Taniqli korporativ va monitoring ilovalari xaritasi
    private val knownApps = mapOf(
        "org.telegram.messenger" to "Telegram (Android)",
        "org.telegram.messenger.web" to "Telegram (Android)",
        "com.whatsapp" to "WhatsApp (Android)",
        "com.whatsapp.w4b" to "WhatsApp (Android)",
        "com.imo.android.imoim" to "imo (Android)",
        "com.microsoft.teams" to "Microsoft Teams (Android)",
        "com.discord" to "Discord (Android)",
        "com.android.chrome" to "Google Chrome",
        "org.mozilla.firefox" to "Firefox",
        "com.google.android.gm" to "Gmail",
        "com.google.android.apps.docs" to "Google Drive"
    )

    fun getForegroundApp(): AppInfo? {
        val usm = usageStatsManager ?: return null
        val now = System.currentTimeMillis()
        // So'nggi 15 soniyadagi hodisalar
        val events = usm.queryEvents(now - 15_000, now)
        val event = UsageEvents.Event()
        var lastForegroundPackage: String? = null
        var lastEventTime = 0L

        while (events.hasNextEvent()) {
            events.getNextEvent(event)
            if (event.eventType == UsageEvents.Event.ACTIVITY_RESUMED ||
                event.eventType == UsageEvents.Event.MOVE_TO_FOREGROUND) {
                if (event.timeStamp > lastEventTime) {
                    lastForegroundPackage = event.packageName
                    lastEventTime = event.timeStamp
                }
            }
        }

        val pkg = lastForegroundPackage ?: return null
        val appName = knownApps[pkg] ?: getAppNameFromPackage(pkg) ?: pkg
        return AppInfo(packageName = pkg, appName = appName)
    }

    /**
     * Fayl yo'li orqali qaysi ilovaga tegishli ekanligini taxmin qilish (fallback).
     */
    fun detectAppFromPath(filePath: String): AppInfo? {
        val lower = filePath.lowercase()
        return when {
            lower.contains("telegram") -> AppInfo("org.telegram.messenger", "Telegram (Android)")
            lower.contains("whatsapp") -> AppInfo("com.whatsapp", "WhatsApp (Android)")
            lower.contains("discord") -> AppInfo("com.discord", "Discord (Android)")
            lower.contains("imo") -> AppInfo("com.imo.android.imoim", "imo (Android)")
            lower.contains("teams") -> AppInfo("com.microsoft.teams", "Microsoft Teams (Android)")
            else -> null
        }
    }

    private fun getAppNameFromPackage(packageName: String): String? {
        return try {
            val appInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                packageManager.getApplicationInfo(packageName, PackageManager.ApplicationInfoFlags.of(0))
            } else {
                @Suppress("DEPRECATION")
                packageManager.getApplicationInfo(packageName, 0)
            }
            packageManager.getApplicationLabel(appInfo).toString()
        } catch (e: Exception) {
            null
        }
    }

    data class AppInfo(val packageName: String, val appName: String)
}
