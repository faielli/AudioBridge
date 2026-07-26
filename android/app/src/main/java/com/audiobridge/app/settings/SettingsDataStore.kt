package com.audiobridge.app.settings

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.store by preferencesDataStore(name = "audio_bridge_settings")

class SettingsDataStore(private val context: Context) {

    companion object {
        private val JITTER_BUFFER_SIZE = intPreferencesKey("jitter_buffer_frames")
        private val KEEP_BACKGROUND = booleanPreferencesKey("keep_background_playback")
    }

    val jitterBufferSize: Flow<Int> = context.store.data.map { prefs ->
        prefs[JITTER_BUFFER_SIZE] ?: 5
    }

    val keepBackground: Flow<Boolean> = context.store.data.map { prefs ->
        prefs[KEEP_BACKGROUND] ?: false
    }

    suspend fun setJitterBufferSize(frames: Int) {
        context.store.edit { prefs ->
            prefs[JITTER_BUFFER_SIZE] = frames.coerceIn(1, 20)
        }
    }

    suspend fun setKeepBackground(enabled: Boolean) {
        context.store.edit { prefs ->
            prefs[KEEP_BACKGROUND] = enabled
        }
    }
}
