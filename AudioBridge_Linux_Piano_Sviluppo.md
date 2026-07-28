# AudioBridge Linux — Piano di Sviluppo per Agente AI

**Obiettivo:** portare AudioBridge su Arch Linux come codebase C# + Avalonia UI separato da quello Windows, **compatibile al 100% con l'app Android esistente e con il protocollo v1.0 già in produzione.**

**Stack:** C# (.NET 8) + Avalonia UI 11.2 + Concentus (Opus) + Makaretu.Dns.Multicast.New + `pw-record` (PipeWire) — identico al desktop Windows eccetto la cattura audio.

**Riferimento protocollo:** `shared/PROTOCOL.md` del repository esistente — non va toccato, l'app Android si connette al Linux esattamente come si connette al Windows.

**Nota di partenza critica:** la codebase Windows contiene già `LinuxPipeWireCapture.cs` come stub vuoto con un commento TODO. Il porting Linux consiste principalmente nel completare quella classe e nel riconfigurare il `.csproj` per escludere NAudio (dipendenza Windows-only) e compilare su Linux. **Tutto il resto del codice (rete, encoding, UI, impostazioni) è già cross-platform e riusabile senza modifiche.**

---

## Come usare questo documento con l'agente AI

Dai all'agente **uno step alla volta**, nell'ordine. Ogni step ha un obiettivo chiuso e un criterio di verifica misurabile. Alla fine di ogni step chiedi all'agente di confermare il criterio prima di passare al successivo.

### Flusso consigliato

1. **Plan iniziale**: fai leggere questo documento all'agente e chiedi un riassunto del piano prima di iniziare a scrivere codice.
2. **Build, uno step alla volta**: un solo step per sessione.
3. **Commit ad ogni step verificato**: ogni step include il comando di commit — usalo prima di passare al successivo, così puoi tornare indietro con `git log`/`git checkout` se uno step rompe qualcosa.
4. Se un step si blocca: torna in Plan e chiedi "rileggi lo step X, cosa è andato storto rispetto al criterio di verifica?"

---

## Contesto architetturale (da leggere prima di iniziare)

### Cosa esiste già nel repo Windows e va **riusato senza modifiche**

