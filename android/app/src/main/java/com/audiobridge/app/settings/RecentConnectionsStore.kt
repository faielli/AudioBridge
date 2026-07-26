package com.audiobridge.app.settings

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import org.json.JSONArray
import org.json.JSONObject

private val Context.recentStore by preferencesDataStore(name = "audio_bridge_recent")

data class RecentConnection(
    val name: String,
    val host: String,
    val timestamp: Long
)

class RecentConnectionsStore(private val context: Context) {

    companion object {
        private val RECENT_KEY = stringPreferencesKey("recent_connections")
        private const val MAX_ENTRIES = 5
    }

    val connections: Flow<List<RecentConnection>> = context.recentStore.data.map { prefs ->
        val json = prefs[RECENT_KEY] ?: return@map emptyList()
        parseList(json)
    }

    suspend fun add(name: String, host: String) {
        context.recentStore.edit { prefs ->
            val existing = prefs[RECENT_KEY]?.let { parseList(it) } ?: emptyList()
            val updated = existing.filter { it.host != host }.toMutableList()
            updated.add(0, RecentConnection(name, host, System.currentTimeMillis()))
            while (updated.size > MAX_ENTRIES) updated.removeAt(updated.lastIndex)
            prefs[RECENT_KEY] = serializeList(updated)
        }
    }

    suspend fun remove(host: String) {
        context.recentStore.edit { prefs ->
            val existing = prefs[RECENT_KEY]?.let { parseList(it) } ?: return@edit
            val updated = existing.filter { it.host != host }
            prefs[RECENT_KEY] = serializeList(updated)
        }
    }

    private fun parseList(json: String): List<RecentConnection> {
        val arr = JSONArray(json)
        return (0 until arr.length()).map { i ->
            val obj = arr.getJSONObject(i)
            RecentConnection(
                name = obj.getString("name"),
                host = obj.getString("host"),
                timestamp = obj.getLong("ts")
            )
        }
    }

    private fun serializeList(list: List<RecentConnection>): String {
        return JSONArray(list.map { conn ->
            JSONObject().apply {
                put("name", conn.name)
                put("host", conn.host)
                put("ts", conn.timestamp)
            }
        }).toString()
    }
}
