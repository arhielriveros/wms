package com.wms.mobile.domain

enum class TaskType { RECEIVE, PUTAWAY, PICK }

enum class TaskStatus {
    ASSIGNED, IN_PROGRESS, SYNC_PENDING, COMPLETED, CONFLICT, REJECTED, EXPIRED
}

enum class CommandResultStatus {
    Accepted, Rejected, Conflict, AlreadyProcessed, RequiresReview, Expired, Unauthorized, Unknown;

    companion object {
        fun fromWire(value: String): CommandResultStatus =
            entries.firstOrNull { it.name.equals(value, ignoreCase = true) } ?: Unknown
    }
}

enum class CommandDisposition { ACKNOWLEDGED, REJECTED, CONFLICT, EXPIRED, AUTH_REQUIRED, RETRY }

data class PendingCommand(
    val commandId: String,
    val taskId: String,
    val localSequence: Long,
)

object CommandBatchPlanner {
    fun plan(commands: List<PendingCommand>, limit: Int = 100): List<PendingCommand> =
        commands
            .distinctBy { it.commandId }
            .sortedWith(compareBy(PendingCommand::taskId, PendingCommand::localSequence))
            .take(limit.coerceIn(1, 100))
}

object CommandResultPolicy {
    fun disposition(status: CommandResultStatus): CommandDisposition = when (status) {
        CommandResultStatus.Accepted,
        CommandResultStatus.AlreadyProcessed -> CommandDisposition.ACKNOWLEDGED
        CommandResultStatus.Rejected -> CommandDisposition.REJECTED
        CommandResultStatus.Conflict,
        CommandResultStatus.RequiresReview -> CommandDisposition.CONFLICT
        CommandResultStatus.Expired -> CommandDisposition.EXPIRED
        CommandResultStatus.Unauthorized -> CommandDisposition.AUTH_REQUIRED
        CommandResultStatus.Unknown -> CommandDisposition.RETRY
    }
}
