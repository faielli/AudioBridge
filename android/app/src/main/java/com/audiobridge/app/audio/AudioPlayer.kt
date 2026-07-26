package com.audiobridge.app.audio

import com.audiobridge.app.network.ProtocolConstants

class AudioPlayer : AutoCloseable {
    private var oboe = OboePlayer()
    var underrunCount: Int = 0

    fun start(sampleRate: Int = ProtocolConstants.SAMPLE_RATE, channels: Int = ProtocolConstants.CHANNELS) {
        oboe.close()
        oboe = OboePlayer()
        oboe.start(sampleRate, channels)
    }

    fun write(pcm: ShortArray) {
        oboe.write(pcm)
    }

    override fun close() {
        oboe.close()
    }
}
