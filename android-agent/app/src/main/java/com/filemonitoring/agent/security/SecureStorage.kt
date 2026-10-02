package com.filemonitoring.agent.security

import android.content.Context
import android.content.SharedPreferences

/**
 * Qurilma identifikatori, server siri va sozlamalarini Android KeyStore orqali
 * shifrlangan holda xavfsiz saqlovchi ombor.
 */
class SecureStorage(context: Context) {
    private val prefs: SharedPreferences =
        context.applicationContext.getSharedPreferences("filemon_secure_prefs", Context.MODE_PRIVATE)

    companion object {
        private const val KEY_SERVER_URL = "server_url"
        private const val KEY_DEVICE_ID = "device_id"
        private const val KEY_DEVICE_SECRET = "device_secret"
        private const val KEY_ENROLLMENT_TOKEN = "enrollment_token"
        private const val KEY_LAST_HEARTBEAT = "last_heartbeat"
        private const val KEY_SERVICE_ENABLED = "service_enabled"
        private const val DEFAULT_SERVER_URL = "http://10.0.2.2:8080"
    }

    var serverUrl: String
        get() = prefs.getString(KEY_SERVER_URL, DEFAULT_SERVER_URL) ?: DEFAULT_SERVER_URL
        set(value) = prefs.edit().putString(KEY_SERVER_URL, value.trimEnd('/')).apply()

    var deviceId: String?
        get() {
            val enc = prefs.getString(KEY_DEVICE_ID, null) ?: return null
            return try { KeyStoreManager.decrypt(enc) } catch (e: Exception) { null }
        }
        set(value) {
            val enc = if (value != null) KeyStoreManager.encrypt(value) else null
            prefs.edit().putString(KEY_DEVICE_ID, enc).apply()
        }

    var deviceSecret: String?
        get() {
            val enc = prefs.getString(KEY_DEVICE_SECRET, null) ?: return null
            return try { KeyStoreManager.decrypt(enc) } catch (e: Exception) { null }
        }
        set(value) {
            val enc = if (value != null) KeyStoreManager.encrypt(value) else null
            prefs.edit().putString(KEY_DEVICE_SECRET, enc).apply()
        }

    var enrollmentToken: String?
        get() {
            val enc = prefs.getString(KEY_ENROLLMENT_TOKEN, null) ?: return null
            return try { KeyStoreManager.decrypt(enc) } catch (e: Exception) { null }
        }
        set(value) {
            val enc = if (value != null) KeyStoreManager.encrypt(value) else null
            prefs.edit().putString(KEY_ENROLLMENT_TOKEN, enc).apply()
        }

    var lastHeartbeatTime: Long
        get() = prefs.getLong(KEY_LAST_HEARTBEAT, 0L)
        set(value) = prefs.edit().putLong(KEY_LAST_HEARTBEAT, value).apply()

    var isServiceEnabled: Boolean
        get() = prefs.getBoolean(KEY_SERVICE_ENABLED, false)
        set(value) = prefs.edit().putBoolean(KEY_SERVICE_ENABLED, value).apply()

    val isRegistered: Boolean
        get() = !deviceId.isNullOrEmpty() && !deviceSecret.isNullOrEmpty()

    fun clearCredentials() {
        prefs.edit()
            .remove(KEY_DEVICE_ID)
            .remove(KEY_DEVICE_SECRET)
            .remove(KEY_ENROLLMENT_TOKEN)
            .apply()
    }
}
