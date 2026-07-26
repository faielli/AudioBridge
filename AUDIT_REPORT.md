# Audit Report — AudioBridge

> Generato il 2026-07-26 da analisi statica del codice esistente.
> Scope: `desktop/AudioBridge.Desktop/` + `android/app/src/main/java/com/audiobridge/app/`
> Escluso: `android/app/src/main/cpp/oboe/` (libreria terza Oboe/Google)

---

## Summary

| Severità | Count | Aree principali |
|----------|-------|-----------------|
| CRITICAL | 10 | Sicurezza rete, leak nativi, build release, backup, static state, overflow |
| HIGH | 41 | Thread-safety, parsing JSON, protocollo non validato, race, lifecycle Service |
| MEDIUM | 42 | Risorsa leak, theme, race minori, build, coerenza costanti, log |
| LOW | 63 | Dead code, smell, i18n, magic numbers, formattazione |
| **Total** | **156** | |

---

<!-- ====================================================================== -->
## Wave 1 — Sicurezza critica + Build (fix bloccanti per qualsiasi release)

### Build Android (`android/app/build.gradle.kts`)

1. `[CRITICAL][Build] build.gradle.kts:28-30` — Release firmata con chiave **debug** + `isMinifyEnabled=false` → APK non pubblicabile, reverse-engineering banale.  
   **Fix**: creare `signingConfigs.release` con keystore dedicato, `isMinifyEnabled=true`, r8 rules per Concentus/Oboe/JNI.

2. `[HIGH][Build] build.gradle.kts:11` — `compileSdk`/`targetSdk=34` obsoleto (Play Store richiede 35 da 2025).  
   **Fix**: bump a 35.

3. `[HIGH][Build] build.gradle.kts:67` — BOM Compose `2024.06.00` (1 anno).  
   **Fix**: aggiornare a `2025.xx.xx`.

4. `[MEDIUM][Build] build.gradle.kts:78` — `constraintlayout-compose:1.1.0` dichiarata ma **mai usata** in alcun file.  
   **Fix**: rimuovere.

5. `[MEDIUM][Build] build.gradle.kts:5` — `kotlin("plugin.parcelize")` attivo ma nessuna classe `@Parcelize`.  
   **Fix**: rimuovere.

6. `[MEDIUM][Build] build.gradle.kts:21-23` — ABI filters includono `x86` (32-bit) deprecato per audio nativo (+2-3MB/APB).  
   **Fix**: tenere solo `arm64-v8a`, `armeabi-v7a`, `x86_64` o passare ad App Bundle.

7. `[MEDIUM][Build] build.gradle.kts:88` — `com.google.android.material:material` in conflitto con Compose Material3.  
   **Fix**: rimuovere se non usato.

8. `[MEDIUM][Build] build.gradle.kts:39-46` — `jvmTarget=1.8` / `sourceCompatibility=VERSION_1_8` blocca modernizzazione Kotlin/Java.  
   **Fix**: migrare a 17 quando possibile.

9. `[LOW][Build] build.gradle.kts:95` — `ui-tooling-preview` ambiguo (il preview è in `ui-tooling`).  
   **Fix**: tenere solo `ui-tooling`.

10. `[LOW][Build] build.gradle.kts:11` — `ndkVersion` pinned senza fallback → se NDK non installato build casca opacamente.  
    **Fix**: documentare o lasciare SDK scegliere.

### AndroidManifest.xml

11. `[CRITICAL][Security] AndroidManifest.xml:17` — `android:allowBackup="true"` + `backup_rules.xml:3` include `<include domain="sharedpref" path="."/>` → storico IP/host connessi esfiltrabile via `adb backup`.  
    **Fix**: `android:allowBackup="false"` o escludere esplicitamente i file DataStore.

12. `[HIGH][Security] AndroidManifest.xml:12` — `RECORD_AUDIO` dichiarata ma l'app è solo **ricevitore** audio → utente può rifiutare + audit Play Store.  
    **Fix**: rimuovere.

13. `[HIGH][Security] AndroidManifest.xml:9` — `FOREGROUND_SERVICE` (generico) ridondante col typed `mediaPlayback` su Android 14+.  
    **Fix**: rimuovere.

14. `[MEDIUM][Security] AndroidManifest.xml:11` — `WAKE_LOCK` dichiarata ma mai acquisita via `PowerManager`.  
    **Fix**: acquisire `PARTIAL_WAKE_LOCK` in `AudioPlaybackService` o rimuovere.

15. `[MEDIUM][Security] AndroidManifest.xml:13` — `CHANGE_WIFI_MULTICAST_STATE` senza `NEARBY_WIFI_DEVICES` (API 33+) → NSD può fallire silenziosamente.  
    **Fix**: aggiungere permesso.

16. `[HIGH][Security] AndroidManifest` — Assenza di `android:networkSecurityConfig`; `TcpControlClient` usa `Socket()` cleartext → su Android 8+ di default cleartext è **bloccato** → socket può fallire senza un config permissivo esplicito.  
    **Fix**: creare `network_security_config.xml` che blocchi cleartext globalmente e lo permetta solo per host `.local.`/mDNS.

17. `[LOW][Security] AndroidManifest.xml:27-37` — Activity senza `android:configChanges` → rotazione distrugge/ricrea Activity → perde stato discovery.  
    **Fix**: gestire `savedInstanceState` o `configChanges`.

### Protocollo di rete (cross-platform)

18. `[CRITICAL][Security] TcpControlChannel.cs:103-131` (desktop) — HELLO del client non autenticato → qualsiasi dispositivo sulla LAN può negoziare uno stream.  
    **Fix**: aggiungere `shared_secret` HMAC nel messaggio HELLO, validato dal server.

19. `[CRITICAL][Security] TcpControlClient.kt:65-66` (android) — Connessione TCP cleartext a host arbitrario senza TLS → MITM/SSRF.  
    **Fix**: filtrare host permessi (RFC1918/loopback/link-local), confermare IP pubblico all'utente, aggiungere TLS opzionale con PSK.

20. `[CRITICAL][Security] UdpAudioReceiver.kt:26,43` (android) — Socket UDP bound su `0.0.0.0` senza filtro mittente → chiunque sulla LAN può iniettare pacchetti audio o fare DoS.  
    **Fix**: filtrare `packet.address` contro l'IP TCP negoziato; opzionalmente HMAC nel PacketHeader.

