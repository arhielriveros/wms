package com.wms.mobile.data.repository

import com.wms.mobile.data.local.TaskEntity
import kotlinx.coroutines.flow.Flow

data class SyncSummary(
    val sent: Int,
    val acknowledged: Int,
    val conflicts: Int,
    val rejected: Int,
    val requiresAuthentication: Boolean = false,
)

interface TaskRepository {
    val tasks: Flow<List<TaskEntity>>
    val pendingCount: Flow<Int>
    val conflictCount: Flow<Int>
    suspend fun bootstrapAndRefresh()
    suspend fun refreshAssignedTasks()
    suspend fun enqueueExecution(taskId: String, barcode: String, quantity: Double): String
    suspend fun syncPending(): SyncSummary
}
