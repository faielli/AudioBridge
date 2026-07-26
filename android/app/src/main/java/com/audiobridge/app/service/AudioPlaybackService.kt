package com.audiobridge.app.service

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.os.IBinder
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.media.app.NotificationCompat.MediaStyle
import android.support.v4.media.session.MediaSessionCompat
import android.support.v4.media.session.PlaybackStateCompat
import com.audiobridge.app.network.AudioParameters
import com.audiobridge.app.network.TcpControlClient
import com.audiobridge.app.stream.StreamSession
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

enum class ConnectionState {
    DISCONNECTED,
    CONNECTING,
    CONNECTED,
    RECONNECTING
}

class AudioPlaybackService : Service() {
    companion object {
        private const val TAG = "AudioPlaybackService"
        const val CHANNEL_ID = "audiobridge_playback"
        const val NOTIFICATION_ID = 1001
        const val EXTRA_HOST = "host"
        const val ACTION_PAUSE = "com.audiobridge.app.action.PAUSE"
        const val ACTION_STOP = "com.audiobridge.app.action.STOP"

        private val _connectionState = MutableStateFlow(ConnectionState.DISCONNECTED)
        val connectionState: StateFlow<ConnectionState> = _connectionState.asStateFlow()
    }

    private var tcpClient: TcpControlClient? = null
    private var session: StreamSession? = null
    private var mediaSession: MediaSessionCompat? = null
    private var sessionParams: AudioParameters? = null

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        createMediaSession()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        when (intent?.action) {
            ACTION_PAUSE -> {
                onPause()
                return START_STICKY
            }
            ACTION_STOP -> {
                onStop()
                return START_NOT_STICKY
            }
        }

        startForeground(NOTIFICATION_ID, buildNotification("Connessione in corso…", false))

        val host = intent?.getStringExtra(EXTRA_HOST)
        if (host.isNullOrBlank()) {
            _connectionState.value = ConnectionState.DISCONNECTED
            stopSelf()
            return START_NOT_STICKY
        }

        closeSession()
        cleanup()

        _connectionState.value = ConnectionState.CONNECTING

        val client = TcpControlClient(host)
        tcpClient = client

        client.onConnected = { params ->
            Log.d(TAG, "TCP connesso, avvio stream UDP (${params.sampleRate}Hz/${params.channels}ch/${params.bitrate}bps)")

            sessionParams = params
            _connectionState.value = ConnectionState.CONNECTED
            updateNotification("Streaming da $host")

            closeSession()

            try {
                Log.d(TAG, "Creazione StreamSession...")
                val newSession = StreamSession(params)
                Log.d(TAG, "Avvio StreamSession...")
                newSession.start()
                session = newSession
                setPlaybackState(true)
                Log.d(TAG, "StreamSession avviato con successo")
            } catch (e: Exception) {
                Log.e(TAG, "ERRORE avvio stream: ${e.message}", e)
                _connectionState.value = ConnectionState.DISCONNECTED
                updateNotification("Errore: ${e.message}")
            }
        }

        client.onDisconnected = {
            Log.d(TAG, "TCP disconnesso")
            _connectionState.value = ConnectionState.RECONNECTING
            closeSession()
            setPlaybackState(false)
            updateNotification("Riconnessione in corso…")
        }

        client.onError = { msg ->
            Log.e(TAG, "TCP error: $msg")
            _connectionState.value = ConnectionState.DISCONNECTED
            updateNotification("Errore: $msg")
            closeSession()
        }

