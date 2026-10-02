package com.filemonitoring.agent.monitor

import android.content.Context
import android.database.ContentObserver
import android.net.Uri
import android.os.Handler
import android.os.Looper
import android.provider.MediaStore
import android.util.Log

/**
 * Android MediaStore ContentProvider orqali yuklab olingan yoki saqlangan
 * fayllarni kuzatuvchi ContentObserver (DOWNLOADED / CREATED hodisalari).
 */
class MediaStoreFileObserver(
    private val context: Context,
    private val onMediaEvent: (eventType: String, uri: Uri, path: String?, name: String, size: Long, mime: String?) -> Unit
) : ContentObserver(Handler(Looper.getMainLooper())) {

    private val contentResolver = context.contentResolver

    fun register() {
        try {
            contentResolver.registerContentObserver(
                MediaStore.Files.getContentUri("external"),
                true,
                this
            )
            contentResolver.registerContentObserver(
                MediaStore.Downloads.EXTERNAL_CONTENT_URI,
                true,
                this
            )
            Log.i(TAG, "MediaStore observer registered")
        } catch (e: Exception) {
            Log.w(TAG, "Failed to register MediaStore observer: ${e.message}")
        }
    }

    fun unregister() {
        try {
            contentResolver.unregisterContentObserver(this)
        } catch (e: Exception) {
            // ignore
        }
    }

    override fun onChange(selfChange: Boolean, uri: Uri?) {
        super.onChange(selfChange, uri)
        if (uri == null) return

        try {
            val projection = arrayOf(
                MediaStore.MediaColumns._ID,
                MediaStore.MediaColumns.DISPLAY_NAME,
                MediaStore.MediaColumns.SIZE,
                MediaStore.MediaColumns.MIME_TYPE,
                MediaStore.MediaColumns.DATA
            )

            contentResolver.query(uri, projection, null, null, null)?.use { cursor ->
                if (cursor.moveToFirst()) {
                    val nameIndex = cursor.getColumnIndex(MediaStore.MediaColumns.DISPLAY_NAME)
                    val sizeIndex = cursor.getColumnIndex(MediaStore.MediaColumns.SIZE)
                    val mimeIndex = cursor.getColumnIndex(MediaStore.MediaColumns.MIME_TYPE)
                    val dataIndex = cursor.getColumnIndex(MediaStore.MediaColumns.DATA)

                    val name = if (nameIndex >= 0) cursor.getString(nameIndex) else "unknown"
                    val size = if (sizeIndex >= 0) cursor.getLong(sizeIndex) else 0L
                    val mime = if (mimeIndex >= 0) cursor.getString(mimeIndex) else null
                    val path = if (dataIndex >= 0) cursor.getString(dataIndex) else null

                    val eventType = if (uri.toString().contains("download", ignoreCase = true)) {
                        "DOWNLOADED"
                    } else {
                        "CREATED"
                    }

                    onMediaEvent(eventType, uri, path, name, size, mime)
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Error querying MediaStore URI $uri: ${e.message}")
        }
    }

    companion object {
        private const val TAG = "MediaStoreObserver"
    }
}
