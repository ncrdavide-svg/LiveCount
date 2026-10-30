# LiveCount — contesto per chi riprende il lavoro (persona o AI)

Questo file serve a continuare lo sviluppo da un'altra postazione o con un altro assistente AI.
Leggilo tutto prima di modificare il codice. Lingua di lavoro con l'utente: **italiano**.

## Cos'è

LiveCount è un timer countdown per eventi dal vivo, per Windows, distribuito come **un unico `.exe` senza installazione**.

- **Regia** (finestra di controllo) sul monitor principale.
- **Display** a schermo pieno sul monitor secondario (si apre da solo all'avvio).
- Barra di avanzamento + numeri: verde, arancio sotto il 10%, rosso sotto il 5% (soglie e colori configurabili). Dopo lo zero conta in positivo (`+00:01`) in rosso.
- Preset con nome, messaggio sotto il timer (F9), font/colori/dimensioni configurabili.
- Video di sfondo da **NDI** o da **scheda USB** (Magewell, AJA U-TAP, webcam: dispositivi UVC) con il timer in un riquadro sovrapposto, trascinabile da un monitor dal vivo nella regia.
- **Controllo remoto HTTP** per Bitfocus Companion / Stream Deck, con esportazione automatica di una pagina `.companionconfig`.
- Finestra "Ti piace LiveCount?" con QR code Buy Me a Coffee a ogni avvio.

App "sorella": **LiveCue** (clone di QLab), repository `ncrdavide-svg/LiveCue`. LiveCount ne riprende nome (prefisso *Live*), stile del logo e finestra del caffè.

## Repository e distribuzione

- GitHub: `ncrdavide-svg/LiveCount` — **privato** finché l'utente non chiede di renderlo pubblico.
- L'exe **non** sta nel repository: si pubblica come asset delle **Release** (`gh release create vX.Y.Z LiveCount.exe ...`).
- Link stabile al download, usato nel README: `https://github.com/ncrdavide-svg/LiveCount/releases/latest/download/LiveCount.exe` (funziona per tutti solo con repo pubblico).
- Identità Git locale del progetto: `ncrdavide <ncrdavide@gmail.com>` (la stessa di LiveCue).

## Preferenze dell'utente (importanti)

- **Non citare il nome dell'utente** nell'app né nei testi pubblici. I contatti sono solo il link "GitHub" (`https://github.com/ncrdavide-svg`) sotto la nota sul marchio NDI.
- **Non chiudere né riavviare LiveCount senza chiedere**: l'utente lo usa mentre si sviluppa. Per verificare il codice compila in una cartella temporanea (vedi "Verifica"); chiedi prima di sovrascrivere `LiveCount.exe`.
- Prima di modificare file di configurazione dell'utente (impostazioni, NDI Webcam…) fai una copia e ripristinala dopo i test.
- Interfaccia e messaggi in italiano.

## Vincoli tecnici (non cambiarli senza motivo)

- **C# 5** compilato con il `csc.exe` di **.NET Framework 4** incluso in Windows (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`). Niente SDK .NET, niente NuGet, niente pacchetti esterni.
  - Quindi niente: interpolazione `$""`, `?.`, `nameof`, membri `=>`, `out var`, inizializzatori di auto-property.
- **WinForms**, piattaforma **x64**, processo DPI-aware (`SetProcessDPIAware`), layout a coordinate fisse con `AutoScaleMode.Dpi`.
- Un solo file eseguibile: risorse (QR code, icona, libreria NDI) sono **incorporate** nell'exe.

## Compilare

```bat
build.bat
```

- Produce `LiveCount.exe` nella cartella del progetto.
- Se trova la libreria NDI (`lib\Processing.NDI.Lib.x64.dll`, oppure la copia da `NDI_RUNTIME_DIR_V6` / `C:\Program Files\NDI\NDI 6 Tools\Runtime`) la incorpora come risorsa `ndi.x64.dll`. Senza, l'exe funziona lo stesso e usa il runtime NDI installato se presente.
- `lib/` è in `.gitignore`: su una nuova postazione installa **NDI Tools** (gratuito) prima di compilare, altrimenti l'exe non avrà NDI incorporato.
- Icona: `python tools/make_icon.py` (richiede Pillow) rigenera `assets/LiveCount.ico` e le anteprime PNG.

## Struttura del codice (`src/`)

| File | Contenuto |
|---|---|
| `Program.cs` | `Main`, icona app, `Settings` (XML, salvato accanto all'exe o in `%APPDATA%\LiveCount`; migra il vecchio `TimerCountdown.settings.xml`), `Preset`, `TimerEngine` (basato su `Stopwatch`, niente deriva). |
| `Renderer.cs` | Disegna barra, timer e messaggio. Usato sia dal display sia dalle anteprime (l'anteprima mostra esattamente ciò che va in onda). Modalità a tutto schermo o "Overlay" (riquadro sopra il video). Centratura sulle cifre, riduzione automatica se il testo non entra. |
| `Views.cs` | `DisplayForm` (uscita a schermo pieno) e `PreviewControl` (monitor nella regia: trascina il riquadro, angolo giallo per ridimensionare, rotellina per scalare). |
| `ControlForm.cs` | La regia: tutta l'interfaccia, preset, NDI/USB, monitor, API remota, esportazione Companion, scorciatoie F5/F8/F9. |
| `Video.cs` | `VideoSource` astratta (thread di ricezione, doppio buffer BGRX, disegno con `StretchDIBits`) + interop GDI. |
| `Ndi.cs` | Interop NDI (P/Invoke), `NdiLoader` (estrae la DLL incorporata in `%LOCALAPPDATA%\LiveCount\ndi-<hash>` e la carica), `NdiReceiver`. |
| `UsbCapture.cs` | Acquisizione UVC con **Media Foundation** via COM interop (source reader, uscita RGB32 con advanced video processing). Le interfacce COM dichiarano metodi segnaposto per rispettare l'ordine della vtable. |
| `RemoteApi.cs` | Server HTTP minimale su `TcpListener` (porta predefinita 8787, ascolta su tutte le interfacce, nessun permesso di amministratore). |
| `CompanionExport.cs` | Genera la pagina `.companionconfig` per Companion. |
| `SupportForm.cs` | Finestra Buy Me a Coffee (giallo `#FFDD00`, link `buymeacoffee.com/cin8`, "No, grazie" attivo dopo 3 s). |

Asset in `assets/`: `LiveCount.ico`/`.png` (logo), `BuyMeACoffeeQR.png` (copiato da LiveCue).

## API remota

GET su `http://<pc>:8787/api/...`, risposta JSON con lo stato (`text`, `remaining_ms`, `total_ms`, `running`, `phase`, `message_visible`).
Comandi: `start`, `pause`, `toggle`, `reset`, `add?min=N` (negativo per togliere), `preset?name=…` / `preset?index=N`, `message/show|hide|toggle`, `display/show|hide`, `status`.
I comandi arrivano su un thread del server e vengono eseguiti sul thread UI con `Invoke`.

## Companion: note sul formato del file

- Il file è una pagina in formato **export versione 9** (`"type": "page"`); Companion (testato sul codice sorgente della 5.1.x) lo aggiorna da solo con i passaggi "legacy" fino alla versione attuale e poi lo convalida con uno schema.
- Ogni tasto usa il modulo **`generic-http`**, azione `get`, connessione `livecount_http` con `config.prefix` = `http://<host>:<porta>/api/`.
- Le **opzioni delle azioni vanno scritte come `{ "isExpression": false, "value": ... }`**: i passaggi legacy non convertono le opzioni dei moduli esterni e lo schema finale le richiede così.
- `lastUpgradeIndex` della connessione e `upgradeIndex` delle azioni = 3 (il modulo generic-http ha 4 script di aggiornamento: indici 0–3).
- Fonti usate: `bitfocus/companion` (`shared-lib/lib/Model/ExportModel.ts`, `companion/lib/Upgrades/Legacy/*`, fixture `companion/test/Upgrades/Exports/v9-full-contour-shuttle.json`, schema `shared-lib/lib/Model/Export/__tests__/snapshots/v17.json`) e `bitfocus/companion-module-generic-http` (`src/index.js`, `src/fields.js`, `src/upgrade.js`).

## Verifica (come è stato testato finora)

- Compilazione di prova senza toccare l'exe in uso: stesso comando di `build.bat` con `/out:` in una cartella temporanea.
- Componenti provati caricando l'exe da PowerShell (`[Reflection.Assembly]::LoadFrom`) e chiamando le classi (es. `UsbCapture.ListDevices()`, `CompanionExport.Build(...)`), oppure catturando lo schermo con `Graphics.CopyFromScreen`.
- NDI provato con un piccolo trasmettitore NDI di prova (barre colorate); USB provato con la webcam virtuale "NDI Webcam Video" di NDI Tools.
- API provata con `Invoke-WebRequest` su `127.0.0.1:8787`: tutti i comandi rispondono correttamente.

## Stato e cose aperte

- [ ] **Importazione in un Companion vero non ancora verificata** (Companion non era installato sulla postazione di sviluppo). Se dà errore, partire dal messaggio di Companion e confrontare con lo schema v17.
- [ ] Mai provato su un PC "pulito" senza NDI Tools (le dipendenze della DLL NDI sono solo DLL di sistema, verificato).
- [ ] Mai provata una scheda Magewell / U-TAP reale (solo la webcam virtuale NDI).
- [ ] Da decidere con l'utente: RESET riporta il tempo **comprensivo** dei minuti aggiunti/tolti, non il valore iniziale del preset.
- [ ] Prima di rendere pubblico il repo: verificare i termini di redistribuzione della licenza NDI SDK (la DLL è incorporata nell'exe; la dicitura sul marchio è già presente).
- [ ] L'exe non è firmato: SmartScreen mostra un avviso al primo avvio.
- Idee non richieste ma possibili: tasto Companion che mostra il tempo residuo (via `/api/status`), modulo Companion dedicato invece di Generic HTTP.
