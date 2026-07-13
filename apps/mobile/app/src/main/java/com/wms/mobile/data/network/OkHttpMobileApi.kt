package com.wms.mobile.data.network

import com.wms.mobile.data.session.SessionStore
import java.net.URLEncoder
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.serialization.decodeFromString
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.Call
import okhttp3.Callback
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import java.io.IOException

class OkHttpMobileApi(
    private val client: OkHttpClient,
    private val json: Json,
    private val session: SessionStore,
) : MobileApi {
    override suspend fun bootstrap(): BootstrapDto =
        get("/api/v1/mobile/bootstrap")

    override suspend fun assignedTasks(since: String?): TaskPageDto {
        val query = since?.let { "?since=${URLEncoder.encode(it, Charsets.UTF_8.name())}" }.orEmpty()
        return get("/api/v1/mobile/tasks$query")
    }

    override suspend fun syncCommands(request: SyncBatchRequest): SyncBatchResponse =
        post("/api/v1/mobile/commands:batch", json.encodeToString(request))

    private suspend inline fun <reified T> get(path: String): T =
        decode(execute(request(path).get().build()))

    private suspend inline fun <reified T> post(path: String, body: String): T =
        decode(execute(request(path).post(body.toRequestBody(JSON_MEDIA_TYPE)).build()))

    private fun request(path: String): Request.Builder {
        val token = session.token?.takeIf(String::isNotBlank) ?: throw AuthenticationRequiredException()
        return Request.Builder()
            .url(session.endpoint + path)
            .header("Authorization", "Bearer $token")
            .header("Accept", "application/json")
    }

    private suspend fun execute(request: Request): Response = suspendCancellableCoroutine { continuation ->
        val call = client.newCall(request)
        continuation.invokeOnCancellation { call.cancel() }
        call.enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                if (continuation.isActive) continuation.resumeWithException(e)
            }

            override fun onResponse(call: Call, response: Response) {
                if (continuation.isActive) continuation.resume(response)
                else response.close()
            }
        })
    }

    private inline fun <reified T> decode(response: Response): T = response.use {
        val body = it.body?.string().orEmpty()
        if (it.code == 401 || it.code == 403) throw AuthenticationRequiredException()
        if (!it.isSuccessful) throw ApiException(it.code, "El servidor respondió ${it.code}.")
        json.decodeFromString(body)
    }

    private companion object {
        val JSON_MEDIA_TYPE = "application/json; charset=utf-8".toMediaType()
    }
}
