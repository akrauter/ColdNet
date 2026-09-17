# Module catalogue

Every module ColdNet could have, organized by category, each with its own reference code (the
`OriginalModule` value on its `[ModuleDefinition]` attribute, shown in Admin as "ColdNet: CNxxx").
"Status" is `✅ implemented` (a working ColdNet module exists), or `— not implemented` (no ColdNet
module yet; add one following the pattern in [Adding a module](../README.md#adding-a-module) if
you need it).

The live version of this table - generated straight from `[ModuleDefinition]` attributes - is
always available in the running app at **/modules**.

## Import modules

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNIMPORT | `ColdImport` | ✅ implemented |
| — (no reference code) | `EdmVaultImport` | ✅ implemented - pulls new files from an EDMVault project via the REST API, the reverse of `EdmVaultExport`. See [README § EDMVault connectors](../README.md#edmvault-connectors). |
| — (no reference code) | `SftpImport` | ✅ implemented - pulls new files from a remote SFTP/FTPS/FTP server. See [Remote transfer](#remote-transfer-coldnet-specific) below. |

## Text converters

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNREPLACE, CNCHANGE | `TextReplace` | ✅ implemented |
| CNENCODETXT, CNSETBOM | `EncodeText` | ✅ implemented |
| CNTXT2PDF | `TextToPdf` | ✅ implemented |
| CNCHANGEBIN, CNCONVTEXT, CNDELFIRSTEMPTY, CNDELLASTLINES, CNDELLINES, CNDOS2WIN, CNGETLINES, CNHEADER, CNINSERTCC, CNMASKFORD3, CNRTF2TXT, CNSPLITLINES, CNTABULATOR, CNTXT2TIF, CNUNICODE2TXT, CNUNIX2WIN, CNWIN2UNIX | — | not implemented |

## XML

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNCONVXML | `PropertiesToXml` | ✅ implemented |
| CNATER2XML | — | not implemented |

## CSV

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| — (no reference code) | `PropertiesToCsv` | ✅ implemented - writes the job's property bag as a CSV file, one row per key/value pair (`;`-delimited by default, opens correctly in a German-locale Excel), for handing extracted values (e.g. from `ExtractText` + `ParseProperties`) to a downstream system or a spreadsheet instead of an XML index file. |

## Tools

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNNOP | `NoOp` | ✅ implemented |
| CNEXECUTE | `ShellExecute` | ✅ implemented |
| CNWAITCONFIG, CNWAITXTIMES | — | not implemented |

## Job separation

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNBARCODE, CNJOBSEP | `BarcodeSplit` | ✅ implemented |
| CNAFPSPLIT, CNFILESPLITTER, CNFORMULAR, CNHL72XML, CNJOBSEPEXT, CNJOBSEPPDF, CNOCRSPLIT, CNSPLITAFTERX, CNSPLITMAIL | — | not implemented |

## Extracting properties

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNSETVAR, CNSETCONST | `SetVariable` | ✅ implemented |
| CNGETID | `GenerateId` | ✅ implemented |
| CNPARSE, CNEXTRACTLINE, CNVARFRTXT, CNVARAFTERKW | `ParseProperties` | ✅ implemented (regex-rule based, covers the common cases of all four) |
| CNPDF2TXT, CNOCR, CNOCRTEXT | `ExtractText` | ✅ implemented - extracts a PDF's embedded text layer (PdfPig, no external tool) or OCRs an image (Tesseract, external process - not bundled) into a `.txt` file; chain a `ParseProperties` step after it for the actual key/value extraction. Scanned PDFs (no text layer) aren't read directly - convert their pages to images first. |
| CNCHANGEPOS, CNCOUNTTIF, CNDBQUERY, CNGETBARCODES, CNGETFILEATTR, CNGETFILEHASH, CNGETIPTC, CNGODCLASSIFY, CNMIXVAR, CNSINGLE2MULTIVAR, CNTXT2JPL, CNVARFRPDF | — | not implemented |

## Hosts

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNAS42PCL, CNAS42TXT, CNAS4FETCHER, CNBS22TXT | — | not implemented - extension point (AS/400-specific, needs real target-system access to build/test against). For generic remote file pickup/delivery (not AS/400-specific), see `SftpImport`/`SftpExport` under [Remote transfer](#remote-transfer-coldnet-specific) below. |

## ERP

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNSAPFC, CNSAPFCATT, CNSAPSENDBC, CNTOA01 | — | not implemented - extension point (SAP-specific, needs an SAP connector/RFC library) |

## Graphics converters

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNCONVGRAFIC | `ConvertGraphic` | ✅ implemented (TIFF/PNG/JPEG/BMP/GIF via Magick.NET, Apache-2.0, cross-platform) |
| CNMULTIPAGE, CNMULTIPAGEADV, CNMULTIPAGEEXT | `MultiPageTiff` | ✅ implemented |
| CNPDFCONCAT, CNAPPENDPDF | `PdfConcat` | ✅ implemented |
| CNOFFICE2PDF | `OfficeToPdf` | ✅ implemented (via LibreOffice headless instead of MS Office automation) |
| CNPDF2PDF | `PdfToPdfA` | ✅ implemented - converts a PDF to a PDF/A-1b/2b/3b archival PDF via Ghostscript (AGPL-licensed, free for any use including commercial - invoked as an external process, like `OfficeToPdf`/LibreOffice, never bundled or linked). Ghostscript can only guarantee "b" (visual reproducibility) conformance, not "a"/"u" (tagged/accessible). |
| CNADDTIF, CNAFP2TIF, CNDELPDFPAGES, CNDELTIFPAGES, CNEASYTIF, CNEASYTIF2, CNGETFORMAT, CNHGLAVTIF, CNMAKETIFDIN, CNOPTFORMAT, CNPCL2PDF, CNPCL2TIF, CNPCL2TXT, CNPDF2TIF, CNPDFAVALIDATE, CNPDFSEARCHABLE, CNPRESCRIBE2TXT, CNPRINTGL, CNPS2PDF, CNPS2TIFCOL, CNPS2TIF, CNPSPDF2TXT, CNREDLINE, CNROTATETIF, CNSHELLEXEC, CNSHELLPDF, CNTIF2DIN, CNTIF2PDF, CNTIFTIF, CNZUGFERD | — | not implemented (most need Ghostscript for PS/PCL; use `ShellExecute` to wrap Ghostscript/an external tool directly in the meantime) |

## Compression

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNUNPACK | `UnpackArchive` | ✅ implemented (ZIP only) |
| CNTXT2LSX, CNPDFATTACHMENT | — | not implemented |

## Case

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNCASE | `CaseBranch` | ✅ implemented - full category coverage |

## File handling

| ColdNet reference | ColdNet module | Status |
|---|---|---|
| CNFILEMOVER | `FileMover` | ✅ implemented |
| CNDELFILES | `DeleteFiles` | ✅ implemented |
| CNRENFILES | `RenameFiles` | ✅ implemented - full category coverage |

## DMS export (ColdNet-specific)

Legacy `d.3 hostimport`-style watchers pick up finished files implicitly, via a "Mask for DMS"
checkbox on whichever module runs last. ColdNet makes that hand-off an explicit final pipeline
step instead:

| ColdNet module | Status |
|---|---|
| `EdmVaultExport` | ✅ implemented - file-drop or live REST connector, see [README § EDMVault connectors](../README.md#edmvault-connectors) |

## Remote transfer (ColdNet-specific)

Generic network file transfer, independent of any specific host system (contrast with the
AS/400-specific `Hosts` category above). SFTP is recommended; FTPS (explicit TLS) and plain FTP
are supported for legacy servers. Built on SSH.NET and FluentFTP (both MIT, both pure-managed -
no native dependencies, so these work on Linux/Docker the same as everywhere else):

| ColdNet module | Status |
|---|---|
| `SftpImport` | ✅ implemented - lists a remote directory and downloads new files as jobs, the remote counterpart of `ColdImport`. Since a remote server has no equivalent of the local "$"-prefix rename trick, already-downloaded files are renamed (default) or deleted remotely so they aren't re-imported. |
| `SftpExport` | ✅ implemented - uploads a job's file(s) to a remote directory. On upload failure, the error now includes the exception type and any inner exception, since some servers (e.g. **Hetzner Storage Box**, which only exposes `/home/` as writable - writing to `/` itself fails with a bare, unhelpful `SftpException: Failure`) reject a write with no further detail; if you hit that exact error, check `RemoteDirectory` against what the server actually allows before assuming it's a ColdNet bug. |
