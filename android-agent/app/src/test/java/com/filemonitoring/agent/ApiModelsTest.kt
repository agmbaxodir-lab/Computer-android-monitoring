package com.filemonitoring.agent

import com.filemonitoring.agent.api.EventDto
import com.filemonitoring.agent.api.FileInfoDto
import com.filemonitoring.agent.api.RegisterRequest
import com.filemonitoring.agent.api.RegisterResponse
import com.filemonitoring.agent.db.EventEntity
import com.google.gson.Gson
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.UUID

class ApiModelsTest {

    private val gson = Gson()

    @Test
    fun testRegisterRequestSerialization() {
        val req = RegisterRequest(
            enrollmentToken = "token-xyz-123",
            hostname = "Samsung-SM-S918B",
            username = "android_user",
            osVersion = "Android 14 (API 34)",
            agentVersion = "1.0.0",
            platform = "Android",
            deviceModel = "Samsung Galaxy S23 Ultra"
        )

        val json = gson.toJson(req)
        assertTrue(json.contains("\"enrollmentToken\":\"token-xyz-123\""))
        assertTrue(json.contains("\"platform\":\"Android\""))
        assertTrue(json.contains("\"deviceModel\":\"Samsung Galaxy S23 Ultra\""))

        val deserialized = gson.fromJson(json, RegisterRequest::class.java)
        assertEquals("Android", deserialized.platform)
        assertEquals("Samsung Galaxy S23 Ultra", deserialized.deviceModel)
    }

    @Test
    fun testEventDtoConversion() {
        val eventId = UUID.randomUUID().toString()
        val deviceId = UUID.randomUUID().toString()

        val dto = EventDto(
            eventId = eventId,
            deviceId = deviceId,
            username = "user1",
            application = "Telegram (Android)",
            processName = "org.telegram.messenger",
            file = FileInfoDto(
                name = "financial_statement.xlsx",
                extension = ".xlsx",
                mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                size = 524288,
                sha256 = "b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9",
                path = "/sdcard/Download/financial_statement.xlsx"
            ),
            eventType = "CREATED",
            timestamp = "2026-10-02T10:00:00.000Z",
            confidence = 1.0f,
            platform = "Android"
        )

        // Entity ga aylantirish
        val entity = EventEntity.fromDto(dto)
        assertEquals(eventId, entity.eventId)
        assertEquals("/sdcard/Download/financial_statement.xlsx", entity.filePath)
        assertEquals("Android", entity.platform)

        // Qaytadan Dto ga o'tkazish
        val backToDto = entity.toDto()
        assertEquals(dto.eventId, backToDto.eventId)
        assertEquals(dto.file.name, backToDto.file.name)
        assertEquals(dto.file.path, backToDto.file.path)
        assertEquals(dto.eventType, backToDto.eventType)
    }

    @Test
    fun testRegisterResponseDeserialization() {
        val json = """{"deviceId":"11111111-2222-3333-4444-555555555555","deviceSecret":"abcdef1234567890=="}"""
        val resp = gson.fromJson(json, RegisterResponse::class.java)
        assertNotNull(resp)
        assertEquals("11111111-2222-3333-4444-555555555555", resp.deviceId)
        assertEquals("abcdef1234567890==", resp.deviceSecret)
    }
}
