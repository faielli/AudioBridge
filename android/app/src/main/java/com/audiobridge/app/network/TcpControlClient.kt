package com.audiobridge.app.network

import android.os.Build
import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.InetSocketAddress
import java.net.Socket
import java.net.SocketTimeoutException
import java.util.UUID

data class AudioParameters(
    val sampleRate: Int,
    val channels: Int,
    val bitrate: Int,
    val frameSizeMs: Int,
    val udpPort: Int,
    val sessionId: String,
    val serverIp: String
)

class TcpControlClient(
    private val host: String,
    private val port: Int = ProtocolConstants.DEFAULT_CONTROL_PORT
) : AutoCloseable {
    companion object {
        private const val TAG = "TcpControlClient"
    }

    private var socket: Socket? = null
    private var reader: BufferedReader? = null
    private var writer: OutputStreamWriter? = null

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    var parameters: AudioParameters? = null
        private set
    var isConnected: Boolean = false
        private set

    var onConnected: ((AudioParameters) -> Unit)? = null
    var onDisconnected: (() -> Unit)? = null
    var onError: ((String) -> Unit)? = null

    private var keepAliveJob: Job? = null
    private var readJob: Job? = null
    private var reconnectJob: Job? = null
    private var lastPongTime = 0L
    private var reconnectAttempt = 0

    @Volatile
    private var notifiedDisconnect = false

    fun connect() {
        notifiedDisconnect = false
        disconnect()
        scope.launch { connectInternal() }
    }

    private suspend fun connectInternal() {
        try {
            Log.d(TAG, "Connecting to $host:$port...")
            cleanupSocket()

            val sock = Socket()
            sock.connect(InetSocketAddress(host, port), 5000)
            sock.soTimeout = 4000
            socket = sock
            reader = BufferedReader(InputStreamReader(sock.getInputStream(), Charsets.UTF_8))
            writer = OutputStreamWriter(sock.getOutputStream(), Charsets.UTF_8)

            // Send HELLO
            val hello = buildHello()
            writeLine(hello)
            Log.d(TAG, "HELLO sent: $hello")

            // Read WELCOME
            val welcomeLine = readLine(maxLength = 4096)
                ?: throw Exception("Connection closed before WELCOME")
            Log.d(TAG, "WELCOME: $welcomeLine")

            val params = parseWelcome(welcomeLine, host)
            parameters = params
            isConnected = true
            reconnectAttempt = 0

            onConnected?.invoke(params)

            startKeepAlive()
        } catch (e: SocketTimeoutException) {
            Log.e(TAG, "Connection timeout to $host:$port")
            onError?.invoke("Connection timeout")
            scheduleReconnect()
        } catch (e: Exception) {
            Log.e(TAG, "Connection failed: ${e.message}")
            onError?.invoke(e.message ?: "Connection failed")
            scheduleReconnect()
        }
    }

    private fun buildHello(): String {
        val deviceName = if (Build.MODEL.isNotBlank()) Build.MODEL else "Android"
        val clientId = "android-${UUID.randomUUID()}"
        return JSONObject().apply {
            put("type", "HELLO")
            put("version", 1)
            put("client_name", deviceName)
            put("client_id", clientId)
            put("capabilities", JSONObject().apply {
                put("opus", true)
                put("max_bitrate", 320000)
                put("sample_rates", org.json.JSONArray(listOf(44100, 48000)))
                put("channels", org.json.JSONArray(listOf(1, 2)))
                put("frame_sizes_ms", org.json.JSONArray(listOf(5, 10, 20, 40, 60)))
            })
        }.toString()
    }

    private fun readLine(maxLength: Int = 4096): String? {
        val sb = StringBuilder(maxLength)
        while (sb.length < maxLength) {
            val c = reader?.read() ?: return null
            if (c == -1) return null
            if (c == '\n'.code) return sb.toString()
            sb.append(c.toChar())
        }
        throw java.io.IOException("Line exceeded max length $maxLength")
    }

    private fun parseWelcome(json: String, serverHost: String): AudioParameters {
        val root = JSONObject(json)
        val neg = root.getJSONObject("negotiated")
        val sampleRate = neg.getInt("sample_rate")
        val channels = neg.getInt("channels")
        val bitrate = neg.getInt("bitrate")
        val frameSizeMs = neg.getInt("frame_size_ms")
        val udpPort = neg.getInt("udp_port")
        val sessionId = root.getString("session_id")
        require(sampleRate in 8000..96000) { "Invalid sampleRate: $sampleRate" }
        require(channels in 1..2) { "Invalid channels: $channels" }
        require(udpPort in 1..65535) { "Invalid udpPort: $udpPort" }
        return AudioParameters(
            sampleRate = sampleRate,
            channels = channels,
            bitrate = bitrate,
            frameSizeMs = frameSizeMs,
            udpPort = udpPort,
            sessionId = sessionId,
            serverIp = serverHost
        )
    }

    private fun startKeepAlive() {
        lastPongTime = System.currentTimeMillis()
        keepAliveJob = scope.launch {
            while (isActive && isConnected) {
                delay(ProtocolConstants.KEEP_ALIVE_INTERVAL_MS)
                if (!isConnected) break

                val elapsed = System.currentTimeMillis() - lastPongTime
                if (elapsed >= ProtocolConstants.KEEP_ALIVE_TIMEOUT_MS) {
                    Log.w(TAG, "Keep-alive timeout (${elapsed}ms since last PONG)")
                    notifyDisconnected()
                    disconnectInternal()
                    scheduleReconnect()
                    break
                }

                val ping = JSONObject().apply {
                    put("type", "PING")
                    put("ts", System.currentTimeMillis())
                }.toString()
                try {
                    writeLine(ping)
                } catch (e: Exception) {
                    Log.w(TAG, "PING write failed: ${e.message}")
                    break
                }
            }
        }

        readJob = scope.launch {
            try {
                while (isActive && isConnected && socket != null) {
                    val line = readLine(maxLength = 4096) ?: break
                    handleMessage(line)
                }
            } catch (e: SocketTimeoutException) {
                Log.w(TAG, "Read timeout")
            } catch (e: Exception) {
                Log.w(TAG, "Read error: ${e.message}")
            } finally {
                if (isConnected) {
                    isConnected = false
                    notifyDisconnected()
                    scheduleReconnect()
                }
            }
        }
    }

    private val ALLOWED_TYPES = setOf("WELCOME", "PING", "PONG", "STREAM_START", "STREAM_STOP", "ERROR")

    private fun handleMessage(line: String) {
        try {
            val root = JSONObject(line)
            val type = root.getString("type")
            if (type !in ALLOWED_TYPES) {
                Log.w(TAG, "Unknown message type: $type")
                return
            }
            when (type) {
                "PING" -> {
                    val ts = root.getLong("ts")
                    val pong = JSONObject().apply {
                        put("type", "PONG")
                        put("ts", ts)
                    }.toString()
                    writeLine(pong)
                }
                "PONG" -> {
                    lastPongTime = System.currentTimeMillis()
                }
                "STREAM_START" -> {
                    Log.d(TAG, "UDP stream started by server")
                }
                "STREAM_STOP" -> {
                    Log.d(TAG, "UDP stream stopped by server")
                }
                "ERROR" -> {
                    val msg = root.optString("message", "Unknown error")
                    Log.e(TAG, "Server error: $msg")
                    onError?.invoke(msg)
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Failed to parse: $line - ${e.message}")
        }
    }

    private fun notifyDisconnected() {
        if (notifiedDisconnect) return
        notifiedDisconnect = true
        scope.launch { onDisconnected?.invoke() }
    }

    private fun scheduleReconnect() {
        reconnectJob?.cancel()
        reconnectJob = scope.launch {
            val delays = listOf(2000L, 5000L, 10000L)
            while (isActive) {
                val delayMs = delays.getOrElse(reconnectAttempt) { 10000L }
                reconnectAttempt++
                Log.d(TAG, "Reconnecting in ${delayMs}ms (attempt $reconnectAttempt)")
                delay(delayMs)
                if (!isActive) break
                connectInternal()
                if (isConnected) {
                    reconnectAttempt = 0
                    break
                }
            }
        }
    }

    @Synchronized
    private fun writeLine(json: String) {
        val w = writer ?: throw java.io.IOException("Writer not initialized")
        w.write(json)
        w.write('\n'.code)
        w.flush()
    }

    private fun disconnectInternal() {
        isConnected = false
        keepAliveJob?.cancel()
        readJob?.cancel()
        cleanupSocket()
    }

    private fun cleanupSocket() {
        try { socket?.close() } catch (_: Exception) {}
        socket = null
        reader = null
        writer = null
    }

    fun disconnect() {
        reconnectJob?.cancel()
        disconnectInternal()
    }

    override fun close() {
        disconnect()
        scope.cancel()
    }
}
