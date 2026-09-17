# Placeholder variables

A handful of text settings fields accept placeholders - plain `{Name}` tokens substituted before
the module runs. Which placeholders a field accepts depends on the module; they're never mixed
(a `SourceFileMask` field only understands `{prefix}`, for example).

The same reference is available inside the running app at **/help**, alongside a few worked
examples.

## File mask placeholders

Used in every module's `SourceFileMask` setting (`EdmVaultExport`, `SftpExport`,
`MultiPageTiff`, `PdfConcat`).

| Placeholder | Resolves to |
|---|---|
| `{prefix}` | The job's file prefix (its job number), e.g. `BEAXH1TS60QW`. |

`SourceFileMask` is a filesystem glob (`*`/`?`), not a regex - it's matched against file names in
the module's input directory. `EdmVaultExport` and `SftpExport` default to `{prefix}.*` (every
file belonging to the job); `MultiPageTiff` and `PdfConcat` default to `{prefix}_*.tif` /
`{prefix}_*.pdf` (numbered single-page files produced by an earlier step - see the worked example
below).

## Shell Execute argument placeholders

Used in `ShellExecute`'s `Arguments` setting.

| Placeholder | Resolves to |
|---|---|
| `{input}` | Full path to the module's resolved input file (input directory + job prefix + file extension). |
| `{output}` | Full path to the module's resolved output file (output directory + job prefix + output file extension). |
| `{prefix}` | The job's file prefix. |
| `{inputDir}` | The module's input directory. |
| `{outputDir}` | The module's output directory. |
| `{fileName}` | File name (with extension) of the module's resolved input file, e.g. `BEAXH1TS60QW.pdf`. |
| `{fileNameWithoutExtension}` | File name without extension of the module's resolved input file, e.g. `BEAXH1TS60QW`. |
| `{extension}` | Extension (without dot) of the module's resolved input file, e.g. `pdf`. |

## Set Variable value placeholders

Used in `SetVariable`'s per-assignment `Value` setting.

| Placeholder | Resolves to |
|---|---|
| `{JobPrefix}` | The job's file prefix. |
| `{DocumentType}` | The module's DMS support "Document type" field (empty string if not set). |
| `{FileName}` | File name (with extension) of the module's resolved input file, e.g. `BEAXH1TS60QW.pdf`. |
| `{FileNameWithoutExtension}` | File name without extension of the module's resolved input file, e.g. `BEAXH1TS60QW`. |
| `{Extension}` | Extension (without dot) of the module's resolved input file, e.g. `pdf`. |
| `{Now}` / `{Now:format}` | Current local date/time. `format` is a [.NET custom date/time format string](https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings); `{Now}` alone is short for `{Now:yyyy-MM-dd}`. |

## Rename Files pattern placeholders

Used in `RenameFiles`'s `NewFileNamePattern` setting - only read when it's non-empty; leave it
blank to keep the module's original single-file `FromExtension`/`ToExtension` behavior.

| Placeholder | Resolves to |
|---|---|
| `{JobPrefix}` | The job's file prefix. |
| `{DocumentType}` | The module's DMS support "Document type" field (empty string if not set). |
| `{FileName}` | File name (with extension) of the file being renamed. |
| `{FileNameWithoutExtension}` | File name without extension of the file being renamed. |
| `{Extension}` | Extension (without dot) of the file being renamed, with any leading `"$"` import marker already stripped (e.g. `$pdf` -> `pdf`) - this is what lets a single `RenameFiles` step drop CNIMPORT's `"$"` marker across a chain that handles more than one source extension, which a fixed `FromExtension`/`ToExtension` pair can't do. |
| `{Now}` / `{Now:format}` | Current local date/time, same as the Set Variable placeholder above. |
| `{Counter}` | An ever-incrementing sequence number, persisted on the module instance (never reset per job) - padded to `CounterDigits` digits using `CounterFillChar` (e.g. `CounterDigits: 4`, `CounterFillChar: "0"` -> `0007`). |

When set, every file matching `<job prefix>.*` in the module's input directory is discovered and
renamed with this pattern - not just one file with a known extension.

## Worked examples

**Hand a finished job's files over to EDMVault or SFTP** - `EdmVaultExport` and `SftpExport` both
default `SourceFileMask` to `{prefix}.*`: every file sharing the job's prefix, regardless of
extension. Leave it as-is to hand over everything the chain produced; narrow it (e.g.
`{prefix}.pdf`) to hand over only one file type and leave the rest for a later step.

**Drop the "$" marker before an SFTP/EDMVault export, whatever the source extension** - a chain
whose `ColdImport` step accepts several file types (e.g. `FileMask: "*.*"`) can't strip CNIMPORT's
`"$"` marker with a fixed `FromExtension`/`ToExtension` pair, since it would need one `RenameFiles`
instance per possible extension. Set `NewFileNamePattern` to `{JobPrefix}.{Extension}` instead -
`{Extension}` already excludes the `"$"`, so this works for every incoming extension in one step,
right before an `SftpExport`/`EdmVaultExport` module that would otherwise upload/hand over a file
still named e.g. `BEAXH1TS60QW.$pdf`. Add a running document number on top with
`NewFileNamePattern: "{JobPrefix}_{Counter}.{Extension}"`, `CounterDigits: 6`.

**Reassemble scanned pages into one multi-page TIFF** - `MultiPageTiff` expects several
single-page files already named `<prefix>_<n>.tif` by an earlier step (a scanner, or a module like
`BarcodeSplit`) - e.g. `BEAXH1TS60QW_001.tif`, `BEAXH1TS60QW_002.tif`, ... With the default mask
`{prefix}_*.tif`, adding the module to a chain after such a step combines all of them (sorted by
file name, so keep the numbering zero-padded) into a single `BEAXH1TS60QW.tif`.

**Call an external converter (e.g. Ghostscript)** - `ShellExecute` with executable `gs` and
arguments `-q -dNOPAUSE -dBATCH -sDEVICE=pdfwrite -o {output} {input}` converts whatever the
module's configured input/output directories and extensions resolve to (e.g. PostScript in, PDF
out) without ColdNet needing a dedicated module for that specific tool.

**Tag a job with computed metadata** - `SetVariable` with assignments `importDate` =
`{Now:yyyy-MM-dd}` and `sourceJob` = `{JobPrefix}` writes both into the job's property bag -
picked up automatically by a later `EdmVaultExport`/`PropertiesToXml` step as index fields.
