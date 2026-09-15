# ColdNet Beispiel-Workflows (Process Chains)

Dieses Dokument beschreibt vorgefertigte Beispiel-Prozessketten in ColdNet und deren Aufbau.

---

## 1. Office-zu-PDF Konvertierung mit Beibehaltung der Quelldokumente

### Ziel
Office-Dokumente (`.docx`, `.xlsx`, `.pptx` etc.) aus einem Eingabeverzeichnis automatisch in PDF konvertieren und anschließend **sowohl das konvertierte PDF als auch die Quelldatei** in ein gemeinsames Ausgabeverzeichnis verschieben.

### Konfigurierte Module in der Prozesskette

| Position | Modul | Anzeigename | Beschreibung & Konfiguration |
|---|---|---|---|
| **0** | `ColdImport` | 1. Office-Dokumente importieren | Überwacht das Eingabeverzeichnis (z. B. `data/input/office`), erfasst eingehende Dateien (z. B. `*.docx`) und benennt sie mit dem `$`-Präfix um (z. B. `doc1.$docx`). |
| **1** | `RenameFiles` | 2. Import-Marker entfernen | Benennt die Dateiendung von `$docx` nach `docx` um (`{"FromExtension":"$docx","ToExtension":"docx"}`), damit LibreOffice die Datei mit gültiger Endung verarbeiten kann. |
| **2** | `OfficeToPdf` | 3. Office zu PDF konvertieren | Ruft LibreOffice im Headless-Modus auf und erzeugt `doc1.pdf`.<br>• `FileExtension`: `docx`<br>• `OutputFileExtension`: `pdf`<br>• `DeleteSourceFile`: `false` (Quelldatei wird nicht gelöscht) |
| **3** | `FileMover` | 4. Quelldokument & PDF in Ausgabe verschieben | Verschiebt alle zum Job gehörenden Dateien (`doc1.*`, d. h. sowohl `doc1.docx` als auch `doc1.pdf`) in das Zielverzeichnis (z. B. `data/output/office`).<br>• `OutputDirectory`: `data/output/office`<br>• `OverwriteExisting`: `true` |

---

## 2. Text-zu-PDF Konvertierung

### Ziel
Reine Textdateien (`.txt`, z. B. Spool-Dateien, Protokolle) einlesen, in PDF konvertieren und das Quelldokument zusammen mit dem PDF im Ausgabeverzeichnis ablegen.

### Konfigurierte Module in der Prozesskette

| Position | Modul | Anzeigename | Beschreibung & Konfiguration |
|---|---|---|---|
| **0** | `ColdImport` | 1. Textdateien importieren | Verzeichnis: `data/input/text`, Dateimaske: `*.txt` |
| **1** | `RenameFiles` | 2. Import-Marker entfernen | Von `$txt` nach `txt` (`{"FromExtension":"$txt","ToExtension":"txt"}`) |
| **2** | `TextToPdf` | 3. Text zu PDF konvertieren | Konvertiert Text nach PDF mit konfigurierter Schriftart/Größe.<br>• `FileExtension`: `txt`<br>• `OutputFileExtension`: `pdf`<br>• `DeleteSourceFile`: `false` |
| **3** | `FileMover` | 4. Quelldokument & PDF in Ausgabe verschieben | Verschiebt alle Dateien (`.txt` und `.pdf`) nach `data/output/text`. |

---

## Automatisches Seeding

Die Beispiel-Workflows werden beim ersten Start der Anwendung (`DbInitializer.MigrateAndSeedAsync`) automatisch in der `default`-Prozessgruppe angelegt. Sie können in der Weboberfläche unter **Process groups** direkt eingesehen, angepasst und ausgeführt werden.
