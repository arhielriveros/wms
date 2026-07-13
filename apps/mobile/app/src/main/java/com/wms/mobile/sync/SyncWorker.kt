package com.wms.mobile.sync

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.wms.mobile.WmsApplication
import com.wms.mobile.data.network.AuthenticationRequiredException

class SyncWorker(appContext: Context, params: WorkerParameters) : CoroutineWorker(appContext, params) {
    override suspend fun doWork(): Result {
        val repository = (applicationContext as WmsApplication).container.repository
        return try {
            val summary = repository.syncPending()
            if (summary.requiresAuthentication) Result.failure() else Result.success()
        } catch (_: AuthenticationRequiredException) {
            Result.failure()
        } catch (_: Exception) {
            Result.retry()
        }
    }
}

object SyncScheduler {
    private const val UNIQUE_NAME = "mobile-command-sync"

    fun enqueue(context: Context) {
        val request = OneTimeWorkRequestBuilder<SyncWorker>()
            .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).build())
            .build()
        WorkManager.getInstance(context)
            .enqueueUniqueWork(UNIQUE_NAME, ExistingWorkPolicy.APPEND_OR_REPLACE, request)
    }
}
