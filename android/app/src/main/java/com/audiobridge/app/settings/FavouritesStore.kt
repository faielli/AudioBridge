package com.audiobridge.app.settings

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import org.json.JSONArray
import org.json.JSONObject

private val Context.favStore by preferencesDataStore(name = "audio_bridge_favourites")

data class FavouriteDevice(
    val name: String,
    val host: String
)

class FavouritesStore(private val context: Context) {

    companion object {
        private val FAV_KEY = stringPreferencesKey("favourites")
    }

    val favourites: Flow<List<FavouriteDevice>> = context.favStore.data.map { prefs ->
        val json = prefs[FAV_KEY] ?: return@map emptyList()
        parseList(json)
    }

    suspend fun add(name: String, host: String): Boolean {
        var added = false
        context.favStore.edit { prefs ->
            val existing = prefs[FAV_KEY]?.let { parseList(it) } ?: emptyList()
            if (existing.none { it.host == host }) {
                val list = existing.toMutableList()
                list.add(FavouriteDevice(name, host))
                prefs[FAV_KEY] = serializeList(list)
                added = true
            }
        }
        return added
    }

    suspend fun remove(host: String) {
        context.favStore.edit { prefs ->
            val existing = prefs[FAV_KEY]?.let { parseList(it) } ?: return@edit
            val updated = existing.filter { it.host != host }
            prefs[FAV_KEY] = serializeList(updated)
        }
    }

    private fun parseList(json: String): List<FavouriteDevice> {
        val arr = JSONArray(json)
        return (0 until arr.length()).map { i ->
            val obj = arr.getJSONObject(i)
            FavouriteDevice(
                name = obj.getString("name"),
                host = obj.getString("host")
            )
        }
    }

    private fun serializeList(list: List<FavouriteDevice>): String {
        return JSONArray(list.map { fav ->
            JSONObject().apply {
                put("name", fav.name)
                put("host", fav.host)
            }
        }).toString()
    }
}
