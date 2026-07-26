# AudioBridge - Protocollo di Rete

> **Versione:** 1.0 (bozza)
> **Stato:** In definizione — in validazione durante Step 5.

---

## 1. Panoramica

AudioBridge trasmette audio di sistema (loopback) da un **sender desktop** (Windows/Linux) a un **receiver Android** via rete locale Wi-Fi.

Architettura a due canali:
| Canale | Protocollo | Porta default | Scopo |
|--------|------------|---------------|-------|
| **Control** | TCP | `54321` | Handshake, keep-alive, comandi, riconnessione |
| **Data** | UDP | `54322` | Stream audio Opus frame-by-frame |
| **Discovery** | mDNS | `5353` (`_audiobridge._tcp.local`) | Annuncio/ricerca dispositivo automatica |

Tutte le porte sono **configurabili** lato utente (settings desktop + Android).

---

## 2. Byte Order (Endianness)

**Little-endian** per tutti i campi numerici multi-byte (uint16, uint32, uint64, float).
Rationale: architetture x86/ARM sono little-endian nativo → zero copy, niente conversioni.

---

## 3. Canale Dati (UDP) — Formato Pacchetto

Ogni pacchetto UDP = **Header fisso (18 byte)** + **Payload Opus (variabile, ≤ MTU)**.

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    MAGIC BYTES (0xAB, 0xCD)                   |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                      SEQUENCE NUMBER (u32)                    |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                    TIMESTAMP NTP (u64, ms)                    |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|  FLAGS (u8)   |   RESERVED (u8)  |   PAYLOAD LEN (u16)        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                         PAYLOAD OPUS ...                      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

