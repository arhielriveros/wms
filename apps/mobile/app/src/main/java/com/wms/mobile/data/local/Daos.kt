package com.wms.mobile.data.local

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import kotlinx.coroutines.flow.Flow

@Dao
interface TaskDao {
    @Query("SELECT * FROM assigned_tasks ORDER BY updatedAt DESC")
    fun observeAll(): Flow<List<TaskEntity>>

    @Query("SELECT * FROM assigned_tasks WHERE taskId = :taskId LIMIT 1")
    suspend fun find(taskId: String): TaskEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(tasks: List<TaskEntity>)

    @Query("UPDATE assigned_tasks SET status = :status WHERE taskId = :taskId")
    suspend fun updateStatus(taskId: String, status: String)
}

@Dao
interface OfflineCommandDao {
    @Query("SELECT COUNT(*) FROM offline_commands WHERE status IN ('PENDING', 'SENDING', 'UNAUTHORIZED')")
    fun observePendingCount(): Flow<Int>

    @Query("SELECT COUNT(*) FROM offline_commands WHERE status IN ('CONFLICT', 'REQUIRES_REVIEW')")
    fun observeConflictCount(): Flow<Int>

    @Query("SELECT COALESCE(MAX(localSequence), 0) FROM offline_commands WHERE taskId = :taskId")
    suspend fun maxSequence(taskId: String): Long

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(command: OfflineCommandEntity): Long

    @Query("SELECT * FROM offline_commands WHERE status IN ('PENDING', 'SENDING', 'UNAUTHORIZED') ORDER BY taskId, localSequence LIMIT :limit")
    suspend fun pending(limit: Int): List<OfflineCommandEntity>

    @Query("UPDATE offline_commands SET status = :status, resultCode = :code, resultMessage = :message WHERE commandId = :commandId")
    suspend fun updateResult(commandId: String, status: String, code: String?, message: String?)
}
