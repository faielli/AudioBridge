package com.audiobridge.app.audio

class OboePlayer : AutoCloseable {
    private var nativeHandle: Long = 0

    fun start(sampleRate: Int, channels: Int) {
        nativeHandle = nativeCreate(sampleRate, channels)
        if (nativeHandle == 0L)
            throw RuntimeException("Failed to create Oboe player")
        nativeStart(nativeHandle)
    }

    fun write(pcm: ShortArray, offset: Int = 0, length: Int = pcm.size): Int {
        if (nativeHandle == 0L) return -1
        return nativeWrite(nativeHandle, pcm, offset, length)
    }

    override fun close() {
        if (nativeHandle != 0L) {
            nativeDestroy(nativeHandle)
            nativeHandle = 0L
        }
    }

    companion object {
        init {
            System.loadLibrary("opus_jni")
        }
    }

    private external fun nativeCreate(sampleRate: Int, channels: Int): Long
    private external fun nativeWrite(handle: Long, pcm: ShortArray, offset: Int, length: Int): Int
    private external fun nativeStart(handle: Long)
    private external fun nativeDestroy(handle: Long)
}