        client.connect()
        return START_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        mediaSession?.release()
        _connectionState.value = ConnectionState.DISCONNECTED
        closeSession()
        tcpClient?.close()
        tcpClient = null
        super.onDestroy()
    }

    private fun onPlay() {
        Log.d(TAG, "MediaSession onPlay")
        if (session != null) {
            session?.start()
            setPlaybackState(true)
            updateNotification("Streaming in corso…")
        } else if (sessionParams != null && tcpClient != null) {
            try {
                val newSession = StreamSession(sessionParams!!)
                newSession.start()
                session = newSession
                setPlaybackState(true)
                updateNotification("Streaming in corso…")
            } catch (e: Exception) {
                Log.e(TAG, "ERRORE riavvio stream: ${e.message}", e)
                updateNotification("Errore: ${e.message}")
            }
        }
    }

    private fun onPause() {
        Log.d(TAG, "MediaSession onPause")
        session?.stop()
        setPlaybackState(false)
        updateNotification("In pausa")
    }

    private fun onStop() {
        Log.d(TAG, "MediaSession onStop")
        _connectionState.value = ConnectionState.DISCONNECTED
        closeSession()
        tcpClient?.close()
        tcpClient = null
        stopForeground(STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    private fun createMediaSession() {
        mediaSession = MediaSessionCompat(this, "AudioBridge").apply {
            setCallback(object : MediaSessionCompat.Callback() {
                override fun onPlay() = this@AudioPlaybackService.onPlay()
                override fun onPause() = this@AudioPlaybackService.onPause()
                override fun onStop() = this@AudioPlaybackService.onStop()
            })
            setFlags(MediaSessionCompat.FLAG_HANDLES_MEDIA_BUTTONS or MediaSessionCompat.FLAG_HANDLES_TRANSPORT_CONTROLS)
            isActive = true
        }
        setPlaybackState(false)
    }

    private fun setPlaybackState(playing: Boolean) {
        val state = if (playing) PlaybackStateCompat.STATE_PLAYING else PlaybackStateCompat.STATE_PAUSED
        val actions = PlaybackStateCompat.ACTION_PLAY or
                PlaybackStateCompat.ACTION_PAUSE or
                PlaybackStateCompat.ACTION_STOP
        val builder = PlaybackStateCompat.Builder()
            .setActions(actions)
            .setState(state, PlaybackStateCompat.PLAYBACK_POSITION_UNKNOWN, 1f)
        mediaSession?.setPlaybackState(builder.build())
    }

    private fun closeSession() {
        session?.close()
        session = null
    }

    private fun cleanup() {
        tcpClient?.close()
        tcpClient = null
    }

    private fun updateNotification(text: String) {
        val isPlaying = session?.isRunning == true
        val manager = getSystemService(NotificationManager::class.java)
        manager.notify(NOTIFICATION_ID, buildNotification(text, isPlaying))
    }

    private fun createNotificationChannel() {
        val channel = NotificationChannel(
            CHANNEL_ID,
            "AudioBridge Playback",
            NotificationManager.IMPORTANCE_LOW
        ).apply {
            description = "Notifica streaming AudioBridge"
            setShowBadge(false)
        }
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(channel)
    }

    private fun buildNotification(text: String, isPlaying: Boolean): Notification {
        val pauseIntent = PendingIntent.getService(
            this, 0,
            Intent(this, AudioPlaybackService::class.java).setAction(ACTION_PAUSE),
            PendingIntent.FLAG_IMMUTABLE
        )
        val stopIntent = PendingIntent.getService(
            this, 1,
            Intent(this, AudioPlaybackService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_IMMUTABLE
        )

        val playPauseIcon = if (isPlaying) android.R.drawable.ic_media_pause
            else android.R.drawable.ic_media_play
        val playPauseLabel = if (isPlaying) "Pausa" else "Riproduci"

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("AudioBridge")
            .setContentText(text)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setOngoing(true)
            .setSilent(true)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .setStyle(MediaStyle()
                .setMediaSession(mediaSession?.sessionToken)
                .setShowActionsInCompactView(0, 1))
            .addAction(playPauseIcon, playPauseLabel, pauseIntent)
            .addAction(android.R.drawable.ic_menu_close_clear_cancel, "Ferma", stopIntent)
            .build()
    }
}
