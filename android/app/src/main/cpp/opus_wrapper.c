#include <jni.h>
#include <opus.h>
#include <stdlib.h>
#include <string.h>

typedef struct {
    OpusDecoder *decoder;
    int sample_rate;
    int channels;
} DecoderState;

JNIEXPORT jlong JNICALL
Java_com_audiobridge_app_audio_OpusDecoder_nativeCreate(
    JNIEnv *env, jclass clazz, jint sample_rate, jint channels)
{
    int error;
    OpusDecoder *dec = opus_decoder_create((opus_int32)sample_rate, (int)channels, &error);
    if (error != OPUS_OK || !dec)
        return 0;
    DecoderState *state = (DecoderState *)malloc(sizeof(DecoderState));
    if (!state) {
        opus_decoder_destroy(dec);
        return 0;
    }
    state->decoder = dec;
    state->sample_rate = (int)sample_rate;
    state->channels = (int)channels;
    return (jlong)(intptr_t)state;
}

JNIEXPORT jint JNICALL
Java_com_audiobridge_app_audio_OpusDecoder_nativeDecode(
    JNIEnv *env, jclass clazz, jlong native_ptr, jbyteArray in, jshortArray out)
{
    DecoderState *state = (DecoderState *)(intptr_t)native_ptr;
    if (!state || !state->decoder)
        return -1;

    jsize in_len = (*env)->GetArrayLength(env, in);
    jbyte *in_bytes = (*env)->GetByteArrayElements(env, in, NULL);
    if (!in_bytes)
        return -1;

    jsize out_capacity = (*env)->GetArrayLength(env, out);
    jshort *out_shorts = (*env)->GetShortArrayElements(env, out, NULL);
    if (!out_shorts) {
        (*env)->ReleaseByteArrayElements(env, in, in_bytes, JNI_ABORT);
        return -1;
    }

    int frame_size = out_capacity / state->channels;

    int ret = opus_decode(
        state->decoder,
        (const unsigned char *)in_bytes,
        (opus_int32)in_len,
        (opus_int16 *)out_shorts,
        frame_size,
        0);

    (*env)->ReleaseByteArrayElements(env, in, in_bytes, JNI_ABORT);
    (*env)->ReleaseShortArrayElements(env, out, out_shorts, 0);

    return ret;
}

JNIEXPORT void JNICALL
Java_com_audiobridge_app_audio_OpusDecoder_nativeDestroy(
    JNIEnv *env, jclass clazz, jlong native_ptr)
{
    DecoderState *state = (DecoderState *)(intptr_t)native_ptr;
    if (state) {
        if (state->decoder)
            opus_decoder_destroy(state->decoder);
        free(state);
    }
}
