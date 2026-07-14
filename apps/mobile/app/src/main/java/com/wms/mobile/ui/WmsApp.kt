package com.wms.mobile.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedCard
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.wms.mobile.data.local.TaskEntity
import com.wms.mobile.scanner.CameraScanner
import com.wms.mobile.ui.theme.Danger
import com.wms.mobile.ui.theme.Info
import com.wms.mobile.ui.theme.Primary
import com.wms.mobile.ui.theme.Success
import com.wms.mobile.ui.theme.Warning

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WmsApp(viewModel: MainViewModel, cameraScanner: CameraScanner) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    Scaffold(
        topBar = {
            if (state.screen != Screen.LOGIN) {
                Column {
                    TopAppBar(
                        title = { Text(screenTitle(state.screen)) },
                        navigationIcon = {
                            if (state.screen == Screen.TASKS || state.screen == Screen.TASK_DETAIL) {
                                IconButton(onClick = if (state.screen == Screen.TASK_DETAIL) viewModel::openTasks else viewModel::openDashboard) {
                                    Icon(Icons.Default.ArrowBack, contentDescription = "Volver")
                                }
                            }
                        },
                        colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
                    )
                    PulseBar(state.isOnline, state.pendingCount, state.conflictCount)
                }
            }
        },
    ) { padding ->
        Box(Modifier.fillMaxSize().padding(padding)) {
            when (state.screen) {
                Screen.LOGIN -> LoginScreen(state, viewModel::login)
                Screen.DASHBOARD -> DashboardScreen(state, viewModel)
                Screen.TASKS -> TaskListScreen(state, viewModel)
                Screen.TASK_DETAIL -> TaskDetailScreen(state, viewModel, cameraScanner)
            }
            if (state.isLoading) LoadingOverlay()
        }
    }
}

@Composable
private fun LoginScreen(state: MainUiState, onLogin: (String, String) -> Unit) {
    var endpoint by remember(state.endpoint) { mutableStateOf(state.endpoint) }
    var token by remember { mutableStateOf("") }
    Column(
        Modifier.fillMaxSize().padding(horizontal = 24.dp, vertical = 42.dp),
        verticalArrangement = Arrangement.spacedBy(18.dp),
    ) {
        Surface(color = Primary, shape = RoundedCornerShape(8.dp)) {
            Icon(Icons.Default.Inventory2, null, tint = Color.White, modifier = Modifier.padding(14.dp).size(34.dp))
        }
        Text("Piso listo.", style = MaterialTheme.typography.headlineLarge)
        Text("Conectá este dispositivo al WMS. El token vive sólo durante la sesión y no se guarda.")
        OutlinedTextField(
            value = endpoint,
            onValueChange = { endpoint = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Endpoint WMS") },
            placeholder = { Text("https://wms.ejemplo.com") },
            singleLine = true,
        )
        OutlinedTextField(
            value = token,
            onValueChange = { token = it },
            modifier = Modifier.fillMaxWidth(),
            label = { Text("Token de acceso") },
            visualTransformation = PasswordVisualTransformation(),
            singleLine = true,
        )
        NoticePanel(state.notice)
        Button(onClick = { onLogin(endpoint, token) }, modifier = Modifier.fillMaxWidth().height(54.dp)) {
            Text("Verificar y entrar")
        }
        Text("HTTPS es obligatorio. Para desarrollo, se admite HTTP sólo en localhost/10.0.2.2.", style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun DashboardScreen(state: MainUiState, viewModel: MainViewModel) {
    LazyColumn(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item { NoticePanel(state.notice) }
        item {
            Text(if (state.isOnline) "Operación conectada" else "Operación local", style = MaterialTheme.typography.headlineLarge)
            Text(if (state.isOnline) "Podés descargar tareas y confirmar la cola." else "Podés ejecutar tareas descargadas. Packing y despacho requieren conexión.")
        }
        item {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                MetricCard("Pendientes", state.pendingCount.toString(), Modifier.weight(1f))
                MetricCard("Conflictos", state.conflictCount.toString(), Modifier.weight(1f), state.conflictCount > 0)
            }
        }
        item {
            Button(onClick = viewModel::openTasks, modifier = Modifier.fillMaxWidth().height(54.dp)) {
                Text("Ver ${state.tasks.size} tarea(s)")
            }
        }
        item {
            OutlinedButton(onClick = viewModel::syncNow, modifier = Modifier.fillMaxWidth().height(54.dp)) {
                Text("Sincronizar ahora")
            }
        }
        item {
            OutlinedButton(onClick = viewModel::logout, modifier = Modifier.fillMaxWidth().height(54.dp)) {
                Text("Cerrar sesión")
            }
        }
    }
}

@Composable
private fun TaskListScreen(state: MainUiState, viewModel: MainViewModel) {
    LazyColumn(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        item { NoticePanel(state.notice) }
        item {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                Text("Asignadas", style = MaterialTheme.typography.headlineLarge)
                OutlinedButton(onClick = viewModel::refresh, enabled = state.isOnline) { Text("Actualizar") }
            }
        }
        if (state.tasks.isEmpty() && !state.isLoading) {
            item { StateCard("Sin tareas", "No hay tareas descargadas. Conectate y tocá Actualizar.", Info) }
        } else {
            items(state.tasks, key = { it.taskId }) { task -> TaskCard(task) { viewModel.openTask(task) } }
        }
    }
}

@Composable
private fun TaskCard(task: TaskEntity, onClick: () -> Unit) {
    OutlinedCard(onClick = onClick, modifier = Modifier.fillMaxWidth(), border = BorderStroke(1.dp, statusColor(task.status))) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
                Text(taskTypeLabel(task.type), style = MaterialTheme.typography.labelLarge, color = Primary)
                StatusPill(task.status)
            }
            Text(task.title, style = MaterialTheme.typography.titleMedium)
            Text("${task.sku} · ${formatQuantity(task.expectedQuantity)} u.")
            Text(task.instruction, style = MaterialTheme.typography.bodyMedium)
        }
    }
}

