package com.wms.mobile.ui

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.wms.mobile.data.local.TaskEntity
import com.wms.mobile.data.repository.TaskRepository
import com.wms.mobile.data.session.EndpointPolicy
import com.wms.mobile.data.session.SessionStore
import com.wms.mobile.scanner.ScanEvent
import com.wms.mobile.scanner.ScannerSource
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

enum class Screen { LOGIN, DASHBOARD, TASKS, TASK_DETAIL }
enum class NoticeKind { INFO, SUCCESS, ERROR, CONFLICT }
data class UiNotice(val kind: NoticeKind, val message: String)

data class MainUiState(
    val screen: Screen = Screen.LOGIN,
    val endpoint: String = "",
    val isOnline: Boolean = false,
    val isLoading: Boolean = false,
    val tasks: List<TaskEntity> = emptyList(),
    val selectedTask: TaskEntity? = null,
    val pendingCount: Int = 0,
    val conflictCount: Int = 0,
    val barcode: String = "",
    val quantity: String = "1",
    val lastScanSource: String? = null,
    val notice: UiNotice? = null,
)

class MainViewModel(
    private val repository: TaskRepository,
    private val session: SessionStore,
    networkOnline: kotlinx.coroutines.flow.Flow<Boolean>,
    scanner: ScannerSource,
    private val scheduleSync: () -> Unit,
) : ViewModel() {
    private val mutableState = MutableStateFlow(MainUiState(endpoint = session.endpoint))
    val state: StateFlow<MainUiState> = mutableState.asStateFlow()

    init {
        viewModelScope.launch { repository.tasks.collect { tasks -> mutableState.update { it.copy(tasks = tasks) } } }
        viewModelScope.launch { repository.pendingCount.collect { count -> mutableState.update { it.copy(pendingCount = count) } } }
        viewModelScope.launch { repository.conflictCount.collect { count -> mutableState.update { it.copy(conflictCount = count) } } }
        viewModelScope.launch { networkOnline.collect { online -> mutableState.update { it.copy(isOnline = online) } } }
        viewModelScope.launch { scanner.scans.collect(::onScan) }
    }

    fun login(endpointInput: String, token: String) {
        val endpoint = EndpointPolicy.normalize(endpointInput).getOrElse {
            setNotice(NoticeKind.ERROR, it.message ?: "Endpoint inválido.")
            return
        }
        if (token.isBlank()) {
            setNotice(NoticeKind.ERROR, "Ingresá un token de sesión válido. No se guarda en el dispositivo.")
            return
        }
        session.configure(endpoint, token)
        mutableState.update { it.copy(isLoading = true, endpoint = endpoint, notice = null) }
        viewModelScope.launch {
            runCatching { repository.bootstrapAndRefresh() }
                .onSuccess {
                    mutableState.update { it.copy(isLoading = false, screen = Screen.DASHBOARD, notice = UiNotice(NoticeKind.SUCCESS, "Sesión verificada y tareas actualizadas.")) }
                    scheduleSync()
                }
                .onFailure { error -> mutableState.update { it.copy(isLoading = false, notice = UiNotice(NoticeKind.ERROR, actionable(error))) } }
        }
    }

    fun logout() {
        session.clearSession()
        mutableState.update { MainUiState(screen = Screen.LOGIN, endpoint = session.endpoint) }
    }

    fun openDashboard() = mutableState.update { it.copy(screen = Screen.DASHBOARD, selectedTask = null) }
    fun openTasks() = mutableState.update { it.copy(screen = Screen.TASKS, selectedTask = null, notice = null) }
    fun openTask(task: TaskEntity) = mutableState.update {
        it.copy(screen = Screen.TASK_DETAIL, selectedTask = task, barcode = "", quantity = task.expectedQuantity.toString(), notice = null)
    }

    fun refresh() {
        if (!state.value.isOnline) {
            setNotice(NoticeKind.INFO, "Sin conexión. Las tareas descargadas siguen disponibles.")
            return
        }
        mutableState.update { it.copy(isLoading = true, notice = null) }
        viewModelScope.launch {
            runCatching { repository.refreshAssignedTasks() }
                .onSuccess { mutableState.update { it.copy(isLoading = false, notice = UiNotice(NoticeKind.SUCCESS, "Tareas actualizadas.")) } }
                .onFailure { error -> mutableState.update { it.copy(isLoading = false, notice = UiNotice(NoticeKind.ERROR, actionable(error))) } }
        }
    }

    fun syncNow() {
        if (!state.value.isOnline) {
            setNotice(NoticeKind.INFO, "La cola se enviará cuando vuelva la conexión.")
            return
        }
        mutableState.update { it.copy(isLoading = true, notice = null) }
        viewModelScope.launch {
            runCatching { repository.syncPending() }
                .onSuccess { summary ->
                    val notice = when {
                        summary.requiresAuthentication -> UiNotice(NoticeKind.ERROR, "La sesión venció. Iniciá sesión para continuar la sincronización.")
                        summary.conflicts > 0 -> UiNotice(NoticeKind.CONFLICT, "${summary.conflicts} comando(s) requieren revisión. El stock no fue modificado automáticamente.")
                        summary.sent == 0 -> UiNotice(NoticeKind.INFO, "La cola ya está al día.")
                        else -> UiNotice(NoticeKind.SUCCESS, "${summary.acknowledged} de ${summary.sent} comando(s) confirmados.")
                    }
                    mutableState.update { it.copy(isLoading = false, notice = notice) }
                }
                .onFailure { error -> mutableState.update { it.copy(isLoading = false, notice = UiNotice(NoticeKind.ERROR, actionable(error))) } }
        }
    }

    fun onManualBarcode(value: String) = mutableState.update { it.copy(barcode = value, lastScanSource = "Manual") }
    fun onQuantity(value: String) = mutableState.update { it.copy(quantity = value.filter { char -> char.isDigit() || char == '.' }) }
    fun onScan(event: ScanEvent) = mutableState.update {
        it.copy(barcode = event.value, lastScanSource = event.source, notice = UiNotice(NoticeKind.INFO, "Código leído por ${event.source}."))
    }

    fun executeSelected() {
        val current = state.value
        val task = current.selectedTask ?: return
        val quantity = current.quantity.toDoubleOrNull()
        if (quantity == null || quantity <= 0) {
            setNotice(NoticeKind.ERROR, "Ingresá una cantidad mayor que cero.")
            return
        }
        mutableState.update { it.copy(isLoading = true, notice = null) }
        viewModelScope.launch {
            runCatching { repository.enqueueExecution(task.taskId, current.barcode, quantity) }
                .onSuccess {
                    scheduleSync()
                    mutableState.update { state ->
                        state.copy(
                            isLoading = false,
                            screen = Screen.TASKS,
                            selectedTask = null,
                            notice = UiNotice(NoticeKind.SUCCESS, "Ejecución guardada en la cola durable."),
                        )
                    }
                }
                .onFailure { error -> mutableState.update { it.copy(isLoading = false, notice = UiNotice(NoticeKind.ERROR, actionable(error))) } }
        }
    }

    fun clearNotice() = mutableState.update { it.copy(notice = null) }
    private fun setNotice(kind: NoticeKind, message: String) = mutableState.update { it.copy(notice = UiNotice(kind, message)) }
    private fun actionable(error: Throwable): String = error.message?.takeIf(String::isNotBlank)
        ?: "No se completó la operación. Revisá la conexión e intentá nuevamente."

    class Factory(
        private val repository: TaskRepository,
        private val session: SessionStore,
        private val networkOnline: kotlinx.coroutines.flow.Flow<Boolean>,
        private val scanner: ScannerSource,
        private val scheduleSync: () -> Unit,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T =
            MainViewModel(repository, session, networkOnline, scanner, scheduleSync) as T
    }
}
