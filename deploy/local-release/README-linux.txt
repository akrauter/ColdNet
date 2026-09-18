ColdNet - lokale Ausfuehrung (Linux x64)
============================================

Enthalten:
  Admin/      - Weboberflaeche (Prozessgruppen, Ketten, Module, Jobs)
  Worker/     - Hintergrunddienst, der die Prozessketten tatsaechlich abarbeitet
  SecretTool/ - Notfall-Kommandozeilentool zum Entschluesseln gespeicherter Kennwoerter
  start-admin.sh
  start-worker.sh

Dieses Paket ist "self-contained": es enthaelt die komplette .NET-Runtime.
Es muss nichts zusaetzlich installiert werden - einfach entpacken und starten
(die Start-Skripte sind bereits ausfuehrbar).

Erste Schritte:
  1. ./start-admin.sh ausfuehren, warten bis "Application started" erscheint,
     dann im Browser http://localhost:5202 oeffnen.
  2. Dort mindestens eine Prozessgruppe/-kette anlegen und konfigurieren
     (siehe die Projekt-README fuer Details zu den Modulen).
  3. ./start-worker.sh ausfuehren, damit Prozessketten automatisch im
     Hintergrund abgearbeitet werden. Alternativ kann man im Admin auch
     manuell ueber den Button "Run now" pro Kette einen Durchlauf ausloesen,
     ohne den Worker zu starten.

Admin und Worker teilen sich eine gemeinsame SQLite-Datenbank in einem
"data"-Ordner, der beim ersten Start automatisch neben diesem README
angelegt wird.

Hinweise zu externen Abhaengigkeiten einzelner Module:
  - "Text to PDF" braucht eine installierte TTF-Schriftart (z.B. das Paket
    "fonts-dejavu-core"); ohne jede Systemschriftart schlaegt das Modul mit
    einer klaren Fehlermeldung fehl statt eine unlesbare PDF zu erzeugen.
  - "Office to PDF" (LibreOffice headless) braucht eine separat installierte
    LibreOffice-Installation - "soffice" muss im PATH liegen oder im Modul
    als voller Pfad konfiguriert werden (z.B. "apt install libreoffice-nogui").
  - "PDF to PDF/A" braucht eine separat installierte Ghostscript-Installation
    (kostenlos, auch fuer kommerzielle Nutzung - "apt install ghostscript")
    sowie ein RGB-ICC-Farbprofil, z.B. aus dem Paket "icc-profiles-free"
    ("apt install icc-profiles-free", Profil dann unter
    "/usr/share/color/icc/sRGB.icc") - Pfade werden im Modul konfiguriert
    ("Ghostscript Path" bzw. "Icc Profile Path").
  - "Extract Text" braucht fuer Bilddateien (nicht fuer PDFs mit Textebene)
    eine separat installierte Tesseract-OCR-Installation samt Sprachpaketen
    (kostenlos, auch fuer kommerzielle Nutzung - z.B.
    "apt install tesseract-ocr tesseract-ocr-deu") - "tesseract" muss im
    PATH liegen oder im Modul als voller Pfad konfiguriert werden
    ("Tesseract Path").
  Alle anderen Module (inkl. Grafik-/Barcode-Module wie ConvertGraphic,
  MultiPageTiff, BarcodeSplit - auf Magick.NET, nicht GDI+) sind in diesem
  self-contained Paket bereits enthalten.

Modul-Signatur: Alle Module (ColdNet.Modules.dll, ColdNet.EdmVault.dll und alles im
Ordner "plugins") muessen eine gueltige Signatur (<name>.dll.sig) tragen. Der Herausgeber
signiert mit seinem privaten Schluessel; die Anwendung kennt nur das oeffentliche Zertifikat
(module-signing.cer, liegt in Admin und Worker bei) und startet bei einer fehlenden oder
ungueltigen Signatur nicht. Dieses Paket ist bereits signiert. Eigene Plugin-Module (DLL plus
.sig) kommen in den Ordner "plugins" neben der Anwendung - Details in docs/MODULE-SIGNING.md
des Projekts (Signieren mit ColdNet.SignTool, Schluesselwechsel, Grenzen des Verfahrens).

Kennwoerter (z.B. bei SFTP-Modulen) werden in der Datenbank ausschliesslich
verschluesselt gespeichert und im Admin nur maskiert angezeigt. Falls ein
Kennwort im Notfall trotzdem im Klartext gebraucht wird (z.B. ohne Zugriff
auf das Admin-Webinterface), oeffnet man eine Kommandozeile im SecretTool-
Ordner und ruft dort z.B. "./ColdNet.SecretTool list-modules" bzw.
"./ColdNet.SecretTool decrypt-module <id>" auf. Siehe docs/ENCRYPTION.md
in der Projekt-Dokumentation fuer Details.

Vollstaendige Dokumentation: https://github.com/akrauter/ColdNet