21. `[CRITICAL][Security] PacketHeader.kt:13-25` (android) — `payloadLen` (16-bit) non validato contro `MAX_PAYLOAD_SIZE (1200)` né contro la dimensione reale del datagramma → server malevolo può inviare `payloadLen=65535` su datagramma 18B = header incoerente.  
    **Fix**: restituire `null` se `payloadLen > MAX_PAYLOAD_SIZE || payloadLen > data.size - HEADER_SIZE`.

22. `[HIGH][Security] PacketHeader.kt:19` (android) — `timestampNtp` letto come `getLong()` ma non validato → timestamp NTP arbitrari possono manipolare jitter/latenza.  
    **Fix**: validare range [now ± 1 anno] lato ricezione.

23. `[HIGH][Bug] UdpAudioReceiver.kt:45` (android) — `packet.length - HEADER_SIZE` può essere negativo se `packet.length < HEADER_SIZE` → `minOf` su valore negativo.  
    **Fix**: `if (packet.length < HEADER_SIZE) return@forEach` prima di `parse`.

24. `[HIGH][Bug] TcpControlClient.kt:78-79` (android) — `reader!!.readLine()` blocca fino a `soTimeout` se il server invia una linea >8KB senza newline; timeout → `SocketTimeoutException` catch → `scheduleReconnect` infinito.  
    **Fix**: usare `BufferedReader.read(charArray, 0, MAX_LINE_LEN)` con cap a 4KB o `JsonReader` streaming.

25. `[HIGH][Bug] TcpControlClient.kt:119-130` (android) — `parseWelcome` non valida esistenza/tipo dei campi JSON; `getInt`/`getString` lanciano `JSONException` → catch generico `Exception` → reconnect loop silenzioso.  
    **Fix**: validare ogni campo: `require(sampleRate in 8000..96000)`, `require(channels in 1..2)`, `require(udpPort in 1..65535)`.

26. `[HIGH][Bug] TcpControlClient.kt:142-144` (android) — `onDisconnected?.invoke()` poi `disconnectInternal()` che chiama `keepAliveJob?.cancel()` → una coroutine cancella sé stessa → comportamento indefinito.  
    **Fix**: lanciare `onDisconnected` in `scope.launch { ... }` separato.

27. `[HIGH][Bug] TcpControlClient.kt:161-178 vs 242-254` (android) — notifica `onDisconnected` fatta da due percorsi (readJob finally e disconnectInternal) → doppia notifica in race.  
    **Fix**: flag `@Volatile var notifiedDisconnect` o centralizzare in `disconnectInternal`.

28. `[HIGH][Security] TcpControlClient.kt:181-211` (android) — `handleMessage` processa JSON arbitrario dal server senza whitelist di `type` né validazione di `ts`.  
    **Fix**: definire una whitelist di tipi, validare `ts` come long valido, loggare tipi sconosciuti.

29. `[HIGH][Bug] TcpControlChannel.cs:275-288` (desktop) — `ReadLineAsync` legge 1 byte alla volta senza limite → client malevolo può inviare MB senza `\n` → memory exhaustion.  
    **Fix**: cap a 65536 byte.

30. `[MEDIUM][Bug] TcpControlClient.kt:67` (android) — `soTimeout=10000` → readJob resta bloccato 10s dopo che la connessione cade → latenza notifica disconnect molto alta.  
    **Fix**: ridurre a 3000-5000.

31. `[MEDIUM][Bug] TcpControlClient.kt:232-240` (android) — `writeLine` skipper silenzioso se `writer==null` → keep-alive crede di inviare PING ma non lo fa.  
    **Fix**: lanciare IOException o ritornare `false`.

32. `[MEDIUM][Bug] TcpControlClient.kt:237` (android) — `write('\n'.code)` → `OutputStreamWriter.write(int)` scrive solo 8-bit → ok per `\n` ma semanticamente scorretto.  
    **Fix**: `write('\n')`.

33. `[MEDIUM][Bug] TcpControlClient.kt:38,60,99` (android) — `CoroutineScope(Dispatchers.IO + SupervisorJob())` in costruttore senza lifecycle → se `close()` non chiamata, job + socket leak.  
    **Fix**: rendere la classe `AutoCloseable` o legare al ciclo di vita.

34. `[MEDIUM][Bug] ProtocolConstants.kt:18 vs SettingsDataStore` — `JITTER_BUFFER_FRAMES=3` costante hardcoded vs default `5` in DataStore → contraddizione.  
    **Fix**: sincronizzare o deduplicare.

35. `[LOW][Bug] ProtocolConstants.kt:15` — `FRAME_SAMPLES=SAMPLE_RATE*FRAME_SIZE_MS/1000=960` costante calcolata a compile-time a 48kHz → se sampleRate negoziato ≠ 48000 (es. 44100), costante sbagliata.  
    **Fix**: marcare `// default only` o calcolare dinamicamente (già fatto in StreamSession.kt:32).

36. `[LOW][Smell] PacketHeader.kt:22` (android) — byte riservato letto con `buf.get()` e scartato senza commento.  
    **Fix**: `val reserved = buf.get() // ignored`.

37. `[MEDIUM][Smell] PacketHeader.cs:29-38` (desktop) — `Unsafe.As<byte, PacketHeader>` su arch little-endian; su big-endian si rompe.  
    **Fix**: documentare il vincolo o usare `BinaryPrimitives` esplicito.

38. `[MEDIUM][Security] UdpAudioSender.cs:27-33` (desktop) — metodo pubblico `Send(byte[], int)` senza validazione lunghezza → chiunque può chiamarlo dopo `SetRemote`.  
    **Fix**: renderlo `internal` o validare `length <= Mtu`.

---

<!-- ====================================================================== -->
## Wave 2 — Thread-safety e leak nativi (rischi crash)

### JNI / wrapper nativi Android

39. `[CRITICAL][Bug] OboePlayer.kt:6-11` — `start()` non chiude un eventuale `nativeHandle` precedente → leak nativo su doppia chiamata.  
    **Fix**: `if (nativeHandle != 0L) close()` all'inizio di `start()`.

40. `[HIGH][Bug] OboePlayer.kt:13-16` — `nativeHandle` non `@Volatile` né protetto da `@Synchronized`; `write()` concorrente può chiamare JNI su handle già distrutto da `close()` → crash nativo.  
    **Fix**: `nativeHandle = 0L` dopo close, `@Volatile`, synchronize start/close/write.

