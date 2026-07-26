# AudioBridge

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Android-blue)
![License](https://img.shields.io/badge/license-GPL--3.0-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Android](https://img.shields.io/badge/Android-8.0%2B-brightgreen)

> Stream audio di sistema da Windows ad Android via Wi-Fi — codec Opus, bassa latenza, discovery mDNS automatico.

## Panoramica

```
[PC Windows]  ──WASAPI──►  Opus Encoder  ──UDP 54322──►  [Android]  ──►  Cuffie BT
                            TCP 54321 (controllo + keep-alive)
```

## Struttura

Monorepo con due progetti principali:

```
audiobridge/
├── desktop/AudioBridge.Desktop/   # Applicazione Windows (Avalonia .NET 8)
├── android/app/                   # App Android (Kotlin + Jetpack Compose)
├── shared/PROTOCOL.md             # Specifica protocollo di rete
└── README.md
```

## Requisiti

- **Desktop**: Windows 10 o superiore
- **Mobile**: Android 8.0 (API 26) o superiore
- **Rete**: stessa rete Wi-Fi per PC e telefono (nessun cavo Ethernet sul PC se usi mDNS)

## Come Funziona

1. **Desktop** cattura l'audio di sistema tramite **WASAPI loopback** (NAudio)
2. L'audio viene **codificato in Opus** (Concentus C# sul desktop, libopus NDK su Android)
3. Trasmesso via **UDP** (porta `54322`) con header binario (sequence, timestamp NTP, flags)
4. Canale di **controllo TCP** (porta `54321`) per handshake, keep-alive e comandi
5. **Android** riceve, decodifica con OpusDecoder JNI e riproduce via **Oboe (AAudio)** a bassa latenza
6. **Discovery** automatico via mDNS (`_audiobridge._tcp.local`) o inserimento IP manuale

## Installazione Desktop

Scarica `AudioBridge-Setup-1.0.0.exe` dall'ultima release ed eseguilo.
L'installer copia i file in `%ProgramFiles%\AudioBridge` e crea shortcut nel menu Start e sul desktop.

In alternativa, build da sorgente:
```bash
dotnet publish desktop/AudioBridge.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows
```

## Installazione Android

1. Scarica `app-release.apk` dall'ultima release
2. Sul telefono: **Impostazioni → Sicurezza → Installa da sorgenti sconosciute** (attiva per il file manager)
3. Installa con ADB (consigliato):
   ```bash
   adb install app-release.apk
   ```
4. **Disabilita ottimizzazione batteria** per AudioBridge:
   - Impostazioni → App → AudioBridge → Batteria → **Non ottimizzare**
   - Necessario per mantenere la connessione in background

## Avvio Rapido

1. Avvia **AudioBridge** sul PC
2. Apri l'app **AudioBridge** su Android
3. Inserisci l'**indirizzo IP** del PC (mostrato nell'interfaccia desktop) oppure seleziona un dispositivo dai **Preferiti**
4. Premi **Connetti**
5. L'audio del PC arriva direttamente sulle cuffie Android

## Limitazioni Note

- **mDNS** non funziona se PC è su Ethernet e telefono su WiFi (subnet diverse). Usa IP manuale.
- **Latenza Bluetooth** ~150–200ms via SBC — limite hardware del profilo A2DP. Cuffie filari o codec LDAC/aptX migliorano.
- **Linux**: la cattura audio (PipeWire/PulseAudio) è solo abbozzata — *TODO*.
- **Skip/Back** non disponibili: AudioBridge è uno stream live, non una playlist.

## Architettura Tecnica

| Componente | Dettaglio |
|------------|-----------|
| **Codec** | Opus 48 kHz stereo, bitrate 64–320 kbps |
| **Frame size** | 5–60 ms configurabile (default 20 ms) |
| **UDP (dati)** | Porta `54322` — header binario (18 byte) + payload Opus |
| **TCP (controllo)** | Porta `54321` — handshake HELLO/WELCOME, JSON Lines |
| **Keep-alive** | PING ogni 3 secondi, timeout assoluto 10 s |
| **Persistenza desktop** | JSON in `%APPDATA%\AudioBridge\` |
| **Persistenza Android** | DataStore Preferences (Jetpack) |
| **Discovery** | mDNS `_audiobridge._tcp.local` (NsdManager) |
| **Desktop UI** | Avalonia + CommunityToolkit.Mvvm |
| **Android UI** | Jetpack Compose + Material 3 |
| **Playback Android** | Oboe (AAudio) + jitter buffer 5 frame |

Il protocollo di rete è documentato in dettaglio in [`shared/PROTOCOL.md`](shared/PROTOCOL.md).

## Build da Sorgente

### Desktop (Windows)

```bash
dotnet publish desktop/AudioBridge.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows
```

Output in `publish/windows/AudioBridge.Desktop.exe`.

### Android

```bash
cd android
# Crea keystore (una tantum):
# keytool -genkey -v -keystore audiobridge.jks -alias audiobridge -keyalg RSA -keysize 2048 -validity 10000

# Build APK firmato:
$env:AUDIOBRIDGE_STORE_PASS="<password>"
$env:AUDIOBRIDGE_KEY_PASS="<password>"
.\gradlew assembleRelease
```

APK in `android/app/build/outputs/apk/release/app-release.apk`.

In alternativa, apri `android/` in **Android Studio → Build → Generate Signed APK → release**.

## Licenza

Distribuito sotto licenza [GPL-3.0](LICENSE).
