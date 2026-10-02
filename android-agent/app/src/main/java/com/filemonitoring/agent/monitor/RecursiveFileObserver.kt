package com.filemonitoring.agent.monitor

import android.os.Build
import android.os.FileObserver
import android.util.Log
import java.io.File
import java.util.concurrent.ConcurrentHashMap

/**
 * Linux inotify API asosida belgilangan katalog va uning ichki papkalaridagi
 * barcha fayl amallarini kuzatuvchi rekursiv FileObserver.
 */
class RecursiveFileObserver(
    private val rootPath: String,
    private val onFileEvent: (type: EventType, path: String, sourcePath: String?) -> Unit
) {
    enum class EventType {
        CREATED,
        MODIFIED,
        RENAMED,
        MOVED,
        DELETED,
        OPENED
    }

    private val observers = ConcurrentHashMap<String, SingleFileObserver>()
    private val movePending = ConcurrentHashMap<Int, String>() // cookie -> sourcePath

    fun startWatching() {
        val root = File(rootPath)
        if (!root.exists() || !root.isDirectory) return
        watchDirectoryRecursive(root)
        Log.i(TAG, "Watching ${observers.size} directories under $rootPath")
    }

    fun stopWatching() {
        observers.values.forEach { it.stopWatching() }
        observers.clear()
        movePending.clear()
    }

    private fun watchDirectoryRecursive(dir: File) {
        val path = dir.absolutePath
        if (observers.containsKey(path)) return

        val obs = SingleFileObserver(path)
        observers[path] = obs
        obs.startWatching()

        dir.listFiles()?.forEach { file ->
            if (file.isDirectory && !file.name.startsWith(".")) {
                watchDirectoryRecursive(file)
            }
        }
    }

    private inner class SingleFileObserver(val dirPath: String) :
        FileObserver(dirPath, ALL_EVENTS_MASK) {

        override fun onEvent(event: Int, path: String?) {
            if (path == null) return
            val action = event and ALL_EVENTS
            val fullPath = "$dirPath/$path"
            val file = File(fullPath)

            when (action) {
                CREATE -> {
                    if (file.isDirectory) {
                        watchDirectoryRecursive(file)
                    }
                    onFileEvent(EventType.CREATED, fullPath, null)
                }

                MODIFY, CLOSE_WRITE -> {
                    if (!file.isDirectory) {
                        onFileEvent(EventType.MODIFIED, fullPath, null)
                    }
                }

                MOVED_FROM -> {
                    // cookie asosida source path saqlanadi
                    val cookie = (event shr 16) and 0xffff
                    movePending[cookie] = fullPath
                }

                MOVED_TO -> {
                    val cookie = (event shr 16) and 0xffff
                    val source = movePending.remove(cookie)
                    if (source != null) {
                        val sameParent = File(source).parent == File(fullPath).parent
                        val eventType = if (sameParent) EventType.RENAMED else EventType.MOVED
                        onFileEvent(eventType, fullPath, source)
                    } else {
                        onFileEvent(EventType.CREATED, fullPath, null)
                    }
                    if (file.isDirectory) {
                        watchDirectoryRecursive(file)
                    }
                }

                DELETE -> {
                    observers.remove(fullPath)?.stopWatching()
                    onFileEvent(EventType.DELETED, fullPath, null)
                }

                OPEN -> {
                    if (!file.isDirectory) {
                        onFileEvent(EventType.OPENED, fullPath, null)
                    }
                }
            }
        }
    }

    companion object {
        private const val TAG = "RecursiveFileObserver"
        private const val ALL_EVENTS_MASK =
            CREATE or MODIFY or CLOSE_WRITE or MOVED_FROM or MOVED_TO or DELETE or OPEN
    }
}