41. `[HIGH][Bug] OpusDecoder.kt:12-16` — `nativePtr` letto in `decode()` senza `@Volatile` → race con `close()` → dangling pointer JNI.  
    **Fix**: `@Volatile` + synchronized o `AtomicLong`.

42. `[HIGH][Bug] OboePlayer.kt:13` — `offset`/`length` passati a JNI senza `require()` → valori negativi o out-of-bounds arrivano al codice nativo e crashano.  
    **Fix**: `require(offset in 0..pcm.size && length >= 0 && offset + length <= pcm.size)`.

43. `[HIGH][Bug] AudioPlayer.kt:9-13` — `start()` chiude/ricrea `OboePlayer` senza sincronizzazione → `write()` concorrente può operare su istanza in mutazione.  
    **Fix**: `@Synchronized` o `Mutex` .

44. `[MEDIUM][Bug] OpusDecoder.kt:12-16` — doppia allocazione `ShortArray(frameSize*channels)` + `copyOf(ret*channels)` ad ogni decodifica → pressione GC sul path audio real-time.  
    **Fix**: buffer pre-allocato + ritornare `Int` count; l'applicazione copia solo la regione usata.

45. `[MEDIUM][Bug] OpusDecoder.kt:12` — `frameSize` non validato tra i frame legali Opus (5/10/20/40/60ms) → `ret ≤ 0` restituisce `null` silenzioso.  
    **Fix**: loggare `ret` e il suo significato `OPUS_BAD_ARG`/`OPUS_CORRUPTED_DATA`.

46. `[MEDIUM][Bug] AudioPlayer.kt:7` — `underrunCount` dichiarato ma mai aggiornato → dead code, stato fuorviante.  
    **Fix**: implementare (leggere da JNI) o rimuovere.

47. `[LOW][Smell] OpusDecoder.kt:32` — parametro `in` con backticks (keyword Java) → leggibilità scarsa.  
    **Fix**: rinominare `input`.

48. `[LOW][Smell] OboePlayer.kt:27` — `System.loadLibrary("opus_jni")` in `init {}` → `UnsatisfiedLinkError` al caricamento classe, catchabile solo esternamente.  
    **Fix**: companion `try/catch` o lazy loading.

49. `[LOW][Smell] AudioPlayer.kt:5-22` — wrapper pass-through attorno a `OboePlayer` senza logica aggiunta.  
    **Fix**: fondere in `OboePlayer` o spostare logica reale (underrun handling, restart).

### StreamSession Android (`android/.../stream/StreamSession.kt`)

50. `[CRITICAL][Bug] StreamSession.kt:32` — `frameSamples = sampleRate * frameSizeMs / 1000` senza `require()` → server malevolo con `sampleRate=Int.MAX` causa overflow in Int → `NegativeArraySizeException` crash.  
    **Fix**: `require(sampleRate in 8000..96000 && frameSizeMs in 5..60)`.

51. `[CRITICAL][Bug] StreamSession.kt:57` — `OboePlayer()` creato ma `start()` non chiude handle precedente → leak + crash.  
    **Fix**: `oboePlayer?.close()` prima di ricreare.

52. `[HIGH][Bug] StreamSession.kt:103-114` — `handlePacket` accetta `flags.PCM_RAW` senza validare esplicitamente `opusData.size <= MAX_PAYLOAD_SIZE`.  
    **Fix**: `require(opusData.size <= ProtocolConstants.MAX_PAYLOAD_SIZE)`.

53. `[HIGH][Bug] StreamSession.kt:121,127` — `pcmQueue.send(...)` è `suspend` → se channel pieno (capacity=3), decodeJob si blocca → backlog UDP silenzioso e perdita pacchetti.  
    **Fix**: `trySend` con `BufferOverflow.DROP_OLDEST` o capacity maggiore (es. 20).

54. `[HIGH][Bug] StreamSession.kt:108-109` — se `seq == _lastSeq` (pacchetto duplicato), `gap = seq - _lastSeq - 1 = -1` → `_lostTotal` diventa negativo.  
    **Fix**: `if (seq == _lastSeq) { gap = 0 } else { calcolo normale }`.

55. `[HIGH][Bug] StreamSession.kt:87` — `_queueSize--` (playJob) e `_queueSize++` (handlePacket) non atomici tra due coroutines → conteggio incoerente.  
    **Fix**: `AtomicInteger` o `synchronized`.

56. `[MEDIUM][Bug] StreamSession.kt:56` — `start()` non idempotente; ricrea `opusDecoder` senza chiudere il precedente → leak native pointer.  
    **Fix**: `opusDecoder?.close()` all'inizio di `start()`.

57. `[MEDIUM][Bug] StreamSession.kt:60-67` — `_pktCount` letto/scritto da coroutines diverse senza `@Volatile` → valore potenzialmente inconsistente.  
    **Fix**: `@Volatile` o `AtomicInteger`.

58. `[MEDIUM][Smell] StreamSession.kt:63/72/75` — tag log misto `"AudioBridge"` vs `"StreamSession"`.  
    **Fix**: creare costante `TAG`.

59. `[LOW][Smell] StreamSession.kt:137-143` — `close()` chiama `stop()` (che fa `scope.cancel()`) e poi `decodeJob.cancel()` ecc. su job già cancellati → ridondante.  
    **Fix**: semplificare a `stop()`.

60. `[LOW][Smell] StreamSession.kt:102-105` — `_packetsReceived` incrementato prima del check seq duplicato → stats includono duplicati.  
    **Fix**: spostare dopo `if (seq == _lastSeq) return`.

### Service & ViewModel Android

61. `[CRITICAL][Bug] AudioPlaybackService.kt:25-32` — `connectionState` dichiarato in `companion object` (static) → condiviso tra istanze di Service e tra processi → stato fantasma dopo ricreazione.  
    **Fix**: spostare a campo d'istanza; esporre via binding o `LocalService`.

62. `[HIGH][Bug] AudioPlaybackService.kt:42-44,88` — `startForeground()` chiamato **prima** di validare `intent.hasExtra(EXTRA_HOST)`; se intent null o senza host, si fa startForeground poi stopSelf immediato → su Android 14+ possibile `ForegroundServiceDidNotStartInTimeException`. Inoltre `return START_STICKY` fa restartare il service senza intent → loop stopSelf infinito.  
    **Fix**: validare host **prima** di `startForeground`; ritornare `START_NOT_STICKY`; gestire restart con flag utente.

