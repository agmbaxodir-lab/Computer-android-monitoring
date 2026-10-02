package com.filemonitoring.agent.monitor

import android.content.Context
import android.os.Environment
import android.util.Log
import android.webkit.MimeTypeMap
import com.filemonitoring.agent.db.AppDatabase
import com.filemonitoring.agent.db.EventEntity
import com.filemonitoring.agent.security.SecureStorage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import java.io.File
import java.io.FileInputStream
import java.security.MessageDigest
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap

/**
 * Android fayl monitoring tizimining boshqaruv markazi.
 * Barcha observer'lar, dedup kesh, hash hisoblash va mahalliy navbatga yozishni muvofiqlashtiradi.
 */
class FileMonitoringManager(private val context: Context) {

    private val storage = SecureStorage(context)
    private val db = AppDatabase.getInstance(context)
    private val appDetector = AppUsageDetector(context)
    private val scope = CoroutineScope(Dispatchers.IO + Job())

    private val fileObservers = mutableListOf<RecursiveFileObserver>()
    private var mediaStoreObserver: MediaStoreFileObserver? = null

    // Dedup kesh: path -> so'nggi hodisa vaqti (takroriy spam hodisalarni oldini olish uchun)
    private val recentEventsCache = ConcurrentHashMap<String, Long>()

    var onEventCaptured: ((EventEntity) -> Unit)? = null