@Composable
private fun TaskDetailScreen(state: MainUiState, viewModel: MainViewModel, cameraScanner: CameraScanner) {
    val task = state.selectedTask ?: return
    LazyColumn(
        Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item { NoticePanel(state.notice) }
        if (task.status == "CONFLICT") {
            item { StateCard("Conflicto", "La versión del servidor cambió. La tarea está pausada y requiere revisión; no se ajustó stock.", Warning) }
        }
        item {
            Text(taskTypeLabel(task.type), style = MaterialTheme.typography.labelLarge, color = Primary)
            Text(task.title, style = MaterialTheme.typography.headlineLarge)
            Text(task.instruction)
        }
        item {
            DetailRow("SKU", task.sku)
            task.sourceLocation?.let { DetailRow("Origen", it) }
            task.destinationLocation?.let { DetailRow("Destino", it) }
            DetailRow("Versión", task.entityVersion.toString())
        }
        item {
            OutlinedTextField(
                value = state.barcode,
                onValueChange = viewModel::onManualBarcode,
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Código escaneado") },
                leadingIcon = { Icon(Icons.Default.QrCodeScanner, null) },
                supportingText = { Text(state.lastScanSource?.let { "Origen: $it" } ?: "Usá DataWedge, cámara o ingreso manual") },
                singleLine = true,
            )
        }
        item {
            OutlinedButton(
                onClick = {
                    cameraScanner.launch { result ->
                        result.onSuccess(viewModel::onScan)
                            .onFailure { error -> if (error !is com.wms.mobile.scanner.CancellationException) viewModel.onManualBarcode("") }
                    }
                },
                modifier = Modifier.fillMaxWidth().height(52.dp),
            ) {
                Icon(Icons.Default.CameraAlt, null)
                Spacer(Modifier.size(8.dp))
                Text("Escanear con cámara")
            }
        }
        item {
            OutlinedTextField(
                value = state.quantity,
                onValueChange = viewModel::onQuantity,
                modifier = Modifier.fillMaxWidth(),
                label = { Text("Cantidad") },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                singleLine = true,
            )
        }
        item {
            Button(
                onClick = viewModel::executeSelected,
                enabled = task.status !in setOf("CONFLICT", "COMPLETED", "EXPIRED"),
                modifier = Modifier.fillMaxWidth().height(58.dp),
            ) { Text(executionLabel(task.type)) }
        }
        if (!state.isOnline) {
            item { StateCard("Se guardará localmente", "La confirmación quedará pendiente hasta recuperar conexión.", Info) }
        }
    }
}