63. `[HIGH][Bug] AudioPlaybackService.kt:60-86` — Callback `onConnected`/`onDisconnected`/`onError` non disinseriti in `cleanup()` → callback da vecchio client possono ancora invocarsi dopo che il client è stato sostituito.  
    **Fix**: azzerare references callback in `cleanup()`.

64. `[MEDIUM][Bug] AudioPlaybackService.kt:97-99` — Ordine invertito: `_connectionState.value = DISCONNECTED` prima di `tcpClient?.close()` → observer vede disconnected mentre socket è ancora aperto.  
    **Fix**: `tcpClient?.close()` poi stato.

65. `[MEDIUM][Bug] AudioPlaybackService.kt:102-110` — `closeSession()` + `cleanup()` chiamati sequenzialmente senza sync → callback in volo da `closeSession` può operare su risorse già chiuse.  
    **Fix**: serializzare via job unico.

66. `[MEDIUM][Smell] AudioPlaybackService.kt:130-138` — Notifica senza `setContentIntent` (non cliccabile), usa `android.R.drawable.ic_media_play` come icona.  
    **Fix**: aggiungere `PendingIntent` verso `MainActivity` + icona drawable custom.

67. `[LOW][Security] AudioPlaybackService.kt:64,84` — host/IP interpolato nel testo notifica → potenziale leak di indirizzi.  
    **Fix**: usare stringa generica "Streaming attivo".

68. `[HIGH][Bug] NsdDiscovery.kt:23,73-80` — `currentListener` è campo condiviso tra flows → quando `discover()` è chiamato due volte, il secondo flow sovrascrive il listener del primo; `awaitClose { stop() }` stoppa **tutti** i listener.  
    **Fix**: catturare listener localmente nel `channelFlow` invece di campo.

69. `[HIGH][Bug] NsdDiscovery.kt:32-46` — `resolveService` lanciato per ogni servizio senza serializzazione → su Android < 12, `NsdManager` supporta **una sola** risoluzione concorrente → fallimenti silenti.  
    **Fix**: coda di risoluzione (1 alla volta) o API 31+.

70. `[MEDIUM][Bug] NsdDiscovery.kt:32` — `onServiceFound` chiama `resolveService` anche per servizi già noti → lavoro inutile.  
    **Fix**: skip se `services.contains(serviceName)`.

71. `[LOW][Smell] NsdDiscovery.kt:33,59,60` — callback errore vuoti → errori NSD invisibili.  
    **Fix**: loggare via `Log.w`.

72. `[HIGH][Bug] RecentConnectionsStore.kt` / `SettingsDataStore.kt` — `parseList` non catcha `JSONException` → JSON corrotto fa crashare il Flow → l'app intera perde accesso alle preferenze.  
    **Fix**: `.catch { emit(emptyList()) }`.

73. `[HIGH][Security] RecentConnectionsStore.kt:12,32-39` — DataStore Preferences in chiaro + backupabile → storico IP/host esfiltrabile.  
    **Fix**: `EncryptedSharedPreferences` o `Tink` DataStore.

74. `[MEDIUM][Smell] RecentConnectionsStore.kt:9-10` — `org.json` + `kotlinx-serialization-json` coesistenti → standardizzare su `kotlinx.serialization`.

75. `[MEDIUM][Bug] SettingsDataStore.kt:21,24` — default `5` / `false` codificati qui e in `MainViewModel.stateIn` → desync.  
    **Fix**: costante condivisa `object SettingsDefaults`.

### Desktop: cattura audio & stream

76. `[HIGH][Bug] StreamSession.cs:72-92` (desktop) — `OnDataAvailable` gira sul thread di NAudio, scrive `_pcmBuffer`/`_pcmBufferPos`; `Stop()` chiama `_capture.DataAvailable -= OnDataAvailable` ma eventi in volo possono arrivare **dopo** la rimozione dell'handler → operazioni su buffer parzialmente fermi.  
    **Fix**: `lock` + `volatile bool _isStreaming` + `Interlocked` per posizione.

77. `[HIGH][Bug] StreamSession.cs:81-82` (desktop) — `_pcmBuffer[96000]` (~2s stereo); se cattura più veloce dello flush, `break` tronca senza consumare resto → next `OnDataAvailable` sovrascrive nuovi dati su posizione arretrata → artefatti audio/desync permanente.  
    **Fix**: incremento atomico + `if (overflow) drop + log`.

78. `[HIGH][Bug] StreamSession.cs:131+144` (desktop) — `new byte[MaxPayloadSize=1200]` allocato ad ogni frame Opus sul thread di cattura → GC pressure significativa su stream 48kHz/20ms (50 allocazioni/s solo per questo buffer).  
    **Fix**: riutilizzare un buffer pool (es. `ArrayPool<byte>.Shared`).

79. `[HIGH][Bug] MainViewModel.cs:425-437` (desktop) — `OnDataAvailable` desktop: `new float[data.Length/4]` ad ogni chunk + `Dispatcher.UIThread.Post` a ogni pacchetto cattura → flood dispatch + GC. Inoltre `data.Length` non è garantito multiplo di 4.  
    **Fix**: batch RMS calcolo su thread cattura e dispatch throttlato (100-200ms). Aggiungere `data.Length % 4 == 0` guard.

80. `[HIGH][Bug] MainViewModel.cs:189-202` (desktop) — `RecordTest` handler scrive su `WaveFileWriter` dal thread NAudio senza lock → `WaveFileWriter` non thread-safe → file WAV corrotto in race.  
    **Fix**: queue producer/consumer o lock attorno a `Write`.

81. `[MEDIUM][Bug] StreamSession.cs:160-165` (desktop) — `Dispose()` chiama `_cts.Cancel()`, poi `Stop()` che chiama `_capture.Stop()`; `WindowsWASAPICapture.Stop()` ferma NAudio ma `OnDataCaptured` in volo può ancora operare su `_capture` dopo dispose → ObjectDisposedException.  
    **Fix**: `lock` + signalling che attende fine del callback in corso.

82. `[MEDIUM][Bug] WindowsWASAPICapture.cs:51-62` (desktop) — `_isCapturing` non `volatile`; `_stopRequested` non volatile → race con `Stop()`.  
    **Fix**: `private volatile bool _capturing`.

