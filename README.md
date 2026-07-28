# AudioBridge

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20Android-blue)
![License](https://img.shields.io/badge/license-GPL--3.0-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Android](https://img.shields.io/badge/Android-8.0%2B-brightgreen)

> Stream audio di sistema da PC ad Android via Wi-Fi — codec Opus, bassa latenza, discovery mDNS automatico.

## Panoramica

```
[PC Windows]  ──WASAPI────►  Opus Encoder  ──UDP 54322──►  [Android]  ──►  Cuffie BT
[PC Linux]    ──PipeWire──►       ↑                       TCP 54321 (controllo + keep-alive)
                             stesso binar io (Avalonia .NET 8)
```

## Struttura

Monorepo con tre progetti principali:

```
audiobridge/
├── desktop/AudioBridge.Desktop/    # Applicazione Windows (Avalonia .NET 8)
├── desktop-linux/AudioBridge.Linux/ # Applicazione Linux (Avalonia .NET 8)
├── android/app/                    # App Android (Kotlin + Jetpack Compose)
├── shared/PROTOCOL.md              # Specifica protocollo di rete
└── README.md
```

## Requisiti

- **Desktop**: Windows 10+ oppure Linux con PipeWire
- **Mobile**: Android 8.0 (API 26) o superiore
- **Rete**: stessa rete Wi-Fi per PC e telefono (nessun cavo Ethernet sul PC se usi mDNS)

## Come Funziona

1. **Desktop** cattura l'audio di sistema — **WASAPI loopback** su Windows, **PipeWire (pw-record)** su Linux
2. L'audio viene **codificato in Opus** (Concentus C# sul desktop, libopus NDK su Android)
3. Trasmesso via **UDP** (porta `54322`) con header binario (sequence, timestamp NTP, flags)
4. Canale di **controllo TCP** (porta `54321`) per handshake, keep-alive e comandi
5. **Android** riceve, decodifica con OpusDecoder JNI e riproduce via **Oboe (AAudio)** a bassa latenza
6. **Discovery** automatico via mDNS (`_audiobridge._tcp.local`) o inserimento IP manuale

## Installazione Desktop

### Windows

Scarica `AudioBridge-Setup-1.0.0.exe` dall'ultima release ed eseguilo.

Build da sorgente:
```bash
dotnet publish desktop/AudioBridge.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows
```

### Linux (Arch Linux)

**Prerequisiti:** PipeWire (già preinstallato su Arch Linux moderne).
```bash
sudo pacman -S pipewire pipewire-pulse wireplumber   # se non già presente
```

Build ed esecuzione:
```bash
# Build
dotnet publish desktop-linux/AudioBridge.Linux -c Release -r linux-x64 --self-contained true -o publish/linux

# Oppure build + run diretto
dotnet run --project desktop-linux/AudioBridge.Linux
```

L'app si apre con la finestra principale. Nessuna dipendenza aggiuntiva — la cattura audio usa `pw-record` fornito da PipeWire.

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
3. Tocca **Cerca dispositivi** — il PC dovrebbe comparire come "arch-*" (o il nome del tuo PC)
4. Seleziona il PC e premi **Connetti**
5. Sul PC, l'indicatore diventa verde e mostra "Connesso"
6. Premi **Avvia trasmissione** sul PC
7. L'audio del PC arriva direttamente sulle cuffie Android

In alternativa, inserisci l'**indirizzo IP** del PC manualmente.

## Test End-to-End (Linux → Android)

### Verifica prerequisiti lato Linux

```bash
# 1. PipeWire funzionante
pw-record --target=@DEFAULT_MONITOR@ --format=s16 --rate=48000 --channels=2 --latency=20ms /dev/null
# Se non funziona: sudo pacman -S pipewire pipewire-pulse wireplumber

# 2. Porte libere
ss -tlnp | grep -E '54321|54322'

# 3. Nessun conflitto mDNS
systemctl status avahi-daemon   # se attivo: sudo systemctl stop avahi-daemon
```

### Procedura di verifica

| Passo | Azione | Risultato atteso |
|-------|--------|-----------------|
| 1 | Avvia AudioBridge su Linux | Finestra principale con stato "Disconnesso" |
| 2 | Apri AudioBridge su Android, tocca "Cerca dispositivi" | Il PC Linux compare nella lista mDNS |
| 3 | Connetti | Indicatore verde su entrambi, stato "Connesso" |
| 4 | Premi "Avvia trasmissione" sul PC | Audio in streaming verso il telefono |
| 5 | Riproduci audio sul PC (YouTube, musica locale) | L'audio arriva alle cuffie Bluetooth collegate al telefono |
| 6 | Spegni WiFi del telefono per 10 secondi, riaccendi | Riconnessione automatica entro 10-15 secondi |

### Risultati test (ambiente di riferimento)

| Parametro | Valore |
|-----------|--------|
| PC | Arch Linux, PipeWire 1.x |
| Telefono | Android 14 |
| WiFi | 5 GHz, stesso AP |
| Cuffie | Bluetooth 5.0 (SBC) |
| Latenza percepita | ~100-150 ms (rete) + ~150-200 ms (BT) |

## Troubleshooting Linux

| Problema | Causa | Soluzione |
|----------|-------|-----------|
| "pw-record non trovato" all'avvio | PipeWire non installato | `sudo pacman -S pipewire pipewire-pulse wireplumber` |
| mDNS non funziona, telefono non trova il PC | Conflitto con `avahi-daemon` sulla porta 5353 | `sudo systemctl stop avahi-daemon` oppure usa IP manuale |
| Audio distorto o rumoroso | Conversione s16→float32 errata | Segnala il bug — aggiorna all'ultima versione |
| App non si avvia su Wayland | Problemi Avalonia + Wayland | Avvia con `DISPLAY=:0 audiobridge` per forzare X11 via XWayland |
| Porta già in uso | Un'altra istanza di AudioBridge è in esecuzione | Chiudi l'altra istanza o cambia porta nelle impostazioni |

## Limitazioni Note

- **mDNS** non funziona se PC è su Ethernet e telefono su WiFi (subnet diverse). Usa IP manuale.
- **Latenza Bluetooth** ~150–200ms via SBC — limite hardware del profilo A2DP. Cuffie filari o codec LDAC/aptX migliorano.
- **Skip/Back** non disponibili: AudioBridge è uno stream live, non una playlist.

## Architettura Tecnica

| Componente | Dettaglio |
|------------|-----------|
| **Codec** | Opus 48 kHz stereo, bitrate 64–320 kbps |
| **Frame size** | 5–60 ms configurabile (default 20 ms) |
| **UDP (dati)** | Porta `54322` — header binario (18 byte) + payload Opus |
| **TCP (controllo)** | Porta `54321` — handshake HELLO/WELCOME, JSON Lines |
| **Keep-alive** | PING ogni 3 secondi, timeout assoluto 10 s |
| **Persistenza desktop** | JSON in `~/.config/audiobridge/` (Linux) o `%APPDATA%\AudioBridge\` (Windows) |
| **Persistenza Android** | DataStore Preferences (Jetpack) |
| **Discovery** | mDNS `_audiobridge._tcp.local` (NsdManager) |
| **Desktop UI** | Avalonia + CommunityToolkit.Mvvm |
| **Android UI** | Jetpack Compose + Material 3 |
| **Playback Android** | Oboe (AAudio) + jitter buffer 5 frame |
| **Cattura Linux** | PipeWire (`pw-record`) — sottoprocesso, formato s16 convertito a float32 |
| **Cattura Windows** | WASAPI loopback (NAudio) |

Il protocollo di rete è documentato in dettaglio in [`shared/PROTOCOL.md`](shared/PROTOCOL.md).

## Build da Sorgente

### Desktop (Windows)

```bash
dotnet publish desktop/AudioBridge.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/windows
```

Output in `publish/windows/AudioBridge.Desktop.exe`.

### Desktop (Linux)

```bash
dotnet publish desktop-linux/AudioBridge.Linux -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux
```

Output in `publish/linux/AudioBridge.Linux`.

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