| Campo | Offset | Size | Tipo | Descrizione |
|-------|--------|------|------|-------------|
| `magic` | 0 | 2 byte | `uint16` | Costante `0xCDAB` (little-endian: `0xAB 0xCD` on wire). Identifica pacchetti AudioBridge. |
| `sequence` | 2 | 4 byte | `uint32` | Numero di sequenza monotono, **wrapping a 2³²**. Incrementato di 1 per ogni frame Opus inviato. |
| `timestamp_ntp` | 6 | 8 byte | `uint64` | Timestamp NTP (millisecondi dall'epoch 1900) del **primo campione** del frame Opus. Usato per sincronizzazione e stima latenza. |
| `flags` | 14 | 1 byte | `uint8` | Bitfield (vedi sotto). |
| `reserved` | 15 | 1 byte | `uint8` | Riservato, deve essere `0x00`. |
| `payload_len` | 16 | 2 byte | `uint16` | Lunghezza payload Opus in byte (max 1200 per stare in MTU ~1280). |
| `payload` | 18 | `payload_len` | `bytes` | Frame Opus compresso (un frame per pacchetto). |

### Flags (bitfield, `uint8`)

| Bit | Nome | Significato |
|-----|------|-------------|
| 0 (LSB) | `KEYFRAME` | `1` = primo frame dopo handshake / keyframe Opus (utile per sync rapido). |
| 1 | `SILENCE` | `1` = frame di silenzio (DTX), payload può essere vuoto o minimo. |
| 2 | `CONFIG_CHANGE` | `1` = parametri stream cambiati (sample rate, canali, bitrate). Receiver deve ri-inizializzare decoder. |
| 3-7 | `RESERVED` | Sempre `0`. |

> **Nota MTU**: Payload Opus max consigliato **1200 byte** (lascia margine per header UDP/IP). Frame size Opus configurabile 5–60 ms → a 48 kHz stereo 256 kbps, frame 20 ms ≈ 640 byte. Ben dentro MTU.

---

## 4. Canale Controllo (TCP) — Messaggi JSON Lines

Connessione TCP persistente. Messaggi = **JSON + newline (`\n`)**. Nessun framing binario.

### 4.1 Handshake Iniziale (Client → Server)

Subito dopo `connect()`, il **client Android** invia:

```json
{
  "type": "HELLO",
  "version": 1,
  "client_name": "Pixel 8",
  "client_id": "android-<uuid-v4>",
  "capabilities": {
    "opus": true,
    "max_bitrate": 320000,
    "sample_rates": [44100, 48000],
    "channels": [1, 2],
    "frame_sizes_ms": [5, 10, 20, 40, 60]
  }
}
```

Il **server desktop** risponde con:

```json
{
  "type": "WELCOME",
  "version": 1,
  "server_name": "FEDERICO-PC",
  "server_id": "desktop-<uuid-v4>",
  "session_id": "<uuid-v4>",
  "negotiated": {
    "sample_rate": 48000,
    "channels": 2,
    "bitrate": 256000,
    "frame_size_ms": 20,
    "udp_port": 54322
  }
}
```

> `session_id` identifica univocamente questa sessione di streaming. Usato nei log e per debug.

Se parametri non compatibili → server chiude connessione con `ERROR` (vedi sotto).

### 4.2 Keep-Alive (bidirezionale)

Ogni **3 secondi** ciascun lato invia:

```json
{"type":"PING","ts":1721654321123}
```

L'altro lato risponde entro **200 ms**:

```json
{"type":"PONG","ts":1721654321123,"rtt_ms":1.2}
```

- `ts` = timestamp NTP (ms) del momento invio PING.
- `rtt_ms` = round-trip time misurato dal risponditore (opzionale, per UI latenza).

Se **3 PING consecutivi senza PONG** → considera connessione morta, chiudi socket TCP e UDP, torna in stato *disconnesso*.

**Timeout assoluto**: 10 secondi dall'ultimo PONG ricevuto. Se non arriva alcun PONG entro 10 secondi, la connessione viene chiusa immediatamente indipendentemente dal numero di PING inviati.

### 4.3 Comandi di Controllo (Client → Server)

```json
{"type":"PAUSE"}           // Metti in pausa invio UDP
{"type":"RESUME"}          // Riprendi invio UDP
{"type":"SET_BITRATE","bps":192000}
{"type":"SET_FRAME_SIZE","ms":10}
{"type":"GET_STATS"}       // Richiedi statistiche correnti
```

Risposta server per `GET_STATS`:

```json
{
  "type":"STATS",
  "packets_sent": 12345,
  "bytes_sent": 7890123,
  "packets_lost_estimated": 12,
  "current_bitrate_bps": 255000,
  "avg_rtt_ms": 14.3
}
```

### 4.4 Notifiche Server → Client

```json
{"type":"STREAM_START"}           // Inizio invio dati UDP
{"type":"STREAM_STOP","reason":"user_paused"}  // Fine stream
{"type":"CONFIG_CHANGED","negotiated":{...}}   // Parametri rinegoziati
{"type":"ERROR","code":"UDP_PORT_BUSY","message":"Porta 54322 già in uso"}
```

### 4.5 Error Codes

| Code | Significato |
|------|-------------|
| `PROTOCOL_VERSION` | Versione protocollo non supportata |
| `INCOMPATIBLE_PARAMS` | Parametri audio non negoziabili |
| `UDP_PORT_BUSY` | Porta dati UDP non bindabile |
| `AUDIO_CAPTURE_FAILED` | Impossibile aprire device loopback |
| `INTERNAL` | Errore generico |

---

## 5. Discovery mDNS

- **Service type**: `_audiobridge._tcp.local`
- **Instance name**: `<server_name>` (es. `FEDERICO-PC`)
- **TXT records**:
  - `version=1`
  - `control_port=54321`
  - `data_port=54322`
  - `server_id=desktop-<uuid>`
  - `name=FEDERICO-PC`

Android usa `NsdManager` per discoverare; fallback IP manuale sempre disponibile.

---

## 6. Parametri Opus (Negoziazione)

| Parametro | Valori ammessi | Default |
|-----------|----------------|---------|
| Sample rate | 48000, 44100 | 48000 |
| Canali | 1 (mono), 2 (stereo) | 2 |
| Bitrate | 64–320 kbps | 256 kbps |
| Frame size | 5, 10, 20, 40, 60 ms | 20 ms |
| Complexity | 0–10 | 5 |
| Signal type | AUTO, MUSIC, VOICE | AUTO |
| DTX | on/off | on |
| FEC | on/off | off (v1) |

---

## 7. Stima Latenza End-to-End (UI)

- `timestamp_ntp` in ogni pacchetto UDP = tempo cattura primo campione.
- Receiver calcola `now_ntp - timestamp_ntp` = **latency_ms** (include rete + jitter buffer + decode + render).
- Keep-alive `PONG.rtt_ms` = RTT puro TCP (network only).
- UI mostra `latency_ms` con colore: verde `<50`, giallo `50-100`, arancione `>100`.

---

## 8. Sicurezza (v1: none, pianificato v2)

- Nessuna crittografia né autenticazione in v1 (rete locale trusted).
- v2: DTLS 1.3 su UDP, TLS 1.3 su TCP, pairing QR-code.

---

## 9. Changelog

| Versione | Data | Autore | Note |
|----------|------|--------|------|
| 1.0 | 2026-07-22 | — | Bozza iniziale per Step 4 |