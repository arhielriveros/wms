package com.wms.mobile.data.repository

import com.wms.mobile.data.local.OfflineCommandDao
import com.wms.mobile.data.local.OfflineCommandEntity
import com.wms.mobile.data.local.TaskDao
import com.wms.mobile.data.local.TaskEntity
import com.wms.mobile.data.network.MobileApiFactory
import com.wms.mobile.data.network.OfflineCommandDto
import com.wms.mobile.data.network.SyncBatchRequest
import com.wms.mobile.data.network.TaskExecutionPayload
import com.wms.mobile.data.session.SessionContext
import com.wms.mobile.data.session.SessionStore
import com.wms.mobile.domain.CommandBatchPlanner
import com.wms.mobile.domain.CommandDisposition
import com.wms.mobile.domain.CommandResultPolicy
import com.wms.mobile.domain.CommandResultStatus
import com.wms.mobile.domain.PendingCommand
import java.time.Instant
import java.util.UUID
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonObject

class DefaultTaskRepository(
    private val taskDao: TaskDao,
    private val commandDao: OfflineCommandDao,
    private val apiFactory: MobileApiFactory,
    private val session: SessionStore,
    private val deviceId: String,
    private val json: Json,
) : TaskRepository {
    override val tasks = taskDao.observeAll()
    override val pendingCount = commandDao.observePendingCount()
    override val conflictCount = commandDao.observeConflictCount()
    private val enqueueMutex = Mutex()

    override suspend fun bootstrapAndRefresh() {
        val api = apiFactory.create()
        val bootstrap = api.bootstrap()
        session.setContext(SessionContext(bootstrap.tenantId, bootstrap.warehouseId, bootstrap.userId))
        refreshAssignedTasks()
    }

    override suspend fun refreshAssignedTasks() {
        val response = apiFactory.create().assignedTasks()
        taskDao.upsertAll(response.tasks.map { dto ->
            TaskEntity(
                taskId = dto.taskId,
                tenantId = dto.tenantId,
                warehouseId = dto.warehouseId,
                type = dto.type.uppercase(),
                status = dto.status.uppercase(),
                entityVersion = dto.entityVersion,
                title = dto.title,
                instruction = dto.instruction,
                sourceLocation = dto.sourceLocation,
                destinationLocation = dto.destinationLocation,
                sku = dto.sku,
                expectedQuantity = dto.expectedQuantity,
                updatedAt = dto.updatedAt,
            )
        })
    }

    override suspend fun enqueueExecution(taskId: String, barcode: String, quantity: Double): String =
        enqueueMutex.withLock {
            require(barcode.isNotBlank()) { "Escaneá o ingresá un código." }
            require(quantity > 0) { "La cantidad debe ser mayor que cero." }
            val task = requireNotNull(taskDao.find(taskId)) { "La tarea ya no está disponible." }
            val actor = requireNotNull(session.context) { "Iniciá sesión antes de ejecutar." }
            require(task.tenantId == actor.tenantId && task.warehouseId == actor.warehouseId) {
                "La tarea está fuera del alcance de la sesión."
            }
            val commandId = UUID.randomUUID().toString()
            val sequence = commandDao.maxSequence(taskId) + 1
            val commandType = when (task.type) {
                "RECEIVE" -> "ConfirmReceipt"
                "PUTAWAY" -> "ConfirmPutaway"
                "PICK" -> "ConfirmPick"
                else -> error("Tipo de tarea no soportado: ${task.type}")
            }
            val payload = TaskExecutionPayload(
                barcode = barcode.trim(),
                quantity = quantity,
                sourceLocation = task.sourceLocation,
                destinationLocation = task.destinationLocation,
            )
            val inserted = commandDao.insert(
                OfflineCommandEntity(
                    commandId = commandId,
                    commandType = commandType,
                    schemaVersion = "1.0",
                    tenantId = actor.tenantId,
                    warehouseId = actor.warehouseId,
                    deviceId = deviceId,
                    userId = actor.userId,
                    occurredAt = Instant.now().toString(),
                    localSequence = sequence,
                    entityVersion = task.entityVersion,
                    taskId = task.taskId,
                    payloadJson = json.encodeToString(payload),
                    status = "PENDING",
                ),
            )
            check(inserted != -1L) { "El comando ya existe." }
            taskDao.updateStatus(task.taskId, "SYNC_PENDING")
            commandId
        }

    override suspend fun syncPending(): SyncSummary {
        val raw = commandDao.pending(100)
        if (raw.isEmpty()) return SyncSummary(0, 0, 0, 0)
        val planned = CommandBatchPlanner.plan(raw.map { PendingCommand(it.commandId, it.taskId, it.localSequence) })
        val byId = raw.associateBy { it.commandId }
        val commands = planned.map { plannedCommand ->
            val entity = requireNotNull(byId[plannedCommand.commandId])
            commandDao.updateResult(entity.commandId, "SENDING", null, null)
            OfflineCommandDto(
                commandId = entity.commandId,
                commandType = entity.commandType,
                schemaVersion = entity.schemaVersion,
                tenantId = entity.tenantId,
                warehouseId = entity.warehouseId,
                deviceId = entity.deviceId,
                userId = entity.userId,
                occurredAt = entity.occurredAt,
                localSequence = entity.localSequence,
                entityVersion = entity.entityVersion,
                taskId = entity.taskId,
                payload = json.parseToJsonElement(entity.payloadJson).jsonObject,
            )
        }

        return try {
            val results = apiFactory.create().syncCommands(SyncBatchRequest(commands)).results
            var acknowledged = 0
            var conflicts = 0
            var rejected = 0
            var authRequired = false
            results.forEach { result ->
                val entity = byId[result.commandId] ?: return@forEach
                when (CommandResultPolicy.disposition(CommandResultStatus.fromWire(result.status))) {
                    CommandDisposition.ACKNOWLEDGED -> {
                        acknowledged++
                        commandDao.updateResult(entity.commandId, "ACKNOWLEDGED", result.code, result.message)
                        taskDao.updateStatus(entity.taskId, "COMPLETED")
                    }
                    CommandDisposition.CONFLICT -> {
                        conflicts++
                        val durableStatus = if (CommandResultStatus.fromWire(result.status) == CommandResultStatus.RequiresReview) {
                            "REQUIRES_REVIEW"
                        } else {
                            "CONFLICT"
                        }
                        commandDao.updateResult(entity.commandId, durableStatus, result.code, result.message)
                        taskDao.updateStatus(entity.taskId, "CONFLICT")
                    }
                    CommandDisposition.REJECTED -> {
                        rejected++
                        commandDao.updateResult(entity.commandId, "REJECTED", result.code, result.message)
                        taskDao.updateStatus(entity.taskId, "REJECTED")
                    }
                    CommandDisposition.EXPIRED -> {
                        rejected++
                        commandDao.updateResult(entity.commandId, "EXPIRED", result.code, result.message)
                        taskDao.updateStatus(entity.taskId, "EXPIRED")
                    }
                    CommandDisposition.AUTH_REQUIRED -> {
                        authRequired = true
                        commandDao.updateResult(entity.commandId, "UNAUTHORIZED", result.code, result.message)
                    }
                    CommandDisposition.RETRY -> commandDao.updateResult(entity.commandId, "PENDING", result.code, result.message)
                }
            }
            val returned = results.mapTo(mutableSetOf()) { it.commandId }
            commands.filterNot { it.commandId in returned }.forEach {
                commandDao.updateResult(it.commandId, "PENDING", "MISSING_RESULT", "El servidor no devolvió un resultado.")
            }
            SyncSummary(commands.size, acknowledged, conflicts, rejected, authRequired)
        } catch (error: Exception) {
            commands.forEach { commandDao.updateResult(it.commandId, "PENDING", "TRANSPORT", error.message) }
            throw error
        }
    }
}