83. `[MEDIUM][Bug] WindowsWASAPICapture.cs:64-75` (desktop) — `Stop()` non attende che NAudio abbia effettivamente fermato; `_capture.Dispose()` può correre prima che `RecordingStopped` fires → ObjectDisposedException.  
    **Fix**: segnalare `ManualResetEvent` per attendere l'effettivo stop.

84. `[MEDIUM][Bug] MainViewModel.cs:170-237` (desktop) — `RecordTest` rimuove `_testHandler` ma in `finally` può ripartire cattura; l'handler `_testHandler` può ancora essere in esecuzione concorrente durante la rimozione → `NullReferenceException` o operazione su writer smaltito.  
    **Fix**: lock attorno a add/remove handler.

85. `[MEDIUM][Bug] MainViewModel.cs:412-423` (desktop) — `OnSessionStreamingChanged` con `streaming=false` chiama `StopUdpStream()` ma **non** aggiorna `IsStreaming`/`ConnectionState` → UI inconsistente.  
    **Fix**: set `IsStreaming = false` e `ConnectionState = Disconnected` dentro callback.

86. `[HIGH][Bug] OpusEncoder.cs:57-62` (desktop) — `Encode` legge `Channels` senza `@Volatile`; `SetFrameSizeMs` muta `_frameSamples` mentre `Encode` può essere in esecuzione su thread NAudio → frame size cambiato a metà encode.  
    **Fix**: `@Volatile` o `try`-`finally` con copia locale.

87. `[MEDIUM][Bug] MdnsPublisher.cs:18-21` (desktop) — `_sd.Probe` rileva conflitto di nome ma advertise comunque → client Android non sa a quale servizio connettersi.  
    **Fix**: retry con nome suffisso `-1`, `-2` …

88. `[LOW][Smell] LinuxPipeWireCapture.cs` — intera classe è TODO; su Linux `Start()` è no-op silenzioso → utente non capisce perché non cattura.  
    **Fix**: lanciare `PlatformNotSupportedException` o implementare pw-record.

### Desktop: TcpControlChannel & SettingsService

89. `[MEDIUM][Bug] TcpControlChannel.cs:38-55` (desktop) — `Start()` chiama `Stop()` che `Dispose` di `_cts`; se due chiamate a `Start()` si sovrappongono, `_cts` può essere null durante `AcceptLoopAsync`.  
    **Fix**: lock.

90. `[MEDIUM][Bug] SettingsService.cs:36` (desktop) — `File.ReadAllText` senza lock concorrente; se `Save` e `Load` in race su thread diversi, il JSON può essere corrotto → `Deserialize` restituisce `null` → settings perse silenziosamente.  
    **Fix**: `FileShare.Read` + tmp+rename su scrittura.

91. `[MEDIUM][Bug] SettingsService.cs:47-54` (desktop) — `Save` cattura qualsiasi eccezione e la ignora → l'utente non sa che le impostazioni non vengono salvate.  
    **Fix**: propagare o loggare come warning utente.

92. `[HIGH][Bug] SettingsViewModel.cs:78` (desktop) — Il costruttore chiama `Save()` sempre dopo `LoadFromSettings()` → scrive il file anche se nessun setting è cambiato, sovrascrivendo modifiche fatte da altra istanza.  
    **Fix**: rimuovere `Save()` dal costruttore.

93. `[MEDIUM][Bug] SettingsViewModel.cs:122-143` (desktop) — Ogni `partial void OnXxxChanged` chiama `Save()` su **ogni** variazione, incluso key-stroke su campo IP/testo → I/O frequente.  
    **Fix**: debounce 500ms o `OnLostFocus`.

94. `[MEDIUM][Bug] SettingsViewModel.cs:182-205` (desktop) — `ApplyAutoStart` scrive `HKCU\...\Run` senza verificare permessi; se exception in mezzo, stato registry resta a metà.  
    **Fix**: try-catch + rollback registrato.

95. `[MEDIUM][Bug] MainWindow.axaml.cs:23-32` (desktop) — `OnWindowClosing` istanzia `new SettingsService()` + `Load()` ad ogni chiusura → I/O inutile, ignora modifiche non salvate in VM.  
    **Fix**: ricevere `SettingsService` via DI / costruttore.

96. `[LOW][Smell] MainWindow.axaml.cs:46` (desktop) — Tray icon usa `avalonia-logo.ico` (logo di Avalonia) → branding errato.  
    **Fix**: icona AudioBridge custom.

97. `[MEDIUM][Bug] ViewLocator.cs:12-30` (desktop) — `Type.GetType(name)` senza specificare assembly → views in assembly diverso da VM non vengono trovate; `Activator.CreateInstance` fragile col trimming AOT .NET 8.  
    **Fix**: mapping esplicito `Dictionary<Type, Type>`.

98. `[MEDIUM][Bug] App.axaml.cs:23` (desktop) — `new WindowsWASAPICapture()` hardcoded → su Linux l'app crasha al boot (WASAPI non esiste).  
    **Fix**: factory che sceglie per OS (`OperatingSystem.IsLinux()` → `LinuxPipeWireCapture`).

99. `[MEDIUM][Smell] Program.cs:23` (desktop) — `LogToTrace()` senza soglia → log di debug in produzione.  
    **Fix**: configurare `LogLevel.Warning` in release.

---

<!-- ====================================================================== -->
## Wave 3 — Bugs funzionali e coerenza protocollo

### Coerenza costanti e default

100. `[MEDIUM][Bug] AppSettings.cs:14` (desktop) — `UdpPort=54320` ma `ProtocolConstants.DefaultDataPort=54322` → disallineamento ovvio; inoltre `UdpPort` è **mai** letto dal codice di stream (si usa sempre `negotiated.UdpPort = ProtocolConstants.DefaultDataPort`).  
     **Fix**: allineare a 54322 o rimuovere da AppSettings se non utilizzato.

101. `[MEDIUM][Bug] AppSettings.cs:10` (desktop) — `Bitrate=192000` vs `ProtocolConstants.DefaultBitrate=256000` → nuova installazione parte con 192k, preset "Musica" applica 256k solo dopo selezione utente → incoerenza.  
     **Fix**: allineare default a 256000.

