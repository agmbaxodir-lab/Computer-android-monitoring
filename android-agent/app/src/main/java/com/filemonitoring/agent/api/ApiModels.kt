package com.filemonitoring.agent.api

import com.google.gson.annotations.SerializedName

data class RegisterRequest(
    @SerializedName("enrollmentToken") val enrollmentToken: String,
    @SerializedName("hostname") val hostname: String,
    @SerializedName("username") val username: String?,
    @SerializedName("osVersion") val osVersion: String?,
    @SerializedName("agentVersion") val agentVersion: String?,
    @SerializedName("platform") val platform: String = "Android",
    @SerializedName("deviceModel") val deviceModel: String? = null
)

data class RegisterResponse(
    @SerializedName("deviceId") val deviceId: String,
    @SerializedName("deviceSecret") val deviceSecret: String
)

data class HeartbeatRequest(
    @SerializedName("agentVersion") val agentVersion: String?,
    @SerializedName("username") val username: String?,
    @SerializedName("cpuPercent") val cpuPercent: Float?,
    @SerializedName("memoryMb") val memoryMb: Float?,
    @SerializedName("queueSize") val queueSize: Int?
)

data class PendingNotificationDto(
    @SerializedName("id") val id: String,
    @SerializedName("title") val title: String,
    @SerializedName("message") val message: String
)

data class HeartbeatResponse(
    @SerializedName("serverTime") val serverTime: String,
    @SerializedName("pendingNotifications") val pendingNotifications: List<PendingNotificationDto>
)

data class FileInfoDto(
    @SerializedName("name") val name: String,
    @SerializedName("extension") val extension: String?,
    @SerializedName("mimeType") val mimeType: String?,
    @SerializedName("size") val size: Long,
    @SerializedName("sha256") val sha256: String?,
    @SerializedName("path") val path: String? = null,
    @SerializedName("source") val source: String? = null,
    @SerializedName("destination") val destination: String? = null
)

data class EventDto(
    @SerializedName("eventId") val eventId: String,
    @SerializedName("deviceId") val deviceId: String,
    @SerializedName("username") val username: String?,
    @SerializedName("application") val application: String?,
    @SerializedName("processName") val processName: String?,
    @SerializedName("file") val file: FileInfoDto,
    @SerializedName("eventType") val eventType: String,
    @SerializedName("timestamp") val timestamp: String,
    @SerializedName("confidence") val confidence: Float,
    @SerializedName("platform") val platform: String = "Android"
)

data class AppConfig(
    @SerializedName("name") val name: String,
    @SerializedName("processNames") val processNames: List<String>,
    @SerializedName("enabled") val enabled: Boolean
)

data class ConfigResponse(
    @SerializedName("applications") val applications: List<AppConfig>,
    @SerializedName("heartbeatSeconds") val heartbeatSeconds: Int,
    @SerializedName("configPollSeconds") val configPollSeconds: Int
)

enum class SendResult {
    OK,
    RETRY,
    REJECT
}
