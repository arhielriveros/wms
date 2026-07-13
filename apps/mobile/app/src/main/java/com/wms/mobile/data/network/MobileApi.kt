package com.wms.mobile.data.network

interface MobileApi {
    suspend fun bootstrap(): BootstrapDto
    suspend fun assignedTasks(since: String? = null): TaskPageDto
    suspend fun syncCommands(request: SyncBatchRequest): SyncBatchResponse
}

fun interface MobileApiFactory {
    fun create(): MobileApi
}

class ApiException(val statusCode: Int, message: String) : RuntimeException(message)
class AuthenticationRequiredException : RuntimeException("La sesión no está disponible.")
