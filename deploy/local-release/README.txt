ColdNet - lokale Ausfuehrung (Windows x64)
============================================

Enthalten:
  Admin\      - Weboberflaeche (Prozessgruppen, Ketten, Module, Jobs)
  Worker\     - Hintergrunddienst, der die Prozessketten tatsaechlich abarbeitet
  SecretTool\ - Notfall-Kommandozeilentool zum Entschluesseln gespeicherter Kennwoerter
  Start-Admin.bat
  Start-Worker.bat

Dieses Paket ist "self-contained": es enthaelt die komplette .NET-Runtime.
Es muss nichts zusaetzlich installiert werden - einfach entpacken und starten.

Erste Schritte:
  1. Start-Admin.bat doppelklicken, warten bis "Application started" erscheint,
     dann im Browser http://localhost:5202 oeffnen.
  2. Dort mindestens eine Prozessgruppe/-kette anlegen und konfigurieren
     (siehe die Projekt-README fuer Details zu den Modulen).
  3. Start-Worker.bat doppelklicken, damit Prozessketten automatisch im
     Hintergrund abgearbeitet werden. Alternativ kann man im Admin auch
     manuell ueber den Button "Run now" pro Kette einen Durchlauf ausloesen,
     ohne den Worker zu starten.

Admin und Worker teilen sich eine gemeinsame SQLite-Datenbank in einem
"data"-Ordner, der beim ersten Start automatisch neben diesem README
angelegt wird.

Hinweis zu externen Abhaengigkeiten einzelner Module:
  - "Office to PDF" (LibreOffice headless) benoetigt eine separat
    installierte LibreOffice-Installation - "soffice.exe" muss im PATH
    liegen oder im Modul als voller Pfad konfiguriert werden.
  - "PDF to PDF/A" benoetigt eine separat installierte Ghostscript-
    Installation (kostenlos, auch fuer kommerzielle Nutzung -
    https://www.ghostscript.com) sowie ein RGB-ICC-Farbprofil; beides wird
    im Modul konfiguriert ("Ghostscript Path" bzw. "Icc Profile Path" -
    Ghostscript bringt unter "<Installationsordner>\iccprofiles\srgb.icc"
    bereits ein passendes Profil mit).
  - "Extract Text" benoetigt fuer Bilddateien (nicht fuer PDFs mit
    Textebene) eine separat installierte Tesseract-OCR-Installation
    (kostenlos, auch fuer kommerzielle Nutzung -
    https://github.com/tesseract-ocr/tesseract) inkl. der benoetigten
    Sprachpakete (z.B. "deu" fuer Deutsch) - "tesseract.exe" muss im PATH
    liegen oder im Modul als voller Pfad konfiguriert werden ("Tesseract
    Path").
  Alle anderen Module (inkl. Grafik-/Barcode-Module wie ConvertGraphic,
  MultiPageTiff, BarcodeSplit) sind in diesem self-contained Paket bereits
  enthalten und benoetigen keine weitere Installation.

Kennwoerter (z.B. bei SFTP-Modulen) werden in der Datenbank ausschliesslich
verschluesselt gespeichert und im Admin nur maskiert angezeigt. Falls ein
Kennwort im Notfall trotzdem im Klartext gebraucht wird (z.B. ohne Zugriff
auf das Admin-Webinterface), oeffnet man eine Kommandozeile im SecretTool-
Ordner und ruft dort z.B. "ColdNet.SecretTool.exe list-modules" bzw.
"ColdNet.SecretTool.exe decrypt-module <id>" auf. Siehe docs/ENCRYPTION.md
in der Projekt-Dokumentation fuer Details.

Vollstaendige Dokumentation: https://github.com/akrauter/ColdNet
