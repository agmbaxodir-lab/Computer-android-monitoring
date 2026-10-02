package com.filemonitoring.agent.api

import com.filemonitoring.agent.security.SecureStorage
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Har bir himoyalangan agent so'roviga X-Device-Id va X-Device-Secret headerlarini qo'shadi.
 */
class DeviceAuthInterceptor(private val storage: SecureStorage) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val original = chain.request()
        val builder = original.newBuilder()

        storage.deviceId?.let { builder.addHeader("X-Device-Id", it) }
        storage.deviceSecret?.let { builder.addHeader("X-Device-Secret", it) }

        return chain.proceed(builder.build())
    }
}
