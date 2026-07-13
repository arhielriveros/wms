package com.wms.mobile.data.local

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

@Database(
    entities = [TaskEntity::class, OfflineCommandEntity::class],
    version = 1,
    exportSchema = true,
)
abstract class WmsDatabase : RoomDatabase() {
    abstract fun taskDao(): TaskDao
    abstract fun commandDao(): OfflineCommandDao

    companion object {
        fun create(context: Context): WmsDatabase =
            Room.databaseBuilder(context, WmsDatabase::class.java, "wms-mobile.db")
                .fallbackToDestructiveMigrationOnDowngrade()
                .build()
    }
}
