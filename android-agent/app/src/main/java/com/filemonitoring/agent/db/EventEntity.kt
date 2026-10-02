package com.filemonitoring.agent.db

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import com.filemonitoring.agent.api.EventDto
import com.filemonitoring.agent.api.FileInfoDto

/**
 * Internet yo'q paytda hodisalarni xavfsiz navbatda saqlash uchun Room entity.
 * eventId PRIMARY KEY bo'lgani sababli bitta hodisa ikki marta yozilmaydi (idempotent).
 */
@Entity(
    tableName = "local_events_queue",
    indices = [Index(value = ["createdAt"])]
)
data class EventEntity(
    @PrimaryKey
    val eventId: String,
    val deviceId: String,
    val username: String?,
    val application: String?,
    val processName: String?,
    val fileName: String,
    val filePath: String?,
    val fileExtension: String?,
    val mimeType: String?,
    val fileSize: Long,
    val sha256: String?,
    val eventType: String,
    val timestamp: String,
    val confidence: Float,
    val platform: String = "Android",
    val createdAt: Long = System.currentTimeMillis(),
    val retryCount: Int = 0
) {
    fun toDto(): EventDto {
        return EventDto(
            eventId = eventId,
            deviceId = deviceId,
            username = username,
            application = application,
            processName = processName,
            file = FileInfoDto(
                name = fileName,
                extension = fileExtension,
                mimeType = mimeType,
                size = fileSize,
                sha256 = sha256,
                path = filePath
            ),
            eventType = eventType,
            timestamp = timestamp,
            confidence = confidence,
            platform = platform
        )
    }

    companion object {
        fun fromDto(dto: EventDto): EventEntity {
            return EventEntity(
                eventId = dto.eventId,
                deviceId = dto.deviceId,
                username = dto.username,
                application = dto.application,
                processName = dto.processName,
                fileName = dto.file.name,
                filePath = dto.file.path,
                fileExtension = dto.file.extension,
                mimeType = dto.file.mimeType,
                fileSize = dto.file.size,
                sha256 = dto.file.sha256,
                eventType = dto.eventType,
                timestamp = dto.timestamp,
                confidence = dto.confidence,
                platform = dto.platform
            )
        }
    }
}
