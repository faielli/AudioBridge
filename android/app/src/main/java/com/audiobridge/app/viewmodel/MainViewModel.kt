package com.audiobridge.app.viewmodel

import android.app.Application
import android.content.Intent
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.audiobridge.app.network.DiscoveredService
import com.audiobridge.app.network.NsdDiscovery
import com.audiobridge.app.service.AudioPlaybackService
import com.audiobridge.app.service.ConnectionState
import com.audiobridge.app.settings.FavouriteDevice
import com.audiobridge.app.settings.FavouritesStore
import com.audiobridge.app.settings.RecentConnection
import com.audiobridge.app.settings.RecentConnectionsStore
import com.audiobridge.app.settings.SettingsDataStore
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

class MainViewModel(application: Application) : AndroidViewModel(application) {
    var targetHost: String = ""

    val isStreaming: Boolean get() = _connectionState.value == ConnectionState.CONNECTED

    private val discovery = NsdDiscovery(application)
    private val settings = SettingsDataStore(application)
    private val recentStore = RecentConnectionsStore(application)
    private val favStore = FavouritesStore(application)
    private var discoveryJob: Job? = null

    private val _discoveredServices = MutableStateFlow<List<DiscoveredService>>(emptyList())
    val discoveredServices: StateFlow<List<DiscoveredService>> = _discoveredServices.asStateFlow()

    private val _connectionState = MutableStateFlow(ConnectionState.DISCONNECTED)
    val connectionState: StateFlow<ConnectionState> = _connectionState.asStateFlow()

    private val _connectionStatusText = MutableStateFlow("")
    val connectionStatusText: StateFlow<String> = _connectionStatusText.asStateFlow()

    private val _clientName = MutableStateFlow("")
    val clientName: StateFlow<String> = _clientName.asStateFlow()

    private val _audioLevel = MutableStateFlow(0f)
    val audioLevel: StateFlow<Float> = _audioLevel.asStateFlow()

    private val _isScanning = MutableStateFlow(false)
    val isScanning: StateFlow<Boolean> = _isScanning.asStateFlow()

    private val _recentConnections = MutableStateFlow<List<RecentConnection>>(emptyList())
    val recentConnections: StateFlow<List<RecentConnection>> = _recentConnections.asStateFlow()

    private val _favourites = MutableStateFlow<List<FavouriteDevice>>(emptyList())
    val favourites: StateFlow<List<FavouriteDevice>> = _favourites.asStateFlow()

    private val _snackbarMessage = MutableStateFlow<String?>(null)
    val snackbarMessage: StateFlow<String?> = _snackbarMessage.asStateFlow()

    private val _pendingFavourite = MutableStateFlow<Pair<String, String>?>(null)
    val pendingFavourite: StateFlow<Pair<String, String>?> = _pendingFavourite.asStateFlow()

    val jitterBufferSize: StateFlow<Int> = settings.jitterBufferSize
        .stateIn(viewModelScope, SharingStarted.Eagerly, 5)

    val keepBackground: StateFlow<Boolean> = settings.keepBackground
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    init {
        viewModelScope.launch {
            recentStore.connections.collect { list ->
                _recentConnections.value = list
            }
        }
        viewModelScope.launch {
            favStore.favourites.collect { list ->
                _favourites.value = list
            }
        }
        viewModelScope.launch {
            AudioPlaybackService.connectionState.collect { state ->
                _connectionState.value = state
                if (state == ConnectionState.CONNECTED && targetHost.isNotBlank()) {
                    recentStore.add(name = _clientName.value, host = targetHost)
                    val name = _clientName.value
                    val host = targetHost
                    val isFav = _favourites.value.any { it.host == host }
                    if (!isFav) {
                        _pendingFavourite.value = name to host
                        _snackbarMessage.value = "Vuoi salvare $name nei preferiti?"
                    }
                }
            }
        }
    }

    fun startDiscovery() {
        discoveryJob?.cancel()
        _isScanning.value = true
        discoveryJob = viewModelScope.launch {
            discovery.discover().collect { services ->
                _discoveredServices.value = services
                _isScanning.value = false
            }
        }
    }

    fun selectService(svc: DiscoveredService) {
        targetHost = svc.host
        _clientName.value = svc.name
        _connectionStatusText.value = "Connessione a ${svc.name}..."
        connectToHost(targetHost)
    }

    fun connectToIp(ip: String) {
        targetHost = ip
        _clientName.value = ip
        _connectionStatusText.value = "Connessione a $ip..."
        connectToHost(ip)
    }

    fun connectToRecent(conn: RecentConnection) {
        targetHost = conn.host
        _clientName.value = conn.name
        _connectionStatusText.value = "Connessione a ${conn.name}..."
        connectToHost(conn.host)
    }

    fun removeRecent(host: String) {
        viewModelScope.launch {
            recentStore.remove(host)
        }
    }

    fun connectToFavourite(fav: FavouriteDevice) {
        targetHost = fav.host
        _clientName.value = fav.name
        _connectionStatusText.value = "Connessione a ${fav.name}..."
        connectToHost(fav.host)
    }

    fun addFavourite(name: String, host: String) {
        viewModelScope.launch {
            val added = favStore.add(name, host)
            _snackbarMessage.value = if (added) "$name aggiunto ai preferiti"
                else "IP già nei preferiti"
        }
    }

    fun removeFavourite(host: String) {
        viewModelScope.launch {
            favStore.remove(host)
        }
    }

    fun savePendingFavourite() {
        _pendingFavourite.value?.let { (name, host) ->
            viewModelScope.launch {
                favStore.add(name, host)
            }
        }
        _pendingFavourite.value = null
        _snackbarMessage.value = null
    }

    fun dismissPendingFavourite() {
        _pendingFavourite.value = null
        _snackbarMessage.value = null
    }

    fun clearSnackbar() {
        _snackbarMessage.value = null
    }

    private fun connectToHost(host: String) {
        val context = getApplication<Application>()
        val intent = Intent(context, AudioPlaybackService::class.java).apply {
            putExtra(AudioPlaybackService.EXTRA_HOST, host)
        }
        context.startForegroundService(intent)
    }

    fun startStream() {
        if (targetHost.isBlank()) return
        connectToHost(targetHost)
    }

    fun stopStream() {
        getApplication<Application>().stopService(
            Intent(getApplication(), AudioPlaybackService::class.java)
        )
    }

    fun stop() {
        getApplication<Application>().stopService(
            Intent(getApplication(), AudioPlaybackService::class.java)
        )
    }

    fun setJitterBufferSize(frames: Int) {
        viewModelScope.launch {
            settings.setJitterBufferSize(frames)
        }
    }

    fun setKeepBackground(enabled: Boolean) {
        viewModelScope.launch {
            settings.setKeepBackground(enabled)
        }
    }

    override fun onCleared() {
        stop()
        discovery.stop()
        super.onCleared()
    }
}
