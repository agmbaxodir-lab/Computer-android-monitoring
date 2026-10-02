package com.filemonitoring.agent.db

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface EventDao {
    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insert(event: EventEntity): Long

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertAll(events: List<EventEntity>): List<Long>

    @Query("SELECT * FROM local_events_queue ORDER BY createdAt ASC LIMIT :limit")
    suspend fun getPendingEvents(limit: Int): List<EventEntity>

    @Query("DELETE FROM local_events_queue WHERE eventId IN (:eventIds)")
    suspend fun deleteEvents(eventIds: List<String>)

    @Query("SELECT COUNT(*) FROM local_events_queue")
    suspend fun getCount(): Int

    @Query("UPDATE local_events_queue SET retryCount = retryCount + 1 WHERE eventId IN (:eventIds)")
    suspend fun incrementRetryCount(eventIds: List<String>)
}