| File / Modulo | Perché è già cross-platform |
|---|---|
| `Network/ProtocolConstants.cs` | Nessuna dipendenza OS |
| `Network/PacketHeader.cs` | Struct binaria pura, `unsafe` standard .NET |
| `Network/OpusEncoder.cs` | Usa Concentus (C# puro, no native) |
| `Network/UdpAudioSender.cs` | Usa `System.Net.Sockets` puro |
| `Network/TcpControlChannel.cs` | Usa `System.Net.Sockets` puro |
| `Network/StreamSession.cs` | Dipende solo da `IAudioCapture` e classi di rete |
| `Network/MdnsPublisher.cs` | Usa `Makaretu.Dns.Multicast.New` che gira su Linux |
| `Services/SettingsService.cs` | Già gestisce `SpecialFolder.ApplicationData` su entrambi gli OS |
| `Models/AppSettings.cs` | POCO puro |
| `Controls/ConnectionPulseIndicator.*` | Avalonia puro |
| `ViewModels/MainViewModel.cs` | Da adattare minimalmente (rimuovere import NAudio) |
| `ViewModels/SettingsViewModel.cs` | Avalonia/MVVM puro |
| `Views/MainWindow.axaml(.cs)` | Avalonia puro |
| `Views/SettingsWindow.axaml(.cs)` | Avalonia puro |
| `Theme.axaml` + `Styles.axaml` | Avalonia puro |

### Cosa va **sostituito o modificato**

| File / Modulo | Modifica richiesta |
|---|---|
| `Capture/WindowsWASAPICapture.cs` | **Non includere** nel progetto Linux |
| `Capture/LinuxPipeWireCapture.cs` | **Implementare** — è il cuore del porting |
| `AudioBridge.Desktop.csproj` | Rimuovere NAudio, aggiungere condizionale OS, cambiare `OutputType` da `WinExe` a `Exe` |
| `Program.cs` | Cambiare istanziazione `IAudioCapture`: creare `LinuxPipeWireCapture` invece di `WindowsWASAPICapture` |
| `app.manifest` | Solo Windows — non includere |

### Interfaccia contrattuale da rispettare (`IAudioCapture`)

```csharp
public interface IAudioCapture
{
    event EventHandler<byte[]> DataAvailable;   // byte[] = PCM float32 interleaved (come WASAPI)
    event EventHandler<Exception> ErrorOccurred;
    event EventHandler<bool> IsCapturingChanged;

    bool IsCapturing { get; }
    int SampleRate { get; }    // deve restituire 48000
    int Channels { get; }     // deve restituire 2
    int BitsPerSample { get; } // deve restituire 32 (float32, come WASAPI)

    void Start();
    void Stop();
}
```

**Critico:** `StreamSession.cs` converte i byte ricevuti da `DataAvailable` assumendo **float32 PCM** (`BitConverter.ToSingle(data, i * 4)`). `LinuxPipeWireCapture` deve emettere **esattamente lo stesso formato** — altrimenti l'audio sarà rumore. `pw-record` produce PCM int16 per default: va convertito a float32 prima di emettere l'evento.

---

## STEP 0 — Struttura del progetto Linux separato

**Obiettivo:** creare la cartella `desktop-linux/` con la struttura identica a `desktop/`, copiando i file cross-platform e preparando il `.csproj` Linux-specific.

**Struttura target:**
```
audiobridge/
├── desktop/                          # esistente — Windows, NON toccare
├── desktop-linux/
│   └── AudioBridge.Linux/
│       ├── AudioBridge.Linux.csproj  # nuovo, Linux-only
│       ├── Program.cs                # copiato e adattato
│       ├── App.axaml(.cs)            # copiato identico
│       ├── Assets/                   # copiato identico (icona, font)
│       ├── Capture/
│       │   ├── IAudioCapture.cs      # copiato identico
│       │   └── LinuxPipeWireCapture.cs  # stub per ora, implementato in Step 1
│       ├── Controls/                 # copiato identico
│       ├── Models/                   # copiato identico
│       ├── Network/                  # copiato identico (tutti i file)
│       ├── Services/                 # copiato identico
│       ├── ViewModels/               # copiato + adattato (rimuovere using NAudio)
│       ├── Views/                    # copiato identico
│       ├── Theme.axaml               # copiato identico
│       └── Styles.axaml              # copiato identico
└── shared/
    └── PROTOCOL.md                   # invariato
```

**Prompt per l'agente:**
> Copia la cartella `desktop/AudioBridge.Desktop/` in `desktop-linux/AudioBridge.Linux/`. Rinomina il progetto in `AudioBridge.Linux` e crea un nuovo `AudioBridge.Linux.csproj` basandoti sul `.csproj` Windows con queste differenze:
> 1. **Rimuovi** `<PackageReference Include="NAudio" .../>` — NAudio è Windows-only.
> 2. **Cambia** `<OutputType>WinExe</OutputType>` → `<OutputType>Exe</OutputType>`.
> 3. **Rimuovi** `<ApplicationManifest>app.manifest</ApplicationManifest>` e il file `app.manifest`.
> 4. **Mantieni** tutte le altre dipendenze: Avalonia 11.2, CommunityToolkit.Mvvm, Concentus, Makaretu.Dns.Multicast.New, Avalonia.Fonts.Inter.
> 5. In `Program.cs`, commenta temporaneamente l'istanziazione di `WindowsWASAPICapture` e istanzia `LinuxPipeWireCapture` (lo stub esistente è sufficiente per ora).
> 6. In `ViewModels/MainViewModel.cs`, rimuovi `using NAudio.Wave;` e tutti i riferimenti a classi NAudio — se il ViewModel enumera dispositivi via NAudio, sostituisci con un placeholder `new[] { "Default" }` per ora (sarà gestito in Step 2).
>
> Verifica che il progetto compili con `dotnet build` su Linux (anche se l'app non fa ancora nulla di utile, deve compilare senza errori).

**Verifica:** `dotnet build desktop-linux/AudioBridge.Linux/AudioBridge.Linux.csproj` completa senza errori. L'app si avvia e mostra la finestra principale (anche con indicatore disconnesso e cattura non funzionante).

**Dopo la verifica:**
```bash
git add -A && git commit -m "step0-linux-struttura-progetto: progetto Linux compila, finestra Avalonia si apre"
```

---

## STEP 1 — Implementazione `LinuxPipeWireCapture` via `pw-record`

**Obiettivo:** implementare la cattura audio loopback su Linux usando `pw-record` come sottoprocesso, emettendo PCM float32 compatibile con `StreamSession`.

**Come funziona `pw-record` per loopback:**
`pw-record` può catturare l'output di sistema (loopback) puntando al monitor della sink di default. Il comando da usare è:

```bash
pw-record \
  --target=@DEFAULT_MONITOR@ \
  --format=s16 \
  --rate=48000 \
  --channels=2 \
  --latency=20ms \
  -
```

- `--target=@DEFAULT_MONITOR@` → cattura l'output del sistema (loopback), non il microfono.
- `--format=s16` → PCM signed 16-bit little-endian (più semplice da convertire rispetto a f32).
- `-` → scrive PCM raw su stdout (senza header WAV).
- `--latency=20ms` → allineato al frame size default Opus.

**Implementazione `LinuxPipeWireCapture.cs`:**

```csharp
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AudioBridge.Desktop.Capture;

public sealed class LinuxPipeWireCapture : IAudioCapture, IDisposable
{
    // Formato output: float32 interleaved (compatibile con StreamSession/WASAPI)
    public int SampleRate => 48000;
    public int Channels => 2;
    public int BitsPerSample => 32; // float32 dopo conversione

    public bool IsCapturing => _isCapturing;

    public event EventHandler<byte[]>? DataAvailable;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? IsCapturingChanged;

    private volatile bool _isCapturing;
    private Process? _process;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    // Dimensione buffer di lettura: 48000 * 2ch * 2byte (s16) * 20ms = 3840 byte
    // Letto in chunk da 960 campioni stereo (20ms a 48kHz)
    private const int ChunkSizeBytes = 48000 * 2 * sizeof(short) * 20 / 1000; // 3840

    public void Start()
    {
        if (_isCapturing) return;

        try
        {
            _cts = new CancellationTokenSource();

            var psi = new ProcessStartInfo
            {
                FileName = "pw-record",
                Arguments = "--target=@DEFAULT_MONITOR@ --format=s16 --rate=48000 --channels=2 --latency=20ms -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            _process = Process.Start(psi)
                ?? throw new InvalidOperationException("Impossibile avviare pw-record");

            _isCapturing = true;
            IsCapturingChanged?.Invoke(this, true);

            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var stream = _process!.StandardOutput.BaseStream;
        var s16Buffer = new byte[ChunkSizeBytes];

        try
        {
            while (!ct.IsCancellationRequested && _isCapturing)
            {
                int totalRead = 0;
                while (totalRead < s16Buffer.Length && !ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(
                        s16Buffer.AsMemory(totalRead, s16Buffer.Length - totalRead), ct);
                    if (read == 0) break; // EOF = processo terminato
                    totalRead += read;
                }

                if (totalRead == 0) break;

                // Converti s16 little-endian → float32 (range -1.0 .. +1.0)
                // Necessario perché StreamSession.OnDataAvailable usa BitConverter.ToSingle()
                int sampleCount = totalRead / sizeof(short);
                var float32 = new byte[sampleCount * sizeof(float)];
                for (int i = 0; i < sampleCount; i++)
                {
                    short s = (short)(s16Buffer[i * 2] | (s16Buffer[i * 2 + 1] << 8));
                    float f = s / 32768f;
                    var fb = BitConverter.GetBytes(f);
                    float32[i * 4] = fb[0];
                    float32[i * 4 + 1] = fb[1];
                    float32[i * 4 + 2] = fb[2];
                    float32[i * 4 + 3] = fb[3];
                }

                DataAvailable?.Invoke(this, float32);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_isCapturing)
                ErrorOccurred?.Invoke(this, ex);
        }
        finally
        {
            if (_isCapturing)
            {
                _isCapturing = false;
                IsCapturingChanged?.Invoke(this, false);
            }
        }
    }

    public void Stop()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        _cts?.Cancel();

        try { _process?.Kill(); } catch { }
        try { _process?.WaitForExit(2000); } catch { }
        _process?.Dispose();
        _process = null;
        _cts?.Dispose();
        _cts = null;

        IsCapturingChanged?.Invoke(this, false);
    }

    public void Dispose() => Stop();
}
```

**Prompt per l'agente:**
> Implementa `LinuxPipeWireCapture.cs` in `desktop-linux/AudioBridge.Linux/Capture/` usando il codice sopra come riferimento preciso. Assicurati di:
> 1. Gestire il caso in cui `pw-record` non sia installato nel sistema: intercetta `Win32Exception` / `FileNotFoundException` nel blocco `Start()` e lancia `ErrorOccurred` con un messaggio chiaro ("pw-record non trovato — installa PipeWire: `sudo pacman -S pipewire pipewire-pulse`").
> 2. Monitorare anche `StandardError` del processo in un task separato e loggarla a console con prefisso `[pw-record]` — utile per debug.
> 3. Gestire il riavvio automatico del processo se termina inaspettatamente mentre `_isCapturing` è ancora `true` (max 3 tentativi, poi `ErrorOccurred`).
> 4. Scrivere un test manuale: aggiungi un pulsante temporaneo "Test cattura 5s → WAV" nella UI (anche solo nel pannello diagnostica) che avvia la cattura, accumula 5 secondi di dati float32, e li salva come `.wav` in `/tmp/audiobridge_test.wav` usando i campi WAV header corretti. Questo permette di verificare la cattura ascoltando il file.

**Verifica:** avviando la cattura di test, il file `/tmp/audiobridge_test.wav` viene creato e, riprodotto con `aplay` o qualsiasi player, contiene l'audio che stava suonando sul PC in quel momento. **Verifica anche che il silenzio sia silenzio** (non rumore bianco — segno di conversione s16→float32 errata).

**Dopo la verifica:**
```bash
git add -A && git commit -m "step1-linux-pipewire-capture: cattura PipeWire funzionante, test WAV verificato"
```

**Note per l'agente:**
- Se il monitor della sink di default non funziona con `@DEFAULT_MONITOR@`, provare `pw-record --list-targets` per trovare il nome corretto del monitor e documentarlo nel `README.md` sezione "Note Linux".
- La latenza di `pw-record` e il chunk size non devono essere più grandi del buffer interno di `StreamSession` (96000 short = 1 secondo a 48kHz stereo). Con chunk da 20ms siamo abbondantemente nei limiti.

---

## STEP 2 — Enumerazione dispositivi audio su Linux

**Obiettivo:** rimpiazzare l'enumerazione NAudio (`WaveOut`/`MMDevice`) con l'equivalente PipeWire/Linux nel `SettingsViewModel`, così il dropdown "Sorgente audio" nelle impostazioni funziona.

**Come enumerare i sink PipeWire:**
```bash
pw-dump | python3 -c "
import json,sys
nodes = [n for n in json.load(sys.stdin)
         if n.get('type') == 'PipeWire:Interface:Node'
         and n.get('info',{}).get('props',{}).get('media.class','') in
             ('Audio/Sink', 'Audio/Duplex')]
for n in nodes:
    props = n['info']['props']
    print(f\"{props.get('node.name','?')} | {props.get('node.description','?')}\")
"
```

In alternativa, più semplice e robusto: `pactl list sinks short` (funziona anche con PipeWire tramite la compatibilità PulseAudio).

```bash
pactl list sinks short
# output: <indice>  <nome>  <modulo>  <formato>  <stato>
```

**Prompt per l'agente:**
> In `ViewModels/SettingsViewModel.cs` (versione Linux), sostituisci l'enumerazione dispositivi NAudio con:
> 1. Una funzione `GetAudioSinks()` che esegue `pactl list sinks short` come sottoprocesso, parsifica l'output (colonna 2 = nome del sink, colonna nome descrittivo da `pactl list sinks` versione estesa se disponibile), e restituisce una lista di `AudioDeviceInfo { Name, Description }`.
> 2. Il sink selezionato nelle impostazioni viene passato a `LinuxPipeWireCapture` come target (aggiorna `pw-record --target=<nome_sink>.monitor` invece di `@DEFAULT_MONITOR@`).
> 3. Se la lista è vuota o `pactl` fallisce, usa `@DEFAULT_MONITOR@` come fallback silenzioso e logga a console.
> 4. Aggiorna il dropdown nel `SettingsWindow.axaml` per mostrare la descrizione leggibile del sink (es. "Altoparlanti incorporati") invece del nome tecnico (es. `alsa_output.pci-0000_00_1f.3.analog-stereo`).

**Verifica:** aprendo le impostazioni su Linux, il dropdown "Sorgente audio" mostra i dispositivi audio disponibili nel sistema. Selezionando un dispositivo diverso da quello di default e avviando la trasmissione, l'audio del dispositivo selezionato viene catturato correttamente.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step2-linux-enum-dispositivi-audio: dropdown sorgente audio funzionante su Linux"
```

---

## STEP 3 — Verifica pipeline end-to-end con Android

**Obiettivo:** confermare che l'intera pipeline Linux → WiFi → Android funziona correttamente, producendo audio udibile nelle cuffie Bluetooth, con latenza misurabile comparabile a quella Windows.

**Prerequisiti per questo step:**
- L'app Android APK già compilata è installata sul telefono.
- PC Linux e telefono Android sulla stessa rete WiFi.
- Cuffie Bluetooth già accoppiate al telefono.

**Prompt per l'agente:**
> Non c'è codice nuovo da scrivere in questo step. Esegui invece il seguente test manuale end-to-end e documenta i risultati:
> 1. Avvia AudioBridge Linux sul PC.
> 2. Apri AudioBridge sull'app Android, tocca "Cerca dispositivi" — il PC Linux deve comparire nella lista mDNS.
> 3. Connetti. L'indicatore di connessione deve diventare verde su entrambi i dispositivi.
> 4. Premi "Avvia trasmissione" sul PC. Riproduci audio sul PC (es. un video su YouTube).
> 5. Verifica che l'audio arrivi alle cuffie Bluetooth.
> 6. Misura la latenza soggettiva: riproduci un metronomo visivo sul PC e confronta con il click audio nelle cuffie.
> 7. Testa la disconnessione: spegni il WiFi del telefono per 10 secondi, riaccendilo. La riconnessione deve avvenire automaticamente.
>
> Se qualcosa non funziona, documenta il problema specifico (es. "mDNS non funziona" → vedi troubleshooting sotto) e risolvi prima di procedere.
>
> **Troubleshooting mDNS su Linux:** se il telefono non trova il PC, verifica che `avahi-daemon` non stia bloccando la porta 5353 (conflitto con Makaretu.Dns). Soluzione: disabilitare avahi temporaneamente (`sudo systemctl stop avahi-daemon`) oppure configurare Makaretu per usare una porta diversa. Documenta la soluzione scelta nel `README.md`.
>
> **Troubleshooting firewall:** su Arch Linux con `ufw` o `firewalld` attivo, assicurati che le porte TCP 54321 e UDP 54322 siano aperte per la rete locale:
> ```bash
> sudo ufw allow from 192.168.0.0/16 to any port 54321
> sudo ufw allow from 192.168.0.0/16 to any port 54322
> ```

**Verifica:** audio funzionante end-to-end, latenza comparabile a Windows (tipicamente 50-150ms escludendo Bluetooth), riconnessione automatica funzionante.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step3-linux-pipeline-end-to-end: audio funzionante Linux→Android, latenza verificata"
```

---

## STEP 4 — Gestione riavvio `pw-record` su cambio dispositivo audio

**Obiettivo:** gestire il caso in cui il dispositivo audio di default cambia mentre lo streaming è attivo (es. connessione/disconnessione di cuffie USB o HDMI), che su Linux corrisponde a un evento PipeWire che può terminare il processo `pw-record`.

**Comportamento atteso:**
- Se `pw-record` termina inaspettatamente (exit code != 0 o stdout chiuso), `LinuxPipeWireCapture` deve rilevarlo, attendere 500ms, e riavviare il processo automaticamente (max 3 tentativi in 10 secondi, poi `ErrorOccurred`).
- Se il sink selezionato scompare (dispositivo disconnesso), riavviare con `@DEFAULT_MONITOR@` come fallback e notificare l'utente nella UI con un messaggio temporaneo ("Dispositivo audio non disponibile, uso output predefinito").

**Prompt per l'agente:**
> Aggiorna `LinuxPipeWireCapture` per:
> 1. Monitorare l'exit code del processo `pw-record` nel `ReadLoopAsync`. Se termina con exit code != 0 mentre `_isCapturing` è `true`, tentare riavvio automatico (attendi 500ms, poi richiama `Start()` internamente) con contatore tentativi.
> 2. Esporre un evento `DeviceChanged` che viene fired quando si usa il fallback, così il ViewModel può mostrare un messaggio nella UI.
> 3. Nel `MainViewModel`, iscriversi a questo evento e aggiornare `StatusText` con un avviso temporaneo (3 secondi) quando avviene il fallback.

**Verifica:** durante la trasmissione attiva, disconnetti e riconnetti le cuffie USB/HDMI dal PC. Lo streaming deve riprende automaticamente entro 2 secondi senza che l'utente debba fare nulla.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step4-linux-riavvio-automatico-pw-record: gestione cambio dispositivo audio"
```

---

## STEP 5 — Fallback PulseAudio

**Obiettivo:** aggiungere un fallback a `parec` (PulseAudio) per il raro caso in cui PipeWire non sia disponibile o non funzioni correttamente.

**Come rilevare se usare PipeWire o PulseAudio:**
```bash
pactl info | grep "Server Name"
# → "PulseAudio (on PipeWire 1.x.x)" → PipeWire con compatibilità PulseAudio (usa pw-record)
# → "PulseAudio 15.x" → PulseAudio nativo (usa parec)
```

**Comando `parec` per loopback:**
```bash
parec --device=$(pactl get-default-sink).monitor \
      --format=s16le \
      --rate=48000 \
      --channels=2
```

Produce lo stesso formato s16 little-endian di `pw-record`, quindi la conversione float32 è identica.

**Prompt per l'agente:**
> Crea una classe `LinuxAudioCaptureFactory` con un metodo statico `Create(string? deviceName)` che:
> 1. Esegue `pactl info | grep "Server Name"` per determinare se il backend è PipeWire o PulseAudio nativo.
> 2. Se PipeWire (output contiene "PipeWire"): istanzia `LinuxPipeWireCapture`.
> 3. Se PulseAudio nativo: istanzia `LinuxPulseAudioCapture` (nuova classe con implementazione analoga ma usando `parec` invece di `pw-record`).
> 4. In `Program.cs`, usa `LinuxAudioCaptureFactory.Create(settings.AudioDeviceName)` invece di istanziare direttamente `LinuxPipeWireCapture`.
>
> Implementa `LinuxPulseAudioCapture` con la stessa struttura di `LinuxPipeWireCapture` ma usando `parec` — il loop di lettura e la conversione s16→float32 sono identici, cambia solo il comando.

**Verifica:** su una macchina con PulseAudio nativo (se disponibile per test) l'app funziona. Su PipeWire (caso normale Arch Linux), il comportamento è invariato rispetto allo Step 3.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step5-linux-fallback-pulseaudio: factory con selezione automatica backend audio"
```

---

## STEP 6 — Adattamenti UI specifici Linux

**Obiettivo:** piccoli adattamenti dell'interfaccia per comportamenti specifici Linux (system tray, avvio automatico).

### 6.1 System Tray su Linux

Su Linux il sistema tray con Avalonia richiede il pacchetto `Avalonia.Tray.Desktop` o l'uso di una libreria come `Hardcodet.NotifyIcon.Avalonia`. Verificare la compatibilità con il window manager in uso (X11 vs Wayland).

**Approccio consigliato:**
- Su **X11**: system tray funziona normalmente con `Hardcodet.NotifyIcon.Avalonia`.
- Su **Wayland**: system tray non è supportato dallo standard. Usa invece la minimizzazione della finestra (`WindowState.Minimized`) come fallback e mostra un avviso nella UI: "System tray non disponibile su Wayland — la finestra verrà minimizzata invece di nascondersi".

### 6.2 Avvio automatico con il sistema

Su Linux, l'avvio automatico avviene tramite un file `.desktop` nella cartella `~/.config/autostart/`:

```ini
[Desktop Entry]
Type=Application
Name=AudioBridge
Exec=/usr/bin/audiobridge
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
```

### 6.3 Percorso configurazione

`SettingsService.cs` usa `Environment.SpecialFolder.ApplicationData` che su Linux punta a `~/.config` — corretto per Arch Linux XDG-compliant. Verificare che salvi in `~/.config/audiobridge/settings.json`.

**Prompt per l'agente:**
> 1. Implementa la logica "Minimizza a tray" con rilevamento automatico X11/Wayland (controlla variabile d'ambiente `$WAYLAND_DISPLAY` o `$XDG_SESSION_TYPE`) e fallback su Wayland come descritto sopra. Mostra avviso in UI solo la prima volta (flag in settings).
> 2. Implementa la checkbox "Avvia con il sistema": quando abilitata, crea/aggiorna il file `~/.config/autostart/audiobridge.desktop` con il percorso dell'eseguibile corrente (`System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName`). Quando disabilitata, rimuove il file.
> 3. Verifica che `SettingsService` salvi correttamente in `~/.config/audiobridge/settings.json`.

**Verifica:** abilitando "Avvia con il sistema" e riloggando (o riavviando), AudioBridge parte automaticamente. Abilitando "Minimizza nella barra" (su X11), cliccando la X della finestra l'app scompare dall'area di lavoro ma rimane nell'indicatore di sistema (system tray).

**Dopo la verifica:**
```bash
git add -A && git commit -m "step6-linux-adattamenti-ui: system tray, avvio automatico, percorso config"
```

---

## STEP 7 — Robustezza e casi limite Linux-specifici

**Obiettivo:** gestire i casi limite specifici dell'ambiente Linux non presenti su Windows.

**Casi da gestire esplicitamente:**

| Scenario | Comportamento atteso |
|---|---|
| `pw-record` non installato | Messaggio chiaro: "PipeWire non trovato. Installa con: `sudo pacman -S pipewire pipewire-pulse`" |
| Porta TCP 54321 già in uso (altra istanza) | Messaggio: "Porta già in uso — chiudi l'altra istanza di AudioBridge o cambia porta nelle impostazioni" |
| Rete WiFi che cambia AP (mesh) | Riconnessione automatica tramite `TcpControlChannel` (già implementata — solo verificare) |
| Sessione Wayland senza supporto tray | Fallback a minimizza (già Step 6) |
| PipeWire in stato di errore (SIGKILL al processo) | `ErrorOccurred` + messaggio in UI + pulsante "Riprova" |
| Permessi insufficienti per leggere stdout pw-record | Rarissimo, ma loggare errore chiaramente |

**Prompt per l'agente:**
> Rivedi il codice degli step precedenti aggiungendo gestione esplicita di questi scenari. In particolare:
> 1. All'avvio dell'app, verifica preventivamente la disponibilità di `pw-record` con `which pw-record` e mostra un banner di avviso non bloccante nella schermata principale se non trovato, con istruzione di installazione.
> 2. Aggiungi al pannello diagnostica (Impostazioni → Avanzate → Log) il log degli eventi di connessione/disconnessione e degli errori di cattura, esattamente come esiste nella versione Windows.
> 3. Verifica che la chiusura improvvisa dell'app Android mentre lo streaming è attivo venga rilevata dal `TcpControlChannel` entro i 10 secondi del timeout keep-alive, e che lo stato dell'app torni a "In attesa".

**Verifica:** testa manualmente ciascuno dei 6 scenari della tabella sopra. L'app non crasha mai e mostra sempre un messaggio di errore utile, mai uno stato ambiguo.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step7-linux-robustezza-casi-limite: gestione errori Linux-specifici"
```

---

## STEP 8 — Packaging: AppImage e PKGBUILD

**Obiettivo:** produrre pacchetti distribuibili per Arch Linux.

### Opzione A — AppImage (raccomandato per distribuzione semplice)

AppImage è un singolo file eseguibile autonomo che funziona su qualsiasi distribuzione Linux con le librerie di base.

```bash
# Pubblica self-contained
dotnet publish desktop-linux/AudioBridge.Linux/AudioBridge.Linux.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/linux

# Crea struttura AppImage
mkdir -p AppDir/usr/bin AppDir/usr/share/applications AppDir/usr/share/icons/hicolor/256x256/apps
cp ./publish/linux/AudioBridge.Linux AppDir/usr/bin/audiobridge
cp android/icons/audiobridge.png AppDir/usr/share/icons/hicolor/256x256/apps/audiobridge.png
```

File `AppDir/audiobridge.desktop`:
```ini
[Desktop Entry]
Name=AudioBridge
Exec=audiobridge
Icon=audiobridge
Type=Application
Categories=Audio;
```

```bash
# Scarica appimagetool e crea AppImage
wget -q https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x appimagetool-x86_64.AppImage
./appimagetool-x86_64.AppImage AppDir AudioBridge-x86_64.AppImage
```

### Opzione B — PKGBUILD per AUR

```bash
# PKGBUILD base per AUR
pkgname=audiobridge
pkgver=1.0.0
pkgrel=1
pkgdesc="Trasmetti audio di sistema via WiFi alle cuffie Bluetooth"
arch=('x86_64')
url="https://github.com/utente/audiobridge"
license=('MIT')
depends=('pipewire' 'dotnet-runtime-8.0')
makedepends=('dotnet-sdk-8.0')
source=("$pkgname-$pkgver.tar.gz::...")
sha256sums=('...')

build() {
    dotnet publish desktop-linux/AudioBridge.Linux/AudioBridge.Linux.csproj \
        -c Release -r linux-x64 --self-contained false \
        -o "$srcdir/build"
}

package() {
    install -Dm755 "$srcdir/build/AudioBridge.Linux" "$pkgdir/usr/bin/audiobridge"
    install -Dm644 "$srcdir/audiobridge.desktop" "$pkgdir/usr/share/applications/audiobridge.desktop"
    install -Dm644 "$srcdir/audiobridge.png" "$pkgdir/usr/share/icons/hicolor/256x256/apps/audiobridge.png"
}
```

**Prompt per l'agente:**
> 1. Configura il `publish` .NET self-contained (`-r linux-x64 --self-contained true`) e verifica che il binario funzioni senza .NET runtime installato separatamente.
> 2. Crea la struttura `AppDir/` e un Makefile o script `build-appimage.sh` che automatizza la creazione dell'AppImage. Il risultato deve essere `AudioBridge-x86_64.AppImage` nella root del repo.
> 3. Crea anche un `PKGBUILD` funzionante per AUR (con `--self-contained false` per sfruttare il runtime di sistema), documentando i passi di test con `makepkg -si`.
> 4. Documenta entrambe le opzioni nel `README.md` del progetto Linux con comandi copia-incolla.

**Verifica:** il file `AudioBridge-x86_64.AppImage` è eseguibile su un sistema Arch Linux diverso (o una VM pulita) senza installare .NET. Il `PKGBUILD` compila e installa correttamente con `makepkg -si`.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step8-linux-packaging-appimage-pkgbuild: AppImage e PKGBUILD funzionanti"
```

---

## STEP 9 — Guida utente e documentazione Linux

**Obiettivo:** aggiornare la guida utente esistente (`AudioBridge_Guida_Utente.md`) e il `README.md` per coprire il porting Linux.

**Sezioni da aggiornare/aggiungere:**

1. **Sezione 3 (Installazione su Arch Linux)** — già presente nella guida, verificare che corrisponda esattamente all'installazione reale prodotta allo Step 8.
2. **Troubleshooting Linux-specifico** — aggiungere alla tabella sezione 8:

| Problema | Causa | Soluzione |
|---|---|---|
| "pw-record non trovato" all'avvio | PipeWire non installato | `sudo pacman -S pipewire pipewire-pulse wireplumber` |
| mDNS non funziona, telefono non trova il PC | Conflitto con `avahi-daemon` sulla porta 5353 | `sudo systemctl stop avahi-daemon` oppure usa IP manuale |
| Audio distorto o rumoroso | Conversione s16→float32 errata (bug) | Aggiorna all'ultima versione; segnala il bug |
| App non si avvia su Wayland | Problema noto con alcune versioni Avalonia + Wayland | Avvia con `DISPLAY=:0 audiobridge` per forzare X11 via XWayland |

**Prompt per l'agente:**
> Rileggi `AudioBridge_Guida_Utente.md`. Verifica che ogni passo della sezione "Installazione su Arch Linux" corrisponda esattamente al comportamento dell'app prodotta negli step precedenti (nomi di pulsanti, percorsi, messaggi). Aggiorna dove necessario. Aggiungi la tabella troubleshooting Linux-specifico. Aggiorna il `README.md` del repo con istruzioni di build per entrambe le piattaforme.

**Verifica:** un utente che legge solo la guida riesce a installare e usare AudioBridge Linux senza consultare altra documentazione.

**Dopo la verifica:**
```bash
git add -A && git commit -m "step9-linux-documentazione-aggiornata: guida utente e README allineati"
```

---

## Struttura repository finale

```
audiobridge/
├── desktop/                          # App Windows (invariata)
│   └── AudioBridge.Desktop/
├── desktop-linux/                    # App Linux (questo porting)
│   └── AudioBridge.Linux/
│       ├── AudioBridge.Linux.csproj
│       ├── Capture/
│       │   ├── IAudioCapture.cs
│       │   ├── LinuxPipeWireCapture.cs   ← implementazione principale
│       │   ├── LinuxPulseAudioCapture.cs ← fallback (Step 5)
│       │   └── LinuxAudioCaptureFactory.cs ← factory (Step 5)
│       ├── Network/                  # identico a Windows
│       ├── ViewModels/               # identico a Windows meno NAudio
│       ├── Views/                    # identico a Windows
│       ├── Controls/                 # identico a Windows
│       ├── Models/                   # identico a Windows
│       ├── Services/                 # identico a Windows
│       ├── Theme.axaml               # identico a Windows
│       └── Styles.axaml              # identico a Windows
├── android/                          # invariato
├── shared/
│   └── PROTOCOL.md                   # invariato — protocollo condiviso
├── scripts/
│   └── build-appimage.sh             # nuovo (Step 8)
├── PKGBUILD                          # nuovo (Step 8)
└── README.md                         # aggiornato (Step 9)
```

---

## Dipendenze NuGet del progetto Linux

Identiche al Windows **meno NAudio**, più nessuna aggiunta:

```xml
<PackageReference Include="Avalonia" Version="11.2.0" />
<PackageReference Include="Avalonia.Desktop" Version="11.2.0" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.0" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.0" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
<PackageReference Include="Concentus" Version="2.2.2" />
<PackageReference Include="Makaretu.Dns.Multicast.New" Version="0.38.0" />
<!-- NAudio RIMOSSO: Windows-only -->
```

La cattura audio usa esclusivamente `System.Diagnostics.Process` (stdlib .NET) per invocare `pw-record`/`parec` — zero dipendenze native aggiuntive.

---

## Note generali per l'agente su tutti gli step

- **Ogni step deve lasciare il progetto compilabile** con `dotnet build`. Mai rompere la build per completarla nello step successivo.
- **Non modificare `desktop/`** (la versione Windows) — sono due codebase separati. Se scopri un bug che riguarda entrambi (es. nel protocollo di rete), segnalalo ma non applicarlo al Windows senza istruzioni esplicite.
- **Il protocollo (`shared/PROTOCOL.md`) è immutabile** in questo porting. L'app Android già in produzione si aspetta esattamente quel formato.
- **Testi UI in italiano**, stesso linguaggio della versione Windows — l'utente deve avere un'esperienza identica sui due desktop.
- **Non aggiungere dipendenze NuGet** non elencate sopra senza spiegazione esplicita.
- **Documenta nel codice** ogni scelta non ovvia relativa a Linux (es. perché si usa `@DEFAULT_MONITOR@`, perché si converte s16 e non si usa direttamente s32/float).
