package com.wms.mobile.data.session

import android.content.Context
import java.util.concurrent.atomic.AtomicReference

data class SessionContext(
    val tenantId: String,
    val warehouseId: String,
    val userId: String,
)

class SessionStore(context: Context) {
    private val preferences = context.getSharedPreferences("wms_public_config", Context.MODE_PRIVATE)
    private val accessToken = AtomicReference<String?>(null)
    private val sessionContext = AtomicReference<SessionContext?>(null)

    val endpoint: String get() = preferences.getString(KEY_ENDPOINT, "").orEmpty()
    val token: String? get() = accessToken.get()
    val context: SessionContext? get() = sessionContext.get()

    fun configure(endpoint: String, token: String) {
        preferences.edit().putString(KEY_ENDPOINT, endpoint.trimEnd('/')).apply()
        accessToken.set(token.trim())
    }

    fun setContext(value: SessionContext) = sessionContext.set(value)

    fun clearSession() {
        accessToken.set(null)
        sessionContext.set(null)
    }

    private companion object { const val KEY_ENDPOINT = "endpoint" }
}

object EndpointPolicy {
    fun normalize(raw: String): Result<String> = runCatching {
        val value = raw.trim().trimEnd('/')
        val uri = java.net.URI(value)
        require(uri.host != null) { "Ingresá un endpoint válido." }
        val local = uri.host == "localhost" || uri.host == "10.0.2.2"
        require(uri.scheme == "https" || (local && uri.scheme == "http")) {
            "El endpoint debe usar HTTPS (HTTP sólo para localhost)."
        }
        value
    }
}
