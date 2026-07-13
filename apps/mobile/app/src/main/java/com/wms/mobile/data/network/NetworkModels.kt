package com.wms.mobile.data.network

import kotlinx.serialization.Serializable

@Serializable
data class BootstrapDto(
    val tenantId: String,
    val warehouseId: String,
    val warehouseCode: String,
    val deviceId: String,
    val userId: String,
    val serverTime: String,
    val checkpoint: Long = 0,
    val scopes: List<String> = emptyList(),
)

@Serializable
data class TaskPageDto(
    val tasks: List<TaskDto> = emptyList(),
    val checkpoint: Long = 0,
)

@Serializable
data class TaskDto(
    val taskId: String,
    val tenantId: String,
    val warehouseId: String,
    val type: String,
    val status: String,
    val entityVersion: Long,
    val title: String,
    val instruction: String,
    val sourceLocation: String? = null,
    val destinationLocation: String? = null,
    val sku: String,
    val expectedQuantity: Double,
    val updatedAt: String,
)

@Serializable
data class SyncBatchRequest(val commands: List<OfflineCommandDto>)

@Serializable
data class OfflineCommandDto(
    val commandId: String,
    val commandType: String,
    val schemaVersion: String,
    val tenantId: String,
    val warehouseId: String,
    val deviceId: String,
    val userId: String,
    val occurredAt: String,
    val localSequence: Long,
    val entityVersion: Long,
    val taskId: String,
    val payload: kotlinx.serialization.json.JsonObject,
)

@Serializable
data class SyncBatchResponse(val results: List<CommandResultDto> = emptyList())

@Serializable
data class CommandResultDto(
    val commandId: String,
    val status: String,
    val code: String? = null,
    val message: String? = null,
    val currentVersion: Long? = null,
)

@Serializable
data class TaskExecutionPayload(
    val barcode: String,
    val quantity: Double,
    val sourceLocation: String? = null,
    val destinationLocation: String? = null,
)
