# ColdNet

ColdNet is a .NET 10 re-implementation of the ideas behind **d.velop d.cold**: a codeless,
modular batch-processing engine for handling incoming documents. You build a **process chain**
out of small, reusable **modules** (import, convert, extract properties, split, export, ...),
and a worker runs that chain against every file that shows up in a watched directory - no
per-customer code, just configuration.

Where d.cold hands finished documents to a **d.3** repository, ColdNet hands them to
**EDMVault** (our own DMS), via a pluggable connector (file drop, or a live REST connector -
see [EDMVault connectors](#edmvault-connectors) below).

d.cold's admin/webadmin duo is replaced by a single Blazor Server web app, **ColdNet.Admin**,
that provides the same core workflow: manage process groups and chains, configure modules,
monitor and reset jobs.

## Solution layout

```
ColdNet.slnx
src/
  ColdNet.Core       Domain model + module SDK (IColdModule, ModuleExecutionContext, PropertyBag, ...) - no external deps
  ColdNet.Data       EF Core (SQLite by default, SQL Server supported) - jobs, chains, groups, module config
  ColdNet.Modules    Built-in modules (Import, TextConversion, Xml, Tools, JobSeparation,
                     PropertyExtraction, GraphicsConversion, Compression, Case, FileHandling,
                     RemoteTransfer)
  ColdNet.EdmVault   EDMVault: file-drop connector, REST connector, export + import modules
  ColdNet.Engine     ModuleRegistry (module discovery) + ChainScheduler (the scheduling loop)
  ColdNet.Worker     Background service host - the "d.cold worker" equivalent
  ColdNet.Admin      Blazor Server admin UI - the "d.cold admin / webadmin" equivalent
  ColdNet.SecretTool Command-line emergency tool to decrypt stored secrets (passwords, ...)
tests/
  ColdNet.Core.Tests
  ColdNet.Engine.Tests
docs/
  MODULES.md         Full d.cold-module -> ColdNet-module mapping table
  PLACEHOLDERS.md    {prefix}/{input}/{Now:...} etc. reference + worked examples (also at /help)
  ENCRYPTION.md       How secrets are encrypted at rest + how to use ColdNet.SecretTool
deploy/local-release/ Files bundled into the release packages (Start-*.bat/start-*.sh, README*.txt)
.github/workflows/   CI (build+test) and CD (win-x64 + linux-x64 local-execution releases from master)
data/                Shared SQLite database file (dev default)
```

## Running it

Both apps share one database (SQLite file at `data/coldnet.db` by default) - point them at the
same connection string to run them together, or run only `ColdNet.Admin` while you design chains
(it can also trigger a scheduling pass itself via the "Run now" button, without the Worker
running).

```bash
dotnet run --project src/ColdNet.Admin/ColdNet.Admin.csproj    # http://localhost:5202
dotnet run --project src/ColdNet.Worker/ColdNet.Worker.csproj  # background processing loop
```

Both apply pending EF Core migrations and seed the built-in `default` process group on startup.

### Or: Docker / Docker Compose (Linux)

You can run both Admin and Worker in Linux containers via Docker Compose:

```bash
docker compose up -d
```
Admin UI will be accessible at http://localhost:5202. Both containers include headless LibreOffice
for Office-to-PDF conversion (`OfficeToPdf`) and Ghostscript + a free sRGB ICC profile for PDF/A
conversion (`PdfToPdfA` - point its `IccProfilePath` setting at `/usr/share/color/icc/sRGB.icc`).

### Or: download a ready-to-run release

Every push to `master` that passes CI publishes a new
[GitHub release](https://github.com/akrauter/ColdNet/releases) with self-contained builds (no
.NET install required) for both common platforms:

- **`ColdNet-win-x64-<version>.zip`** - `Admin/`, `Worker/`, `SecretTool/`, `Start-Admin.bat`,
  `Start-Worker.bat`. Unzip it, run `Start-Admin.bat`, open http://localhost:5202, then run
  `Start-Worker.bat` to actually process chains in the background. See
  `deploy/local-release/README.txt` (bundled in the zip) for details.
- **`ColdNet-linux-x64-<version>.tar.gz`** - same layout, `start-admin.sh`/`start-worker.sh`
  instead of the `.bat` files (already executable). See `deploy/local-release/README-linux.txt`
  (bundled in the archive) for details, including the small set of external dependencies some
  modules need on a bare Linux host (`TextToPdf` needs any installed TTF font, `OfficeToPdf` needs
  LibreOffice) - the Docker images below already carry them.

See `.github/workflows/ci-release.yml` for the pipeline itself: it builds and tests on every
push/PR (on `ubuntu-latest`, so a green run is real proof of Linux compatibility, not just an
assumption from package metadata), and only cuts a release - for both platforms - once that passes
**and** the commit is on `master`.

## Core concepts (mirrors d.cold's own terms)

| d.cold term | ColdNet term | Notes |
|---|---|---|
| Prozessgruppe (process group) | `ProcessGroup` | Pure organizational container for chains. |
| Prozesskette (process chain) | `ProcessChain` | Ordered list of module instances. Module 0 is always an import module. |
| Modul-Instanz | `ModuleInstance` | One configured step: common settings (dir/extension/save/...), DMS support settings, module-specific JSON settings. |
| Job | `Job` | A file (or group of same-prefix files) moving through a chain. Status: `Ready` / `Working` / `Error` / `Finished`. |
| d.cold worker | `ChainWorker` (in `ColdNet.Worker`) | Repeatedly runs `ChainScheduler.RunOnceAsync`. |
| d.cold admin / webadmin | `ColdNet.Admin` | One Blazor Server app instead of a native + web pair. |
| `$`-prefix trick on import | Same | DCIMPORT's exact mechanism is reproduced: `Test.pdf` -> `<jobnumber>.$pdf` so a directory scan never re-imports a file. |
| JPL property file | `PropertyBag` (`<prefix>.properties.json`) | Same idea (a job-scoped variable bag with multi-value fields), JSON instead of the legacy JPL text format. |
| General / d.3 support tabs | `CommonModuleSettings` / `DmsSupportSettings` | Same fields (Directory, Output directory, File extension, Save, Delete source, Mask for DMS, Append / DMS support enabled + Document type). |

### Scheduling

`ChainScheduler.RunOnceAsync` follows d.cold's documented order: for module position 0, then 1,
then 2, ... it processes that module's ready jobs across **every** chain assigned to this worker
before moving to the next position, then starts over. A chain's `WorkerName` is the equivalent of
d.cold's `/P` worker assignment - only one worker process should ever be configured with a given
name for a given chain, matching d.cold's "a chain can only be loaded by one worker" rule.

On failure, a job is parked in `Error` at the module that failed (its `CurrentModuleOrder` does
not advance) - fix the cause and reset it to `Ready` from the Jobs page, and processing resumes
exactly where it left off, exactly like d.cold.

## Placeholders in module settings

Some settings fields (`SourceFileMask`, `ShellExecute`'s `Arguments`, `SetVariable`'s `Value`)
accept `{Name}` placeholders - e.g. `{prefix}` for the job's file prefix, or `{Now:yyyy-MM-dd}` for
today's date. Full reference and worked examples: `docs/PLACEHOLDERS.md`, or in the running app at
**/help**.

## Adding a module

Drop a class implementing `IColdModule` (or `IJobImportModule` for a module meant to sit at
position 0) into `ColdNet.Modules` (or your own plugin assembly), decorate it with
`[ModuleDefinition(...)]`, and it appears in the admin UI's "Add module" picker automatically -
no registry edits needed. See any existing module (e.g. `ColdNet.Modules/TextConversion/TextReplaceModule.cs`)
for the pattern, and `docs/MODULES.md` for the full catalogue.

## EDMVault connectors

Set `ColdNet:EdmVault:Connector` to choose how the `EdmVaultExport` module hands a finished job
off:

- **`FileDrop`** (default) - copies/moves the job's files into a hand-over directory and writes
  a JSON (or XML) index file next to them, the same "drop files + index file for pickup" pattern
  d.cold uses for `d.3 hostimport`. No API or credentials required.
- **`RestApi`** - talks to a live EdmVault.Api instance directly: logs in as a configured service
  account, resolves the module's "Document type" to an EDMVault project by title, uploads the
  file(s) (`POST /api/files`, `POST /api/files/{id}/secondary`), and writes the property bag as
  the file's metadata (`PUT /api/files/{id}/metadata`).

This setting only picks the *default* connector for `EdmVaultExport`. The `EdmVaultImport` module
(the reverse direction - lists an EDMVault project via `GET /api/files?projectId=`, downloads new
files as jobs, and imports their metadata into each job's property bag) always talks to the REST
API directly and reads the same `ColdNet:EdmVault:RestApi` section for its base URL/credentials,
regardless of which connector is configured as the default. Both use the module's DMS support
"Document type" field as the EDMVault project title.

```json
"ColdNet": {
  "EdmVault": {
    "Connector": "RestApi",
    "RestApi": {
      "BaseUrl": "https://localhost:7031",
      "UserName": "service-account",
      "Password": "…",
      "ProjectLookupCacheMinutes": 10
    }
  }
}
```

Don't commit real credentials into `appsettings.json` - use `dotnet user-secrets` or environment
variables (`ColdNet__EdmVault__RestApi__Password`) for anything beyond local development.

## Secrets at rest

Module settings fields marked `[SensitiveValue]` (currently `SftpImport`/`SftpExport`'s `Password`
and `PrivateKeyPassphrase`) are masked in the admin UI (shown as dots, with a Show/Hide toggle) and
stored AES-256-GCM encrypted in the database - never in plain text. `ColdNet.SecretTool`, a
command-line tool published alongside Admin and Worker, lets an admin decrypt a value directly
against the database in an emergency (e.g. no access to the Admin UI). See
[`docs/ENCRYPTION.md`](docs/ENCRYPTION.md) for how the encryption works, key management/rotation,
and the tool's commands.

## Known deviations from d.cold

- **Module coverage**: this is a framework plus a representative module per category (~25
  modules), not a line-for-line port of all ~90 d.cold modules. Host/mainframe conversion
  (AS/400, SAP) and several niche graphics converters (Ghostscript-based PS/PCL page-description
  conversion, ABBYY OCR) are intentionally out of scope - see `docs/MODULES.md` for exactly what's
  covered and what would need a new module. `SftpImport`/`SftpExport` (SFTP, FTPS, or plain FTP - see
  `ColdNet.Modules/RemoteTransfer`) and `EdmVaultImport` have no d.cold equivalent at all; they
  exist because ColdNet targets EDMVault instead of d.3 and needed a way to pull work in, not
  just hand it off.
- **Finished jobs are kept**, not deleted or moved to a separate table, so the Jobs page stays
  useful for auditing/debugging. Delete them manually (or add a cleanup task) if that matters
  for your volume.
- **Module settings are edited as raw JSON** in the admin UI (with a "load default template"
  helper), rather than a bespoke form per module - a reasonable framework-v1 tradeoff given the
  number of modules; a typed settings form per module is a natural next step.
- **Third-party licenses & cross-platform support**: every `ColdNet.Modules`/`ColdNet.EdmVault` dependency is permissively licensed
  (`Magick.NET` and `ZXing.Net` are Apache-2.0; `PDFsharp`, `SSH.NET`, and `FluentFTP` are MIT) - all are free and approved for commercial use without royalty fees, unlike the Six Labors Split License `SixLabors.ImageSharp` originally used here (dropped for exactly that reason). Every one of them is pure-managed with no native dependency, so image conversion, multi-page TIFF processing, barcode splitting, and the SFTP/FTPS modules are all fully cross-platform and run natively on Linux and in Docker containers - not just the graphics stack.
