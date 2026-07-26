package com.audiobridge.app.network

import java.nio.ByteBuffer
import java.nio.ByteOrder

data class PacketHeader(
    val sequence: Long,
    val timestampNtp: Long,
    val flags: Int,
    val payloadLen: Int
) {
    companion object {
        fun parse(data: ByteArray, offset: Int = 0): PacketHeader? {
            if (data.size - offset < ProtocolConstants.HEADER_SIZE) return null
            val buf = ByteBuffer.wrap(data, offset, ProtocolConstants.HEADER_SIZE)
                .order(ByteOrder.LITTLE_ENDIAN)
            val magic = buf.getShort().toInt() and 0xFFFF
            if (magic != ProtocolConstants.MAGIC.toInt()) return null
            val seq = buf.getInt().toLong() and 0xFFFF_FFFFL
            val ts = buf.getLong()
            val ntpNowMs = System.currentTimeMillis() + 2208988800000L
            if (kotlin.math.abs(ts - ntpNowMs) > 31536000000L) return null
            val flags = buf.get().toInt() and 0xFF
            buf.get()
            val payloadLen = buf.getShort().toInt() and 0xFFFF
            if (payloadLen > ProtocolConstants.MAX_PAYLOAD_SIZE || payloadLen > data.size - offset - ProtocolConstants.HEADER_SIZE) return null
            return PacketHeader(seq, ts, flags, payloadLen)
        }
    }
}