    fun startMonitoring() {
        stopMonitoring()
        Log.i(TAG, "Starting Android file monitoring...")

        // Kuzatuv ostidagi asosiy jildlar
        val directoriesToWatch = listOfNotNull(
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS),
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS),
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_PICTURES),
            Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DCIM),
            Environment.getExternalStorageDirectory() // Umumiy tashqi xotira ildizi
        ).filter { it.exists() && it.canRead() }

        // Recursive inotify observerlarni ishga tushirish
        directoriesToWatch.forEach { dir ->
            val observer = RecursiveFileObserver(dir.absolutePath) { type, path, sourcePath ->
                handleInotifyEvent(type, path, sourcePath)
            }
            observer.startWatching()
            fileObservers.add(observer)
        }

        // MediaStore observerni ishga tushirish
        mediaStoreObserver = MediaStoreFileObserver(context) { type, uri, path, name, size, mime ->
            handleMediaStoreEvent(type, path, name, size, mime)
        }.also { it.register() }

        Log.i(TAG, "File monitoring active on ${directoriesToWatch.size} public directories")
    }

    fun stopMonitoring() {
        fileObservers.forEach { it.stopWatching() }
        fileObservers.clear()
        mediaStoreObserver?.unregister()
        mediaStoreObserver = null
        recentEventsCache.clear()
        Log.i(TAG, "File monitoring stopped")
    }

    private fun handleInotifyEvent(
        type: RecursiveFileObserver.EventType,
        path: String,
        sourcePath: String?
    ) {
        val file = File(path)
        if (file.name.startsWith(".") || file.isDirectory) return

        // 2 soniyalik dedup oynasi
        val now = System.currentTimeMillis()
        val lastTime = recentEventsCache[path] ?: 0L
        if (now - lastTime < 2000L && type == RecursiveFileObserver.EventType.MODIFIED) {
            return
        }
        recentEventsCache[path] = now

        scope.launch {
            try {
                val size = if (file.exists()) file.length() else 0L
                val ext = file.extension.let { if (it.isNotEmpty()) ".$it" else null }
                val mime = ext?.let { MimeTypeMap.getSingleton().getMimeTypeFromExtension(it.trimStart('.')) }
                val sha256 = if (file.exists() && size in 1..(50 * 1024 * 1024)) calculateSha256(file) else null

                // Ilovani aniqlash (Foreground app yoki path analizi)
                val activeApp = appDetector.getForegroundApp() ?: appDetector.detectAppFromPath(path)
                val confidence = when {
                    activeApp != null && sha256 != null -> 1.0f
                    activeApp != null -> 0.85f
                    else -> 0.70f
                }

                val eventTypeStr = when (type) {
                    RecursiveFileObserver.EventType.CREATED -> "CREATED"
                    RecursiveFileObserver.EventType.MODIFIED -> "MODIFIED"
                    RecursiveFileObserver.EventType.RENAMED -> "RENAMED"
                    RecursiveFileObserver.EventType.MOVED -> "MOVED"
                    RecursiveFileObserver.EventType.DELETED -> "DELETED"
                    RecursiveFileObserver.EventType.OPENED -> "OPENED"
                }

                enqueueEvent(
                    eventType = eventTypeStr,
                    fileName = file.name,
                    filePath = path,
                    fileExtension = ext,
                    mimeType = mime,
                    fileSize = size,
                    sha256 = sha256,
                    application = activeApp?.appName,
                    processName = activeApp?.packageName,
                    confidence = confidence
                )
            } catch (e: Exception) {
                Log.w(TAG, "Error handling inotify event: ${e.message}")
            }
        }
    }

    private fun handleMediaStoreEvent(
        eventType: String,
        path: String?,
        name: String,
        size: Long,
        mime: String?
    ) {
        val resolvedPath = path ?: "/sdcard/Download/$name"
        val now = System.currentTimeMillis()
        val lastTime = recentEventsCache[resolvedPath] ?: 0L
        if (now - lastTime < 2500L) return
        recentEventsCache[resolvedPath] = now

        scope.launch {
            try {
                val ext = name.substringAfterLast('.', "").let { if (it.isNotEmpty()) ".$it" else null }
                val file = path?.let { File(it) }
                val sha256 = if (file != null && file.exists() && size in 1..(50 * 1024 * 1024)) {
                    calculateSha256(file)
                } else null

                val activeApp = appDetector.getForegroundApp() ?: appDetector.detectAppFromPath(resolvedPath)
                val confidence = if (activeApp != null) 0.95f else 0.80f

                enqueueEvent(
                    eventType = eventType,
                    fileName = name,
                    filePath = resolvedPath,
                    fileExtension = ext,
                    mimeType = mime,
                    fileSize = size,
                    sha256 = sha256,
                    application = activeApp?.appName,
                    processName = activeApp?.packageName,
                    confidence = confidence
                )
            } catch (e: Exception) {
                Log.w(TAG, "Error handling media store event: ${e.message}")
            }
        }
    }

    suspend fun enqueueEvent(
        eventType: String,
        fileName: String,
        filePath: String?,
        fileExtension: String?,
        mimeType: String?,
        fileSize: Long,
        sha256: String?,
        application: String?,
        processName: String?,
        confidence: Float
    ) {
        val deviceId = storage.deviceId ?: UUID.randomUUID().toString()
        val timestamp = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
            timeZone = TimeZone.getTimeZone("UTC")
        }.format(Date())

        val event = EventEntity(
            eventId = UUID.randomUUID().toString(),
            deviceId = deviceId,
            username = null,
            application = application,
            processName = processName,
            fileName = fileName,
            filePath = filePath,
            fileExtension = fileExtension?.lowercase(Locale.ROOT),
            mimeType = mimeType,
            fileSize = fileSize,
            sha256 = sha256?.lowercase(Locale.ROOT),
            eventType = eventType,
            timestamp = timestamp,
            confidence = confidence,
            platform = "Android"
        )

        db.eventDao().insert(event)
        Log.i(TAG, "Event queued: [$eventType] $fileName ($fileSize bytes, app: $application)")
        onEventCaptured?.invoke(event)
    }

    /**
     * Test yoki diagnostika maqsadi uchun hodisa hosil qilish
     */
    fun createTestEvent(fileName: String = "sample_confidential.pdf", eventType: String = "CREATED") {
        scope.launch {
            val fakeHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
            enqueueEvent(
                eventType = eventType,
                fileName = fileName,
                filePath = "/sdcard/Download/$fileName",
                fileExtension = ".pdf",
                mimeType = "application/pdf",
                fileSize = 1048576L,
                sha256 = fakeHash,
                application = "Telegram (Android)",
                processName = "org.telegram.messenger",
                confidence = 0.99f
            )
        }
    }

    private fun calculateSha256(file: File): String? {
        return try {
            val digest = MessageDigest.getInstance("SHA-256")
            FileInputStream(file).use { fis ->
                val buffer = ByteArray(8192)
                var bytesRead: Int
                while (fis.read(buffer).also { bytesRead = it } != -1) {
                    digest.update(buffer, 0, bytesRead)
                }
            }
            digest.digest().joinToString("") { "%02x".format(it) }
        } catch (e: Exception) {
            null
        }
    }

    companion object {
        private const val TAG = "FileMonitoringManager"
    }
}
