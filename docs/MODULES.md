# Module catalogue

ColdNet's module categories mirror d.cold's own module chapters 1:1, so this table can be read
side-by-side with the d.cold manual. "Status" is `✅ implemented` (a working ColdNet module
exists), or `— not implemented` (no ColdNet module yet; add one following the pattern in
[Adding a module](../README.md#adding-a-module) if you need it).

The live version of this table - generated straight from `[ModuleDefinition]` attributes - is
always available in the running app at **/modules**.

## Import modules

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCIMPORT | `ColdImport` | ✅ implemented |
| — (no d.cold equivalent) | `EdmVaultImport` | ✅ implemented - pulls new files from an EDMVault project via the REST API, the reverse of `EdmVaultExport`. See [README § EDMVault connectors](../README.md#edmvault-connectors). |
| — (no d.cold equivalent) | `SftpImport` | ✅ implemented - pulls new files from a remote SFTP/FTPS/FTP server. See [Remote transfer](#remote-transfer-coldnet-specific---no-direct-dcold-category) below. |

## Text converters

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCREPLACE, DCCHANGE | `TextReplace` | ✅ implemented |
| DCENCODETXT, DCSETBOM | `EncodeText` | ✅ implemented |
| DCTXT2PDF | `TextToPdf` | ✅ implemented |
| DCCHANGEBIN, DCCONVTEXT, DCDELFIRSTEMPTY, DCDELLASTLINES, DCDELLINES, DCDOS2WIN, DCGETLINES, DCHEADER, DCINSERTCC, DCMASKFORD3, DCRTF2TXT, DCSPLITLINES, DCTABULATOR, DCTXT2TIF, DCUNICODE2TXT, DCUNIX2WIN, DCWIN2UNIX | — | not implemented |

## XML

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCCONVXML | `PropertiesToXml` | ✅ implemented |
| DCATER2XML | — | not implemented |

## Tools

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCNOP | `NoOp` | ✅ implemented |
| DCEXECUTE | `ShellExecute` | ✅ implemented |
| DCWAITCONFIG, DCWAITXTIMES | — | not implemented |

## Job separation

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCBARCODE, DCJOBSEP | `BarcodeSplit` | ✅ implemented |
| DCAFPSPLIT, DCFILESPLITTER, DCFORMULAR, DCHL72XML, DCJOBSEPEXT, DCJOBSEPPDF, DCOCRSPLIT, DCSPLITAFTERX, DCSPLITMAIL | — | not implemented |

## Extracting properties

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCSETVAR, DCSETCONST | `SetVariable` | ✅ implemented |
| DCGETID | `GenerateId` | ✅ implemented |
| DCPARSE, DCEXTRACTLINE, DCVARFRTXT, DCVARAFTERKW | `ParseProperties` | ✅ implemented (regex-rule based, covers the common cases of all four) |
| DCCHANGEPOS, DCCOUNTTIF, DCDBQUERY, DCGETBARCODES, DCGETFILEATTR, DCGETFILEHASH, DCGETIPTC, DCGODCLASSIFY, DCMIXVAR, DCOCR, DCOCRTEXT, DCPDF2TXT, DCSINGLE2MULTIVAR, DCTXT2JPL, DCVARFRPDF | — | not implemented (OCR modules need a Tesseract/ABBYY wrapper - see below) |

## Hosts

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCAS42PCL, DCAS42TXT, DCAS4FETCHER, DCBS22TXT | — | not implemented - extension point (AS/400-specific, needs real target-system access to build/test against). For generic remote file pickup/delivery (not AS/400-specific), see `SftpImport`/`SftpExport` under [Remote transfer](#remote-transfer-coldnet-specific---no-direct-dcold-category) below. |

## ERP

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCSAPFC, DCSAPFCATT, DCSAPSENDBC, DCTOA01 | — | not implemented - extension point (SAP-specific, needs an SAP connector/RFC library) |

## Graphics converters

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCCONVGRAFIC | `ConvertGraphic` | ✅ implemented (TIFF/PNG/JPEG/BMP/GIF via Magick.NET, Apache-2.0, cross-platform) |
| DCMULTIPAGE, DCMULTIPAGEADV, DCMULTIPAGEEXT | `MultiPageTiff` | ✅ implemented |
| DCPDFCONCAT, DCAPPENDPDF | `PdfConcat` | ✅ implemented |
| DCOFFICE2PDF | `OfficeToPdf` | ✅ implemented (via LibreOffice headless instead of MS Office automation) |
| DCADDTIF, DCAFP2TIF, DCDELPDFPAGES, DCDELTIFPAGES, DCEASYTIF, DCEASYTIF2, DCGETFORMAT, DCHGLAVTIF, DCMAKETIFDIN, DCOPTFORMAT, DCPCL2PDF, DCPCL2TIF, DCPCL2TXT, DCPDF2PDF, DCPDF2TIF, DCPDFAVALIDATE, DCPDFSEARCHABLE, DCPRESCRIBE2TXT, DCPRINTGL, DCPS2PDF, DCPS2TIFCOL, DCPS2TIF, DCPSPDF2TXT, DCREDLINE, DCROTATETIF, DCSHELLEXEC, DCSHELLPDF, DCTIF2DIN, DCTIF2PDF, DCTIFTIF, DCZUGFERD | — | not implemented (most need Ghostscript for PS/PCL; use `ShellExecute` to wrap Ghostscript/an external tool directly in the meantime) |

## Compression

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCUNPACK | `UnpackArchive` | ✅ implemented (ZIP only) |
| DCTXT2LSX, DCPDFATTACHMENT | — | not implemented |

## Case

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCCASE | `CaseBranch` | ✅ implemented - full category coverage |

## File handling

| d.cold module | ColdNet module | Status |
|---|---|---|
| DCFILEMOVER | `FileMover` | ✅ implemented |
| DCDELFILES | `DeleteFiles` | ✅ implemented |
| DCRENFILES | `RenameFiles` | ✅ implemented - full category coverage |

## DMS export (ColdNet-specific - no direct d.cold category)

d.cold hands finished files to `d.3 hostimport` implicitly, via the "Mask for d.3" checkbox on
whichever module runs last. ColdNet makes that hand-off an explicit final pipeline step instead:

| ColdNet module | Status |
|---|---|
| `EdmVaultExport` | ✅ implemented - file-drop or live REST connector, see [README § EDMVault connectors](../README.md#edmvault-connectors) |

## Remote transfer (ColdNet-specific - no direct d.cold category)

Generic network file transfer, independent of any specific host system (contrast with the
AS/400-specific `Hosts` category above). SFTP is recommended; FTPS (explicit TLS) and plain FTP
are supported for legacy servers. Built on SSH.NET and FluentFTP (both MIT, both pure-managed -
no native dependencies, so these work on Linux/Docker the same as everywhere else):

| ColdNet module | Status |
|---|---|
| `SftpImport` | ✅ implemented - lists a remote directory and downloads new files as jobs, the remote counterpart of `ColdImport`. Since a remote server has no equivalent of the local "$"-prefix rename trick, already-downloaded files are renamed (default) or deleted remotely so they aren't re-imported. |
| `SftpExport` | ✅ implemented - uploads a job's file(s) to a remote directory. |
