package com.audiobridge.app.network

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import kotlinx.coroutines.isActive
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.SocketTimeoutException

data class AudioPacket(
    val header: PacketHeader,
    val opusData: ByteArray
)

class UdpAudioReceiver(
    private val port: Int = ProtocolConstants.DEFAULT_DATA_PORT,
    private val expectedServerIp: java.net.InetAddress? = null
) {

    companion object {
        private const val TAG = "UdpAudioReceiver"
    }

    @Volatile
    private var socket: DatagramSocket? = null

    fun start(): Flow<AudioPacket> = flow {
        val sock: DatagramSocket
        try {
            sock = DatagramSocket(port).also {
                it.soTimeout = 1000
                it.receiveBufferSize = 1024 * 1024
            }
            socket = sock
            Log.d(TAG, "Socket bound to port $port")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to bind port $port: ${e.message}")
            socket = null
            throw e
        }
        try {
            val bufSize = ProtocolConstants.HEADER_SIZE + ProtocolConstants.MAX_PAYLOAD_SIZE
            val buf = ByteArray(bufSize)
            while (currentCoroutineContext().isActive) {
                try {
                    val packet = DatagramPacket(buf, buf.size)
                    sock.receive(packet)
                    if (expectedServerIp != null && packet.address != expectedServerIp) continue
                    if (packet.length < ProtocolConstants.HEADER_SIZE) continue
                    val header = PacketHeader.parse(buf) ?: continue
                    val len = minOf(header.payloadLen, packet.length - ProtocolConstants.HEADER_SIZE)
                    if (len <= 0) continue
                    val opusData = buf.copyOfRange(
                        ProtocolConstants.HEADER_SIZE,
                        ProtocolConstants.HEADER_SIZE + len
                    )
                    emit(AudioPacket(header, opusData))
                } catch (_: SocketTimeoutException) {
                }
            }
        } finally {
            sock.close()
            socket = null
            Log.d(TAG, "Socket closed")
        }
    }.flowOn(Dispatchers.IO)

    fun close() {
        socket?.close()
        socket = null
    }
}
