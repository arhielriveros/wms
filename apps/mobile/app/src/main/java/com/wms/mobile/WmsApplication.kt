package com.wms.mobile

import android.app.Application

class WmsApplication : Application() {
    val container: AppContainer by lazy { AppContainer(this) }
}
