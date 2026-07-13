package com.wms.mobile.data.local

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(tableName = "assigned_tasks", indices = [Index("status"), Index("updatedAt")])
data class TaskEntity(
    @PrimaryKey val taskId: String,
    val tenantId: String,
    val warehouseId: String,
    val type: String,
    val status: String,
    val entityVersion: Long,
    val title: String,
    val instruction: String,
    val sourceLocation: String?,
    val destinationLocation: String?,
    val sku: String,
    val expectedQuantity: Double,
    val updatedAt: String,
)

@Entity(
    tableName = "offline_commands",
    indices = [
        Index(value = ["taskId", "localSequence"], unique = true),
        Index("status"),
    ],
)
data class OfflineCommandEntity(
    @PrimaryKey val commandId: String,
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
    val payloadJson: String,
    val status: String,
    val resultCode: String? = null,
    val resultMessage: String? = null,
)
