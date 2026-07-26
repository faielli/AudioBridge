#include <oboe/Oboe.h>
#include <jni.h>
#include <vector>
#include <atomic>
#include <cstring>
#include <memory>

class OboeAudioPlayer : public oboe::AudioStreamDataCallback {
public:
    std::shared_ptr<oboe::AudioStream> mStream;
    std::vector<int16_t> ringBuffer;
    std::atomic<int32_t> writeCursor{0};
    std::atomic<int32_t> readCursor{0};
    int32_t channelCount = 2;
    int32_t capacity = 32768;
    int32_t underrunCount = 0;

    oboe::Result open(int32_t sampleRate, int32_t channels) {
        channelCount = channels;
        ringBuffer.resize(capacity);

        oboe::AudioStreamBuilder builder;
        auto result = builder.setDirection(oboe::Direction::Output)
            ->setPerformanceMode(oboe::PerformanceMode::LowLatency)
            ->setSharingMode(oboe::SharingMode::Exclusive)
            ->setFormat(oboe::AudioFormat::I16)
            ->setChannelCount(channels)
            ->setSampleRate(sampleRate)
            ->setDataCallback(this)
            ->openStream(mStream);
        return result;
    }

    oboe::Result start() {
        return mStream->requestStart();
    }

    oboe::Result stop() {
        return mStream->requestStop();
    }

    oboe::Result close() {
        if (mStream) {
            auto result = mStream->close();
            mStream.reset();
            return result;
        }
        return oboe::Result::OK;
    }

    int32_t write(const int16_t* data, int32_t samples) {
        int32_t wp = writeCursor.load(std::memory_order_relaxed);
        int32_t rp = readCursor.load(std::memory_order_acquire);
        int32_t used = wp - rp;
        int32_t available = capacity - used;
        int32_t toWrite = std::min(samples, available);

        for (int32_t i = 0; i < toWrite; i++) {
            ringBuffer[(wp + i) & (capacity - 1)] = data[i];
        }
        writeCursor.store(wp + toWrite, std::memory_order_release);
        return toWrite;
    }

    oboe::DataCallbackResult onAudioReady(
        oboe::AudioStream* /*stream*/,
        void* audioData,
        int32_t numFrames
    ) override {
        auto* output = static_cast<int16_t*>(audioData);
        int32_t samplesNeeded = numFrames * channelCount;

        int32_t rp = readCursor.load(std::memory_order_relaxed);
        int32_t wp = writeCursor.load(std::memory_order_acquire);
        int32_t available = wp - rp;
        int32_t toRead = std::min(samplesNeeded, available);

        for (int32_t i = 0; i < toRead; i++) {
            output[i] = ringBuffer[(rp + i) & (capacity - 1)];
        }
        if (toRead < samplesNeeded) {
            std::memset(output + toRead, 0,
                (samplesNeeded - toRead) * sizeof(int16_t));
            underrunCount++;
        }
        readCursor.store(rp + toRead, std::memory_order_release);

        return oboe::DataCallbackResult::Continue;
    }
};

extern "C" {

JNIEXPORT jlong JNICALL
Java_com_audiobridge_app_audio_OboePlayer_nativeCreate(
    JNIEnv* /*env*/, jclass /*clazz*/, jint sampleRate, jint channels)
{
    auto* player = new OboeAudioPlayer();
    auto result = player->open(sampleRate, channels);
    if (result != oboe::Result::OK) {
        delete player;
        return 0;
    }
    return reinterpret_cast<jlong>(player);
}

JNIEXPORT jint JNICALL
Java_com_audiobridge_app_audio_OboePlayer_nativeWrite(
    JNIEnv* env, jclass /*clazz*/, jlong handle,
    jshortArray pcm, jint offset, jint length)
{
    auto* player = reinterpret_cast<OboeAudioPlayer*>(handle);
    if (!player) return -1;

    jshort* elements = env->GetShortArrayElements(pcm, nullptr);
    if (!elements) return -1;

    int32_t written = player->write(elements + offset, length);
    env->ReleaseShortArrayElements(pcm, elements, JNI_ABORT);
    return written;
}

JNIEXPORT void JNICALL
Java_com_audiobridge_app_audio_OboePlayer_nativeStart(
    JNIEnv* /*env*/, jclass /*clazz*/, jlong handle)
{
    auto* player = reinterpret_cast<OboeAudioPlayer*>(handle);
    if (player) player->start();
}

JNIEXPORT void JNICALL
Java_com_audiobridge_app_audio_OboePlayer_nativeDestroy(
    JNIEnv* /*env*/, jclass /*clazz*/, jlong handle)
{
    auto* player = reinterpret_cast<OboeAudioPlayer*>(handle);
    if (player) {
        player->close();
        delete player;
    }
}

} // extern "C"