@Composable
private fun PulseBar(online: Boolean, pending: Int, conflicts: Int) {
    val color = when { conflicts > 0 -> Warning; online -> Success; else -> Info }
    Row(
        Modifier.fillMaxWidth().background(color).padding(horizontal = 16.dp, vertical = 9.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Icon(if (online) Icons.Default.CloudDone else Icons.Default.CloudOff, null, tint = Color.White)
        Text(if (online) "EN LÍNEA" else "OFFLINE", color = Color.White, fontWeight = FontWeight.Black)
        Spacer(Modifier.weight(1f))
        Text("$pending en cola · $conflicts conflicto(s)", color = Color.White, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun NoticePanel(notice: UiNotice?) {
    if (notice == null) return
    val color = when (notice.kind) {
        NoticeKind.INFO -> Info
        NoticeKind.SUCCESS -> Success
        NoticeKind.ERROR -> Danger
        NoticeKind.CONFLICT -> Warning
    }
    StateCard(
        title = when (notice.kind) {
            NoticeKind.INFO -> "Información"
            NoticeKind.SUCCESS -> "Listo"
            NoticeKind.ERROR -> "No se completó"
            NoticeKind.CONFLICT -> "Requiere revisión"
        },
        message = notice.message,
        color = color,
    )
}

@Composable
private fun StateCard(title: String, message: String, color: Color) {
    Card(colors = CardDefaults.cardColors(containerColor = color.copy(alpha = 0.10f)), modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(14.dp)) {
            Text(title, fontWeight = FontWeight.Bold, color = color)
            Text(message)
        }
    }
}

@Composable
private fun MetricCard(label: String, value: String, modifier: Modifier, alert: Boolean = false) {
    Card(modifier, colors = CardDefaults.cardColors(containerColor = if (alert) Warning.copy(alpha = 0.12f) else MaterialTheme.colorScheme.surface)) {
        Column(Modifier.padding(16.dp)) {
            Text(value, style = MaterialTheme.typography.headlineLarge, color = if (alert) Warning else Primary)
            Text(label, fontWeight = FontWeight.Bold)
        }
    }
}

@Composable
private fun StatusPill(status: String) {
    val color = statusColor(status)
    Surface(color = color.copy(alpha = 0.12f), shape = RoundedCornerShape(4.dp)) {
        Text(status.replace('_', ' '), color = color, fontWeight = FontWeight.Bold, modifier = Modifier.padding(horizontal = 8.dp, vertical = 4.dp))
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth().padding(vertical = 7.dp), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(label, color = MaterialTheme.colorScheme.outline)
        Text(value, fontWeight = FontWeight.Bold)
    }
    HorizontalDivider()
}

@Composable
private fun LoadingOverlay() {
    Box(Modifier.fillMaxSize().background(Color.Black.copy(alpha = 0.25f)), contentAlignment = Alignment.Center) {
        Card { Row(Modifier.padding(20.dp), verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            CircularProgressIndicator(Modifier.size(28.dp))
            Text("Procesando…", fontWeight = FontWeight.Bold)
        } }
    }
}

private fun screenTitle(screen: Screen) = when (screen) {
    Screen.LOGIN -> "Acceso"
    Screen.DASHBOARD -> "Pulso operativo"
    Screen.TASKS -> "Tareas"
    Screen.TASK_DETAIL -> "Ejecutar tarea"
}
private fun taskTypeLabel(type: String) = when (type) { "RECEIVE" -> "RECEPCIÓN"; "PUTAWAY" -> "PUTAWAY"; "PICK" -> "PICKING"; else -> type }
private fun executionLabel(type: String) = when (type) { "RECEIVE" -> "Guardar recepción"; "PUTAWAY" -> "Confirmar putaway"; "PICK" -> "Guardar pick"; else -> "Guardar ejecución" }
private fun statusColor(status: String) = when (status) { "COMPLETED" -> Success; "CONFLICT", "REQUIRES_REVIEW" -> Warning; "REJECTED", "EXPIRED" -> Danger; "SYNC_PENDING" -> Info; else -> Primary }
private fun formatQuantity(value: Double) = if (value % 1.0 == 0.0) value.toLong().toString() else value.toString()