102. `[HIGH][Bug] MainViewModel.cs:66-70, OpusEncoder.cs:10` (desktop) — `PresetSettings` include `(192000, 15ms, "Medio-basso")` per "Film" ma lato Opus i frame validi sono `[10,20,40,60]ms` a 48kHz → 15ms viene snap silenzioso a 10ms → bitrate reale ≠ atteso. Lato Android il preset "Film" usa 15ms in `MainViewModel.kt` ma disallineato col decoder.  
     **Fix**: usare solo frame size validi Opus: `(192000, 10, "Medio-basso")`.

103. `[LOW][Smell] MainViewModel.cs:78 + 66` (desktop) — `Profiles=["Musica","Film","Gaming"]` mappati a `PresetSettings` di 3 entry; campo `BufferNote` mai visualizzato → dead field.  
     **Fix**: rimuovere `BufferNote`.

104. `[LOW][Bug] MainViewModel.cs:66-70 + 99` (desktop) — `SelectedProfile` da settings può valere 99 (se file corrotto/editato); `OnSelectedProfileChanged` clamped solo output ma non corregge settings → persiste valore invalido.  
     **Fix**: clamp nel costruttore.

105. `[LOW][Bug] MainViewModel.cs:78` (desktop) — Preset "Film"=15ms non coincide con Android `MainViewModel.kt` dove non esiste mapping preset → quando si negoziano parametri, il server invia `DefaultFrameSizeMs=20` (lato desktop) ma client Android non ha logica per applicare lato suo.

### Parsing ed edge cases

106. `[MEDIUM][Bug] MainViewModel.cs:289` (desktop) — `IPAddress.TryParse(TargetIp, out _)` accetta IPv6, ma `TcpControlChannel` si connette solo IPv4 (`IPAddress.Any`); se arriva IPv6, connect fallisce.  
     **Fix**: dopo parse, controllare `ip.AddressFamily == InterNetwork`.

107. `[MEDIUM][Bug] TcpControlChannel.cs:308-317` (desktop) — `NegotiatedParams` ha `UdpPort = ProtocolConstants.DefaultDataPort` ma `AppSettings.UdpPort` (54320) non usato → codice morto.  
     **Fix**: usare `AppSettings.UdpPort` se presente.

108. `[LOW][Smell] ProtocolConstants.kt:24-25` (android) — commenti "same as desktop" senza riferimento preciso.  
     **Fix**: specificare il costante o rimuovere.

109. `[LOW][Bug] MainViewModel.kt:23` (android) — `targetHost: String` var pubblico mutable, non thread-safe.  
     **Fix**: `private set` + `@Volatile`.

110. `[MEDIUM][Bug] MainViewModel.kt:25` (android) — `isStreaming: Boolean get() = _connectionState.value == CONNECTED` non è Flow → non osservabile come tale da Compose.  
     **Fix**: esporre `StateFlow<Boolean>`.

111. `[MEDIUM][Bug] MainViewModel.kt:27-29` (android) — `NsdDiscovery`, `SettingsDataStore`, `RecentConnectionsStore` creati nel ViewModel con `application` context → ok per non-fuite, ma non c'è `onCleared` per chiuderli.  
     **Fix**: override `onCleared()` per pulire risorse.

112. `[HIGH][Bug] MainViewModel.kt:121-124` (android) — `startStream()` non checka `connectionState` → se già CONNECTING, lancia un secondo `startForegroundService` → doppio Intent → doppio TcpControlClient.  
     **Fix**: guardia `if (connectionState.value != DISCONNECTED) return`.

113. `[HIGH][Bug] MainViewModel.kt:132-136` (android) — `onCleared` chiama sempre `stop()` (che fa `stopService`) anche se `keepBackground=true` → viola preferenza utente.  
     **Fix**: se `keepBackground`, non fermare il Service.

114. `[HIGH][Bug] MainViewModel.kt:150-154` (android) — `discovery.stop()` dopo che `viewModelScope` è stato cancellato → `currentListener` può essere null → NPE.  
     **Fix**: `try { discovery.stop() } catch`.

### UI / Compose

115. `[HIGH][Bug] MainActivity.kt:104` (android) — `items[selectedIndex].content(viewModel)` senza `key()` né `NavHost` → side-effect e collezioni di ViewModel distrutti/ricreati ad ogni cambio tab → perdita stato, recollect dei Flow.  
     **Fix**: `key(selectedIndex) { ... }` o `NavHost` con `composable()`.

116. `[HIGH][Bug] SettingsScreen.kt:80-82` (android) — Slider `onValueChange` chiama `viewModel.setJitterBufferSize(value.toInt())` che fa `viewModelScope.launch { settings.setJitterBufferSize(frames) }` ad ogni Float generato dal drag (~60 Hz) → flood di coroutine + DataStore writes.  
     **Fix**: scrivere su `onValueChangeFinished`.

117. `[MEDIUM][Bug] SettingsScreen.kt:84` (android) — `steps = 19` per range 1..20 produce 21 posizioni ma DataStore fa `coerceIn(1,20)` → off-by-one (valori possibili 0..20).  
     **Fix**: `steps = 18` per esattamente 20 posizioni.

118. `[HIGH][Bug] MainScreen.kt:84-99` (android) — Colori di stato `Color.Gray`, `Color(0xFF4CAF50)`, `Color(0xFFE53935)` hardcoded ignorando tema claro/scuro → illeggibile in light theme.  
     **Fix**: mappare tramite `MaterialTheme.colorScheme` o costanti semantiche.

119. `[HIGH][Bug] MainScreen.kt:122,335` (android) — `AudioLevelBar(level = audioLevel)` ma `audioLevel` nel ViewModel è `MutableStateFlow(0f)` mai aggiornato → barra sempre 0% → UI morta.  
     **Fix**: collegare RMS da StreamSession o rimuovere componente.

120. `[MEDIUM][Bug] DeviceListScreen.kt:76` (android) — `TextPrimary`/`TextSecondary` hardcoded (colori dark) usati su sfondo che dipende dal tema → in light theme testo scuro su sfondo scuro → illeggibile.  
     **Fix**: usare `MaterialTheme.colorScheme.onBackground`/`onSurfaceVariant`.

121. `[MEDIUM][Bug] DeviceListScreen.kt:191-205` (android) — `OutlinedTextField` per IP senza `keyboardOptions = KeyboardType.Number` e senza validazione → utente può scrivere "foo" → `connectToIp("foo")` → DNS exception.  
     **Fix**: keyboardType=Uri + regex IPv4.

