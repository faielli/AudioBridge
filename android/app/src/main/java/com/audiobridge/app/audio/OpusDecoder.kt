package com.audiobridge.app.audio

import android.util.Log

private const val TAG = "OpusDecoder"

class OpusDecoder(sampleRate: Int, val channels: Int) : AutoCloseable {
    private var nativePtr: Long = 0L

    init {
        nativePtr = nativeCreate(sampleRate, channels)
        Log.d(TAG, "Decoder creato — nativePtr=$nativePtr sampleRate=$sampleRate channels=$channels")
        if (nativePtr == 0L)
            throw RuntimeException("Failed to create Opus decoder")
    }

    fun decode(opusData: ByteArray, frameSize: Int): ShortArray? {
        if (nativePtr == 0L) {
            Log.w(TAG, "decode: nativePtr=0 — decoder chiuso o non inizializzato")
            return null
        }
        val pcm = ShortArray(frameSize * channels)
        val ret = nativeDecode(nativePtr, opusData, pcm)
        Log.d(TAG, "decode: ptr=$nativePtr inputLen=${opusData.size} frameSize=$frameSize outputSamples=$ret")
        return when {
            ret > 0 -> pcm.copyOf(ret * channels)
            ret == -1 -> { Log.w(TAG, "OPUS_BAD_ARG — input corrotto"); null }
            ret == -2 -> { Log.w(TAG, "OPUS_INVALID_STATE — decoder in stato invalido"); null }
            else -> { Log.w(TAG, "decode fallito: ret=$ret"); null }
        }
    }

    override fun close() {
        if (nativePtr != 0L) {
            nativeDestroy(nativePtr)
            nativePtr = 0L
            Log.d(TAG, "Decoder distrutto")
        }
    }

    companion object {
        init {
            try {
                Log.d(TAG, "Caricamento libreria opus_jni...")
                System.loadLibrary("opus_jni")
                Log.d(TAG, "Libreria caricata con successo")
            } catch (e: UnsatisfiedLinkError) {
                Log.e(TAG, "ERRORE caricamento libreria: ${e.message}", e)
            } catch (e: Exception) {
                Log.e(TAG, "ERRORE generico caricamento: ${e.message}", e)
            }
        }
    }

    private external fun nativeCreate(sampleRate: Int, channels: Int): Long
    private external fun nativeDecode(nativePtr: Long, `in`: ByteArray, out: ShortArray): Int
    private external fun nativeDestroy(nativePtr: Long)
}
