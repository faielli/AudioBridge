package com.audiobridge.app.network

object ProtocolConstants {
    const val MAGIC: UShort = 0xCDABu
    const val HEADER_SIZE = 18
    const val MAX_PAYLOAD_SIZE = 1200

    const val DEFAULT_DATA_PORT = 54322
    const val DEFAULT_CONTROL_PORT = 54321

    // Parametri riproduzione audio (48kHz stereo)
    const val SAMPLE_RATE = 48000
    const val CHANNELS = 2
    const val FRAME_SIZE_MS = 20
    const val FRAME_SAMPLES = SAMPLE_RATE * FRAME_SIZE_MS / 1000

    // Jitter buffer: ~60ms = 3 frame
    const val JITTER_BUFFER_FRAMES = 3

    // Keep-alive (same as desktop)
    const val KEEP_ALIVE_INTERVAL_MS = 3000L
    const val KEEP_ALIVE_TIMEOUT_MS = 10000L

    // mDNS service discovery (Android NsdManager requires trailing dot)
    const val SERVICE_TYPE_NSD = "_audiobridge._tcp."

    object Flags {
        const val KEYFRAME = 0x01
        const val SILENCE = 0x02
        const val CONFIG_CHANGE = 0x04
        const val PCM_RAW = 0x08
    }
}
