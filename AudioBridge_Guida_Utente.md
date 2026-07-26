# AudioBridge — Guida Utente

Guida a setup e utilizzo di AudioBridge su Windows 10, Arch Linux e Android.

> **Nota importante sulla latenza**: AudioBridge riduce al minimo il ritardo introdotto dalla trasmissione via WiFi, ma il ritardo finale che senti dipende anche dal Bluetooth delle tue cuffie/cassa. Con dispositivi che usano il codec standard SBC (la maggior parte delle cuffie economiche), aspettati un ritardo complessivo percepibile — ottimo per musica e film, meno indicato per giochi che richiedono riflessi molto rapidi.

---

## 1. Requisiti

- **PC**: Windows 10 (build recente) oppure Arch Linux con PipeWire (predefinito nelle installazioni moderne).
- **Telefono**: Android 8.0 (Oreo) o successivo.
- **Rete**: PC e telefono devono essere connessi alla **stessa rete WiFi**. Il collegamento via dati mobili o reti diverse non funziona.
- **Cuffie/cassa Bluetooth**: già accoppiate al telefono in autonomia, tramite le normali impostazioni Bluetooth di Android — AudioBridge non gestisce l'accoppiamento Bluetooth, solo l'invio dell'audio al telefono.

---

## 2. Installazione su Windows 10

1. Scarica il pacchetto di installazione `AudioBridge-Setup.exe` (fornito dal team di sviluppo o dalla release del progetto).
2. Esegui il file ed segui la procedura guidata standard (Avanti → Installa → Fine).
3. Al primo avvio, Windows potrebbe mostrare un avviso del firewall chiedendo se consentire ad AudioBridge l'accesso alla rete: scegli **Consenti accesso** (necessario per trovare il telefono e inviare l'audio).
4. L'app si apre mostrando la schermata principale con lo stato "Non connesso".

---

## 3. Installazione su Arch Linux

**Opzione A — pacchetto AUR (consigliata se disponibile):**
```bash
yay -S audiobridge
# oppure, con altro helper AUR:
paru -S audiobridge
```

**Opzione B — AppImage:**
```bash
chmod +x AudioBridge-x86_64.AppImage
./AudioBridge-x86_64.AppImage
```

**Verifica prerequisiti PipeWire** (di solito già presenti su installazioni recenti):
```bash
pactl info | grep "Server Name"
```
Se il risultato menziona PipeWire, sei a posto. In caso contrario, installa:
```bash
sudo pacman -S pipewire pipewire-pulse pipewire-audio wireplumber
```
e riavvia la sessione.

Se il tuo firewall locale (es. `ufw`, `firewalld`) è attivo, assicurati che le porte usate da AudioBridge siano consentite sulla rete locale (le porte esatte sono visibili e modificabili in **Impostazioni → Rete** dentro l'app, vedi sezione 5).

---

## 4. Installazione su Android

1. Scarica il file `AudioBridge.apk` sul telefono (dal PC via cavo/trasferimento file, oppure direttamente se disponibile un link di download).
2. Se è la prima volta che installi un'app fuori dal Play Store, Android chiederà di abilitare **"Installa app da origini sconosciute"** per il file manager o browser usato: conferma quando richiesto.
3. Apri il file APK e completa l'installazione.
4. Al primo avvio, l'app chiederà i permessi di rete (automatico) e, per la notifica persistente di servizio attivo, il permesso di mostrare notifiche: concedilo, serve per mantenere la connessione stabile anche a schermo spento.

---

## 5. Primo collegamento PC ↔ Telefono

1. Assicurati che PC e telefono siano sulla stessa rete WiFi.
2. Apri AudioBridge sul PC: rimane in ascolto in attesa di una connessione (stato: "In attesa").
3. Apri AudioBridge sul telefono: nella schermata principale, tocca **Cerca dispositivi**. Dopo pochi secondi dovrebbe comparire il nome del tuo PC nella lista.
4. Tocca il nome del PC per connetterti. L'indicatore di connessione (il cerchio pulsante) diventa verde su entrambi i dispositivi quando il collegamento è stabile.

**Se il PC non compare nella lista:**
- Verifica che entrambi i dispositivi siano sulla stessa rete (non su reti WiFi "ospiti" separate, comune in alcuni router che isolano gli ospiti dalla rete principale).
- Prova il collegamento manuale: sul telefono, tocca **Inserisci IP manualmente** e digita l'indirizzo IP del PC (visibile in **Impostazioni → Rete** nell'app desktop).
- Controlla che il firewall del PC non stia bloccando l'app (vedi sezioni 2 e 3).