122. `[MEDIUM][Bug] MainScreen.kt:187-194` (android) — `Column` con `verticalScroll` per `connections.forEach { ... }` → tutte le card composte sempre (nessun recycle); ok per MAX=5 ma anti-pattern.  
     **Fix**: `LazyColumn`.

123. `[MEDIUM][Bug] MainScreen.kt:205` (android) — `SimpleDateFormat` creato in composable a ogni ricomposizione → GC + thread-unsafe.  
     **Fix**: `remember { SimpleDateFormat(...) }` o top-level.

124. `[MEDIUM][Bug] Theme.kt:22-29` (android) — `LightColorScheme` usa `TextSecondary` (grigio scuro designato per dark) su `onSurfaceVariant` → in light su sfondo bianco il contrasto è scarso.  
     **Fix**: definire `LightTextSecondary` separato.

125. `[MEDIUM][Bug] Color.kt` (android) — `TextSecondaryDark` mancante rispetto a `TextSecondary` light → incompletezza palette.  
     **Fix**: aggiungere.

126. `[MEDIUM][Bug] Theme.kt` (android) — `LightColorScheme` non definisce `background`, `surfaceVariant` → default Material3 possono differire dal dark creando incoerenza visiva.  
     **Fix**: completare palette Light.

127. `[LOW][Smell] Type.kt:8` (android) — Solo 7 stili Typography definiti; composabili usano `bodySmall`/`labelSmall` → default Material3 → incoerenza.  
     **Fix**: specificare tutti gli stili utilizzati.

128. `[LOW][Smell] Shape.kt:5` (android) — import `androidx.compose.ui.unit.sp` inutilizzato.  
     **Fix**: rimuovere.

129. `[MEDIUM][Bug] AudioBridgeApplication.kt` (android) — Application vuota (`onCreate` eredita). Potrebbe servire per inizializzare DataStore/crash handler.  
     **Fix**: inizializzare logging/crash reporter o rimuovere `android:name` dal manifest.

130. `[LOW][Smell] AudioPlayer.kt:5-22` (android) — wrapper vuoto (pass-through) → fondere con OboePlayer.

---

<!-- ====================================================================== -->
## Wave 4 — Smell, i18n, dead code, pulizia

### Internazionalizzazione

131. `[MEDIUM][Smell] Tutti gli screen` (android) — Stringhe hardcoded in italiano in tutti i file UI (`DeviceListScreen.kt`, `MainScreen.kt`, `SettingsScreen.kt`, `AudioPlaybackService.kt`).  
     **Fix**: estrarre in `res/values/strings.xml` (esistente, quasi vuoto).  
     - Esempi: "Disconnesso", "Connessione in corso…", "Stop Stream" (misto IT/EN), "Cerca", "Home", "Impostazioni", "Trasmissione ferma", ecc.

132. `[LOW][Smell] MainActivity.kt:61-63` (android) — Tab label hardcodi.  
     **Fix**: stringResource(R.string.xxx).

133. `[MEDIUM][Smell] MainViewModel.cs:28-35` (desktop) — `LogEntries.RemoveAt(0)` in `ObservableCollection` → shift O(n) a ogni log; su stream attivo (molti log) degrada.  
     **Fix**: ring buffer con drop o `ObservableCollection` limit con shift.

134. `[LOW][Smell] MainViewModel.cs:108-111` (desktop) — `static SolidColorBrush` mai congelati (Avalonia `Freeze()`) → overhead binding.  
     **Fix**: creare in XAML resources o chiamare `Freeze()`.

135. `[MEDIUM][Bug] MainViewModel.cs:486-490` (desktop) — `GetLocalIpAddress()` restituisce il primo IPv4 non loopback; su Docker/VPN può restituire IP non raggiungibile dalla LAN.  
     **Fix**: preferire IP RFC1918.

136. `[LOW][Smell] MainViewModel.cs:170-237, 280-320` (desktop) — `RecordTest` (67 linee) e `StartStreaming` (40 linee) → refactoring in metodi più piccoli.

137. `[LOW][Smell] TcpControlChannel.cs:103-180, 206-273` (desktop) — `HandleClientAsync` (78 linee, 6 livelli di nesting) e `ReadLoopAsync` (67 linee) → estrarre metodi.

138. `[LOW][Smell] StreamSession.cs:94-152` (desktop) — `FlushRawFrames` e `FlushOpusFrames` quasi duplicati → template method.

139. `[LOW][Smell] StreamSession.cs:84 + MainViewModel.cs:195` (desktop) — float→short conversion duplicata → `PcmConverter.ToPcm16`.

140. `[LOW][Smell] ` (android) — Tag log "TcpControlClient"/"AudioBridge"/"StreamSession"/"NsdDiscovery" hardcoded ovunque.  
     **Fix**: costante `TAG` per classe o usare Timber.

141. `[LOW][Smell] SettingsDataStore.kt / RecentConnectionsStore.kt` (android) — Astrazione DataStore duplicata → `BaseDataStoreRepository<T>`.

142. `[LOW][Smell] AudioBridgeApplication.kt` (android) — classe Application vuota → rimuovere.

143. `[LOW][Smell] UdpAudioReceiver.kt:27,49` (android) — `soTimeout` + `socket=null` non azzerato dopo close → doppia chiamata close senza safety.  
     **Fix**: `socket = null` dopo `socket.close()`.

144. `[LOW][Smell] PacketHeader.kt:18` — `MAGIC.toInt()` su `UShort` fragile se MAGIC > 0x7FFF.  
     **Fix**: definire MAGIC come `Int` (`0xCDAB`).

145. `[LOW][Smell] DeviceListScreen.kt:87` — `IconButton(onClick = { viewModel.startDiscovery() })` mai disabilitato quando `isScanning=true` → UX confusa anche se funzionalmente ok (cancel/relaunch).  
     **Fix**: `enabled = !isScanning`.

146. `[LOW][Smell] MainScreen.kt:355` — `${(level * 100).toInt()}%` tronca invece di arrotondare.  
     **Fix**: `String.format("%.0f%%", level * 100)`.

147. `[LOW][Smell] MainScreen.kt:95,137,267,329,331` — colori `0xFF4CAF50`(green) e `0xFFE53935`(red) ripetuti → costanti semantiche in `Color.kt`.

148. `[LOW][Smell] desktop/AudioBridge.Desktop.csproj:11` — `<Folder Include="Models\" />` ridondante.  
     **Fix**: rimuovere.

