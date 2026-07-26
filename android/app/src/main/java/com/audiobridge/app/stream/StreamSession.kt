package com.audiobridge.app.stream

import android.os.Process
import android.util.Log
import com.audiobridge.app.audio.OboePlayer
import com.audiobridge.app.audio.OpusDecoder
import com.audiobridge.app.network.AudioPacket
import com.audiobridge.app.network.AudioParameters
import com.audiobridge.app.network.ProtocolConstants
import com.audiobridge.app.network.UdpAudioReceiver
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.onCompletion
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.concurrent.atomic.AtomicLong

class StreamSession(private val params: AudioParameters) : AutoCloseable {
    companion object {
        private const val TAG = "StreamSession"
    }

    private var scope: CoroutineScope? = null
    private val oboePlayer = OboePlayer()
    private var receiver: UdpAudioReceiver? = null

    private var opusDecoder: OpusDecoder? = null

    private val frameSamples = params.sampleRate * params.frameSizeMs / 1000

    private val pcmQueue = Channel<ShortArray>(
        capacity = ProtocolConstants.JITTER_BUFFER_FRAMES,
        onBufferOverflow = BufferOverflow.SUSPEND
    )

    private val _packetsReceived = AtomicLong(0)
    private val _packetsDecoded = AtomicLong(0)
    private val _packetsDropped = AtomicLong(0)

    val packetsReceived: Long get() = _packetsReceived.get()
    val packetsDecoded: Long get() = _packetsDecoded.get()
    val packetsDropped: Long get() = _packetsDropped.get()

    private var _lastSeq: Long = -1
    @Volatile
    private var _pktCount: Int = 0
    private var _lostTotal: Long = 0
    private var _queueSize: Int = 0
    private var _running = false

    private var decodeJob: Job? = null
    private var playJob: Job? = null
    private var loggerJob: Job? = null
    private var _playerStarted = false

    val isRunning: Boolean get() = _running

    fun start() {
        if (_running) {
            Log.d(TAG, "start() — già in esecuzione, skip")
            return
        }
        _running = true
        Log.d(TAG, "Avvio sessione — sampleRate=${params.sampleRate} channels=${params.channels} frameSamples=$frameSamples")

        scope = CoroutineScope(Dispatchers.IO + SupervisorJob())
        val serverAddress = try { java.net.InetAddress.getByName(params.serverIp) } catch (_: Exception) { null }
        receiver = UdpAudioReceiver(port = params.udpPort, expectedServerIp = serverAddress)

        if (!_playerStarted) {
            oboePlayer.start(params.sampleRate, params.channels)
            _playerStarted = true
        }

        if (opusDecoder == null) {
            val oldDecoder = opusDecoder
            try {
                opusDecoder = OpusDecoder(params.sampleRate, params.channels)
                oldDecoder?.close()
                Log.d(TAG, "OpusDecoder creato: $opusDecoder")
            } catch (e: Exception) {
                opusDecoder = null
                Log.e(TAG, "ERRORE creazione OpusDecoder: ${e.message}", e)
            }
        }

        val s = scope!!
        val r = receiver!!

        loggerJob = s.launch {
            while (isActive) {
                kotlinx.coroutines.delay(1000)
                Log.d(TAG, "pkt/s=$_pktCount lost=$_lostTotal buffer=$_queueSize")
                _pktCount = 0
            }
        }

        decodeJob = s.launch {
            r.start()
                .catch { e ->
                    Log.e(TAG, "UDP error", e)
                }
                .onCompletion {
                    Log.d(TAG, "UDP flow ended")
                }
                .collect { packet -> handlePacket(packet) }
        }

        playJob = s.launch {
            Process.setThreadPriority(Process.THREAD_PRIORITY_AUDIO)
            val silence = ShortArray(frameSamples * params.channels)
            while (isActive) {
                val result = pcmQueue.receiveCatching()
                val frame = result.getOrNull()
                if (frame != null) {
                    _queueSize--
                    oboePlayer.write(frame)
                } else {
                    oboePlayer.write(silence)
                }
            }
        }
    }

    fun stop() {
        if (!_running) return
        _running = false
        receiver?.close()
        receiver = null
        scope?.cancel()
        scope = null
    }

    private suspend fun handlePacket(packet: AudioPacket) {
        _packetsReceived.incrementAndGet()
        _pktCount++

        val seq = packet.header.sequence
        if (_lastSeq != -1L && seq != (_lastSeq + 1) % 0x1_0000_0000L) {
            val gap = if (seq > _lastSeq) (seq - _lastSeq - 1) else (0x1_0000_0000L - _lastSeq + seq - 1)
            _lostTotal += gap
        }
        _lastSeq = seq

        val isPcmRaw = (packet.header.flags and ProtocolConstants.Flags.PCM_RAW) != 0
        Log.d(TAG, "Packet seq=$seq flags=${packet.header.flags} isPcmRaw=$isPcmRaw payloadSize=${packet.opusData.size}")

        if (isPcmRaw) {
            val pcmShorts = ShortArray(packet.opusData.size / 2)
            ByteBuffer.wrap(packet.opusData)
                .order(ByteOrder.LITTLE_ENDIAN)
                .asShortBuffer()
                .get(pcmShorts)
            _packetsDecoded.incrementAndGet()
            Log.d(TAG, "PCM passthrough — seq=$seq samples=${pcmShorts.size}")
            pcmQueue.send(pcmShorts)
            _queueSize++
        } else {
            val pcm = opusDecoder?.decode(packet.opusData, frameSamples)
            if (pcm != null) {
                _packetsDecoded.incrementAndGet()
                Log.d(TAG, "Opus decoded — seq=$seq outputSamples=${pcm.size}")
                pcmQueue.send(pcm)
                _queueSize++
            } else {
                Log.w(TAG, "Opus decode failed — seq=$seq decoder=$opusDecoder payloadSize=${packet.opusData.size}")
            }
        }
    }

    override fun close() {
        stop()
        pcmQueue.close()
        opusDecoder?.close()
        opusDecoder = null
        oboePlayer.close()
    }
}
