package com.wms.mobile.scanner

import android.app.Activity
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

data class ScanEvent(val value: String, val source: String, val symbology: String? = null)

interface ScannerSource {
    val scans: SharedFlow<ScanEvent>
    fun start()
    fun stop()
}

class DataWedgeScanner(private val context: Context) : ScannerSource {
    private val mutableScans = MutableSharedFlow<ScanEvent>(extraBufferCapacity = 16)
    override val scans = mutableScans.asSharedFlow()
    private var registered = false

    private val receiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            val value = intent?.getStringExtra(DATA_STRING)?.trim().orEmpty()
            if (value.isNotEmpty()) {
                mutableScans.tryEmit(ScanEvent(value, "DataWedge", intent?.getStringExtra(LABEL_TYPE)))
            }
        }
    }

    override fun start() {
        if (registered) return
        ContextCompat.registerReceiver(
            context,
            receiver,
            IntentFilter(ACTION_SCAN),
            ContextCompat.RECEIVER_EXPORTED,
        )
        registered = true
    }

    override fun stop() {
        if (!registered) return
        context.unregisterReceiver(receiver)
        registered = false
    }

    companion object {
        const val ACTION_SCAN = "com.wms.mobile.SCAN"
        private const val DATA_STRING = "com.symbol.datawedge.data_string"
        private const val LABEL_TYPE = "com.symbol.datawedge.label_type"
    }
}

fun interface CameraScanner {
    fun launch(onResult: (Result<ScanEvent>) -> Unit)
}

class GoogleCodeCameraScanner(private val activity: Activity) : CameraScanner {
    override fun launch(onResult: (Result<ScanEvent>) -> Unit) {
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_ALL_FORMATS)
            .enableAutoZoom()
            .build()
        GmsBarcodeScanning.getClient(activity, options).startScan()
            .addOnSuccessListener { barcode ->
                val value = barcode.rawValue.orEmpty()
                if (value.isBlank()) onResult(Result.failure(IllegalStateException("No se detectó un código.")))
                else onResult(Result.success(ScanEvent(value, "Cámara")))
            }
            .addOnCanceledListener { onResult(Result.failure(CancellationException("Escaneo cancelado."))) }
            .addOnFailureListener { onResult(Result.failure(it)) }
    }
}

class CancellationException(message: String) : RuntimeException(message)