149. `[MEDIUM][Bug] desktop/AudioBridge.Desktop.csproj:7` — `<LangVersion>preview</LangVersion>` → rischia di esporre a breaking changes C#.  
     **Fix**: `12.0` o `default`.

150. `[LOW][Smell] desktop/` — file `dotnet` directory in tree (probabile output build) → gitignore.  
     **Fix**: aggiungere a `.gitignore`.

151. `[LOW][Smell] app.manifest` (desktop) — referenziato in csproj:6 ma non presente nel file system → build potenzialmente fallisce su Windows.  
     **Fix**: creare o rimuovere dal csproj.

152. `[LOW][Smell] Avvio UDP` (android) — `UdpAudioReceiver.kt:18` hardcoded `54322`; il server negozia nel WELCOME → ok ma porta default duplicata.  
     **Fix**: documentare.

153. `[LOW][Smell] MainViewModel.kt:138-148` (android) — `setJitterBufferSize`/`setKeepBackground` lanciano coroutine senza try-catch → fallimento scrittura DataStore silenzioso.  
     **Fix**: try-catch + feedback utente.

154. `[LOW][Smell] ` (both) — Nessun test unitario presente per nessuna classe di logica (encoders, network, decoder).  
     **Fix**: aggiungere test per `PacketHeader.TryRead/Read/Write`, `OpusEncoder` e `SettingsService.Load/Save`.

155. `[LOW][Smell] MainActivity.kt:44` (android) — `viewModel()` factory non esplicita → uso di `AndroidViewModel` senza factory potrebbe fallire in certi scenari DI.  
     **Fix**: fornire `ViewModelProvider.Factory`.

156. `[LOW][Smell] Android` — mix IT/EN in labels: "Stop Stream" vs "Avvia trasmissione".  
     **Fix**: tutto in italiano (target utente IT) o tutto in inglese.

---

## Checklist per file (mappa file → ID findings)

| File | Finding IDs |
|------|------------|
| `android/app/build.gradle.kts` | 1-10 |
| `AndroidManifest.xml` | 11-17 |
| `AudioBridgeApplication.kt` | 129, 142 |
| `MainActivity.kt` | 115, 132, 155 |
| `audio/OboePlayer.kt` | 39-40, 42, 47-48 |
| `audio/OpusDecoder.kt` | 41, 44-45, 47 |
| `audio/AudioPlayer.kt` | 43, 46, 49, 130 |
| `network/NsdDiscovery.kt` | 68-71 |
| `network/PacketHeader.kt` | 21-22, 36, 144 |
| `network/ProtocolConstants.kt` | 34-35, 108 |
| `network/TcpControlClient.kt` | 19, 24-28, 30-33 |
| `network/UdpAudioReceiver.kt` | 20, 23, 143 |
| `service/AudioPlaybackService.kt` | 61-67 |
| `settings/RecentConnectionsStore.kt` | 72-74 |
| `settings/SettingsDataStore.kt` | 75 |
| `stream/StreamSession.kt` | 50-60 |
| `ui/screen/DeviceListScreen.kt` | 120-121, 145 |
| `ui/screen/MainScreen.kt` | 118-119, 122-123, 146-147 |
| `ui/screen/SettingsScreen.kt` | 116-117 |
| `ui/theme/Color.kt` | 125 |
| `ui/theme/Shape.kt` | 128 |
| `ui/theme/Theme.kt` | 124, 126 |
| `ui/theme/Type.kt` | 127 |
| `viewmodel/MainViewModel.kt` | 109-114, 153 |
| `desktop/AudioBridge.Desktop.csproj` | 148-149, 151 |
| `desktop/App.axaml.cs` | 98 |
| `desktop/Program.cs` | 99 |
| `desktop/ViewLocator.cs` | 97 |
| `desktop/Models/AppSettings.cs` | 100-101 |
| `desktop/Capture/WindowsWASAPICapture.cs` | 82-83 |
| `desktop/Capture/LinuxPipeWireCapture.cs` | 88 |
| `desktop/Network/TcpControlChannel.cs` | 18, 29, 37, 89, 107, 137 |
| `desktop/Network/UdpAudioSender.cs` | 38 |
| `desktop/Network/StreamSession.cs` | 76-78, 81, 138-139 |
| `desktop/Network/OpusEncoder.cs` | 86, 102 |
| `desktop/Network/MdnsPublisher.cs` | 87 |
| `desktop/Network/PacketHeader.cs` | 37 |
| `desktop/ViewModels/MainViewModel.cs` | 79-80, 84-85, 102-104, 106, 133-136, 139 |
| `desktop/ViewModels/SettingsViewModel.cs` | 92-94 |
| `desktop/Services/SettingsService.cs` | 90-91 |
| `desktop/Views/MainWindow.axaml.cs` | 95-96 |
| `desktop/Theme.axaml / Styles.axaml` | *(non auditati — XAML UI style)* |

---

## Note per l'agente AI che applicherà i fix

1. **Applicare per Wave**, mai mescolare file di wave diverse. Ogni Wave è autonoma e può essere validata singolarmente.
2. Dopo ogni Wave eseguire:
   - Desktop: `dotnet build desktop/AudioBridge.Desktop/`
   - Android: `cd android && ./gradlew assembleDebug`
3. I file `cpp/oboe/**` non vanno **mai** toccati.
4. Le `require()` / validazioni aggiunte vanno testate con input malformati (payloadLen=65535, sampleRate=INT_MAX, sequenza = MAX_UINT32, timestamp NTP futuri/passati, JSON malformato).
5. Per le costanti disallineate (UdpPort, Bitrate, JitterBuffer), decidere un singolo source of truth.
6. Le modifiche al protocollo di rete (HMAC, validazione payload) devono essere coordinate tra desktop e Android — Update entrambi i lati prima di testare.
7. Dopo la Wave 1, testare su dispositivo fisico Android + Windows: deve connettersi, fare handshake, streammare UDP e disconnettersi senza crash.
8. Rimuovere `RECORD_AUDIO` dal manifest solo dopo aver verificato che l'app non lo usi realmente (lato desktop cattura audio, lato Android solo ricezione).
9. Per `AudioPlaybackService`, prestare attenzione alla lifecycle Android 14 (ForegroundServiceType dichiarato nel manifest).
10. Dopo la Wave 4 (pulizia), eseguire lint: `./gradlew lint` (Android) e `dotnet format` (desktop).
