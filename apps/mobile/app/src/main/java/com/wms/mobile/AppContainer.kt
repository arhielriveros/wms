package com.wms.mobile

import android.content.Context
import android.provider.Settings
import com.wms.mobile.data.local.WmsDatabase
import com.wms.mobile.data.network.MobileApiFactory
import com.wms.mobile.data.network.OkHttpMobileApi
import com.wms.mobile.data.repository.DefaultTaskRepository
import com.wms.mobile.data.repository.TaskRepository
import com.wms.mobile.data.session.SessionStore
import com.wms.mobile.platform.NetworkMonitor
import com.wms.mobile.scanner.DataWedgeScanner
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit

class AppContainer(context: Context) {
    private val appContext = context.applicationContext
    private val database = WmsDatabase.create(appContext)
    val session = SessionStore(appContext)
    val networkMonitor = NetworkMonitor(appContext)
    val dataWedgeScanner = DataWedgeScanner(appContext)

    private val json = Json {
        ignoreUnknownKeys = true
        explicitNulls = false
        encodeDefaults = true
    }
    private val http = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(20, TimeUnit.SECONDS)
        .writeTimeout(20, TimeUnit.SECONDS)
        .retryOnConnectionFailure(true)
        .build()
    private val apiFactory = MobileApiFactory { OkHttpMobileApi(http, json, session) }
    private val deviceId = Settings.Secure.getString(appContext.contentResolver, Settings.Secure.ANDROID_ID)
        ?: "unavailable-device-id"

    val repository: TaskRepository = DefaultTaskRepository(
        taskDao = database.taskDao(),
        commandDao = database.commandDao(),
        apiFactory = apiFactory,
        session = session,
        deviceId = deviceId,
        json = json,
    )
}
