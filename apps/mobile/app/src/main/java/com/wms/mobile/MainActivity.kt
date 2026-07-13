package com.wms.mobile

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import com.wms.mobile.scanner.GoogleCodeCameraScanner
import com.wms.mobile.sync.SyncScheduler
import com.wms.mobile.ui.MainViewModel
import com.wms.mobile.ui.WmsApp
import com.wms.mobile.ui.theme.WmsTheme

class MainActivity : ComponentActivity() {
    private val container get() = (application as WmsApplication).container
    private val viewModel: MainViewModel by viewModels {
        MainViewModel.Factory(
            repository = container.repository,
            session = container.session,
            networkOnline = container.networkMonitor.isOnline,
            scanner = container.dataWedgeScanner,
            scheduleSync = { SyncScheduler.enqueue(this) },
        )
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val camera = GoogleCodeCameraScanner(this)
        setContent { WmsTheme { WmsApp(viewModel, camera) } }
    }

    override fun onStart() {
        super.onStart()
        container.dataWedgeScanner.start()
    }

    override fun onStop() {
        container.dataWedgeScanner.stop()
        super.onStop()
    }
}