---

## 6. Uso quotidiano

1. Collega le cuffie/cassa Bluetooth al telefono come faresti normalmente (Impostazioni Android → Bluetooth).
2. Apri AudioBridge sia sul PC sia sul telefono; se erano già stati collegati in precedenza sulla stessa rete, la connessione si ristabilisce da sola.
3. Sul PC, scegli un profilo dalla schermata principale in base a cosa stai facendo:
   - **Musica** — qualità audio più alta, ideale per ascolto attento.
   - **Film** — bilanciato tra qualità e sincronizzazione con il labiale.
   - **Gaming** — ritardo minimo possibile, qualità leggermente ridotta.
4. Premi **Avvia trasmissione**. L'audio del PC ora viene inviato al telefono e da lì alle tue cuffie Bluetooth.
5. Per interrompere, premi **Interrompi trasmissione** oppure chiudi semplicemente l'app (l'audio tornerà agli altoparlanti/uscita normale del PC).

---

## 7. Impostazioni avanzate (opzionali)

Nel pannello **Impostazioni** dell'app desktop puoi regolare manualmente, se i preset non ti soddisfano:

- **Audio**: sorgente da catturare, qualità (bitrate), dimensione del buffer audio.
- **Rete**: porta di comunicazione, buffer di rete, ricerca automatica del telefono on/off.
- **Avanzate**: dimensione del buffer di ricezione, avvio automatico di AudioBridge con il PC, riduzione a icona nella barra delle applicazioni.

**Consiglio pratico**: se senti micro-interruzioni nell'audio, prova ad aumentare leggermente il buffer di rete o quello audio (Impostazioni → Audio/Rete) — a scapito di un minimo di ritardo aggiuntivo, ma con più stabilità.

Sull'app Android, in **Impostazioni**, puoi regolare il buffer di ricezione (jitter buffer): utile se la tua rete WiFi non è molto stabile.

---

## 8. Risoluzione problemi comuni

| Problema | Possibile causa | Soluzione |
|---|---|---|
| Il telefono non trova il PC | Reti WiFi diverse o rete con isolamento client | Verifica stessa rete; usa IP manuale |
| Audio con scatti/interruzioni | Buffer troppo basso per la tua rete | Aumenta buffer di rete/audio nelle impostazioni |
| Ritardo troppo alto per giocare | Limite del Bluetooth delle cuffie, non risolvibile via software | Prova preset Gaming; valuta cuffie con codec a bassa latenza (aptX LL) se il ritardo resta un problema |
| L'app Android si disconnette quando lo schermo si spegne | Risparmio energetico del telefono chiude il servizio | Attiva "Mantieni riproduzione in background" nelle impostazioni Android e verifica che il risparmio energetico di sistema non stia escludendo l'app dalle ottimizzazioni batteria |
| Connessione persa quando cambio stanza (WiFi mesh) | Il telefono ha cambiato punto di accesso nella rete mesh | La riconnessione dovrebbe avvenire automaticamente entro pochi secondi; se non accade, chiudi e riapri l'app Android |

---

## 9. Domande frequenti

**Posso collegare più telefoni contemporaneamente allo stesso PC?**
Non nella versione base del progetto — è pensato per un collegamento PC-telefono alla volta.

**Funziona anche fuori casa, senza WiFi?**
No, richiede che entrambi i dispositivi siano sulla stessa rete locale.

**Consuma molta batteria sul telefono?**
Lo streaming audio in background ha un consumo contenuto ma continuo, paragonabile a una normale riproduzione musicale via Bluetooth.
