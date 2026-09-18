# ColdNet

ColdNet is a .NET 10 codeless, modular batch-processing engine for handling incoming documents,
in the tradition of classic COLD (Computer Output to Laser Disk) batch-processing tools. You
build a **process chain** out of small, reusable **modules** (import, convert, extract
properties, split, export, ...), and a worker runs that chain against every file that shows up
in a watched directory - no per-customer code, just configuration.

Finished documents are handed off to **EDMVault** (our own DMS), via a pluggable connector
(file drop, or a live REST connector - see [EDMVault connectors](#edmvault-connectors) below).

A single Blazor Server web app, **ColdNet.Admin**, covers the whole admin workflow: manage
process groups and chains, configure modules, monitor and reset jobs.

## Solution layout

```
ColdNet.slnx
src/
  ColdNet.Core       Domain model + module SDK (IColdModule, ModuleExecutionContext, PropertyBag, ...) - no external deps
  ColdNet.Data       EF Core (SQLite by default, SQL Server supported) - jobs, chains, groups, module config
  ColdNet.Modules    Built-in modules (Import, TextConversion, Xml, Csv, Tools, JobSeparation,
                     PropertyExtraction, GraphicsConversion, Compression, Case, FileHandling,
                     RemoteTransfer)
  ColdNet.EdmVault   EDMVault: file-drop connector, REST connector, export + import modules
  ColdNet.Engine     ModuleRegistry (module discovery) + ChainScheduler (the scheduling loop)
  ColdNet.Worker     Background service host - runs the scheduling loop
  ColdNet.Admin      Blazor Server admin UI - process groups/chains, modules, jobs
  ColdNet.SecretTool Command-line emergency tool to decrypt stored secrets (passwords, ...)
  ColdNet.SignTool   Publisher-side tool: creates the module-signing certificate, signs/verifies module assemblies
tests/
  ColdNet.Core.Tests
  ColdNet.Engine.Tests
docs/
  MODULES.md         Full ColdNet module catalogue, with each module's reference code
  PLACEHOLDERS.md    {prefix}/{input}/{Now:...} etc. reference + worked examples (also at /help)
  ENCRYPTION.md       How secrets are encrypted at rest + how to use ColdNet.SecretTool
  MODULE-SIGNING.md  Module signature verification: trust model, CI setup, plugin signing, limits
templates/            dotnet new templates: module item templates + plugin project (VS/Rider/CLI)
deploy/local-release/ Files bundled into the release packages (Start-*.bat/start-*.sh, README*.txt)
docker-compose.yml   Admin + Worker in Linux containers, see "Running it" below
src/ColdNet.Admin/Dockerfile, src/ColdNet.Worker/Dockerfile  Built by docker-compose.yml and CI
.github/workflows/   CI (build+test) and CD (win-x64 + linux-x64 local-execution releases, plus
                     Docker Hub image pushes, all from master)
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

You can run both Admin and Worker in Linux containers via Docker Compose - `docker-compose.yml`
builds both images locally:

```bash
docker compose up -d
```
Images built locally by `docker-compose.yml` are unsigned, so it sets `ColdNet__ModuleSigning__Enforce=false`
for them (see [Module signing](#module-signing)); the Docker Hub images below are signed by CI and
need no such override.

Admin UI will be accessible at http://localhost:5202. Both containers include headless LibreOffice
for Office-to-PDF conversion (`OfficeToPdf`), Ghostscript + a free sRGB ICC profile for PDF/A
conversion (`PdfToPdfA` - point its `IccProfilePath` setting at `/usr/share/color/icc/sRGB.icc`),
and Tesseract with German/English language packs for image OCR (`ExtractText` - already on PATH,
matching its default `TesseractPath` setting).

Pre-built images are also published to Docker Hub on every push to `master` that passes CI -
[`akrauter/coldnet-admin`](https://hub.docker.com/r/akrauter/coldnet-admin) and
[`akrauter/coldnet-worker`](https://hub.docker.com/r/akrauter/coldnet-worker), tagged `latest` and
`master-<short-sha>`. Point `docker-compose.yml`'s `build:` sections at these instead of building
locally, or run them directly (`docker run -p 5202:8080 -v coldnet-data:/app/data
akrauter/coldnet-admin`) - both images already have `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`
set to `Docker`, so they listen on `:8080` and store the database under `/app/data` without any
extra configuration.

### Or: download a ready-to-run release

Every push to `master` that passes CI publishes a new
[GitHub release](https://github.com/akrauter/ColdNet/releases) with self-contained builds (no
.NET install required) for both common platforms:

- **`ColdNet-win-x64-<version>.zip`** - `Admin/`, `Worker/`, `SecretTool/`, `Start-Admin.bat`,
  `Start-Worker.bat`. The modules are signed and `module-signing.cer` is bundled. Unzip it, run `Start-Admin.bat`, open http://localhost:5202, then run
  `Start-Worker.bat` to actually process chains in the background. See
  `deploy/local-release/README.txt` (bundled in the zip) for details.
- **`ColdNet-linux-x64-<version>.tar.gz`** - same layout, `start-admin.sh`/`start-worker.sh`
  instead of the `.bat` files (already executable). See `deploy/local-release/README-linux.txt`
  (bundled in the archive) for details, including the small set of external dependencies some
  modules need on a bare Linux host (`TextToPdf` needs any installed TTF font, `OfficeToPdf` needs
  LibreOffice) - the Docker images above already carry them.

See `.github/workflows/ci-release.yml` for the pipeline itself: it builds and tests on every
push/PR (on `ubuntu-latest`, so a green run is real proof of Linux compatibility, not just an
assumption from package metadata), and only cuts a release - for both platforms - once that passes
**and** the commit is on `master`.

## Core concepts

| Term | ColdNet type | Notes |
|---|---|---|
| Prozessgruppe (process group) | `ProcessGroup` | Pure organizational container for chains. |
| Prozesskette (process chain) | `ProcessChain` | Ordered list of module instances. Module 0 is always an import module. |
| Modul-Instanz | `ModuleInstance` | One configured step: common settings (dir/extension/save/...), DMS support settings, module-specific JSON settings. |
| Job | `Job` | A file (or group of same-prefix files) moving through a chain. Status: `Ready` / `Working` / `Error` / `Finished`. `FilePrefix` is guaranteed unique per chain - a re-delivered file that would otherwise collide with an existing (e.g. finished, still-kept) job gets a `-2`, `-3`, ... suffix instead of being silently skipped. |
| Job log | `JobLogEntry` | One row per module a job has passed through (success or failure, with any error message) - the full step-by-step history, shown via the "Log" button on the Jobs page, independent of the job's current live status. |
| `$`-prefix trick on import | Same | CNIMPORT's mechanism: `Test.pdf` -> `<jobnumber>.$pdf` so a directory scan never re-imports a file. |
| JPL-style property file | `PropertyBag` (`<prefix>.properties.json`) | A job-scoped variable bag with multi-value fields, JSON instead of the legacy JPL text format. |
| General / DMS support tabs | `CommonModuleSettings` / `DmsSupportSettings` | Directory, Output directory, File extension, Save, Delete source, Mask for DMS, Append / DMS support enabled + Document type. |

### Scheduling

`ChainScheduler.RunOnceAsync` processes jobs in this order: for module position 0, then 1,
then 2, ... it processes that module's ready jobs across **every** chain assigned to this worker
before moving to the next position, then starts over. A chain's `WorkerName` assigns it to a
worker - only one worker process should ever be configured with a given name for a given chain,
since a chain should only ever be loaded by one worker.

On failure, a job is parked in `Error` at the module that failed (its `CurrentModuleOrder` does
not advance) - fix the cause and reset it to `Ready` from the Jobs page, and processing resumes
exactly where it left off.

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

Because a module runs inside the Admin/Worker process with its full rights, the host only runs
module assemblies that carry a valid detached signature from a certificate it trusts - the built-in
modules and anything dropped into `plugins/` alike (see [Module signing](#module-signing)).

`templates/` has `dotnet new` templates (`coldnet-module` / `coldnet-import-module` items, `coldnet-plugin` project for external signed plugins) that
scaffold a new module's starting file for you - install once with `dotnet new install ./templates`
and they show up both on the command line and inside Visual Studio's/Rider's "Add New Item" /
"New from Template" dialogs. See `templates/README.md`.

## EDMVault connectors

Set `ColdNet:EdmVault:Connector` to choose how the `EdmVaultExport` module hands a finished job
off:

- **`FileDrop`** (default) - copies/moves the job's files into a hand-over directory and writes
  a JSON (or XML) index file next to them, a "drop files + index file for pickup" pattern used by
  `d.3 hostimport`-style watchers. No API or credentials required.
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

## Module signing

Every module assembly - built-in and plugin - must carry a detached signature (`<name>.dll.sig`)
made with the publisher's **private** key; the application holds only the matching **public**
certificate (`module-signing.cer`) and refuses to start if any module fails verification. Release
packages and the Docker Hub images are signed by CI (fail-closed: the pipeline needs the
`MODULE_SIGNING_PFX_BASE64`/`MODULE_SIGNING_PFX_PASSWORD` secrets). Local `dotnet run` (Development
environment) and locally built Docker images opt out via `ColdNet:ModuleSigning:Enforce = false`,
with a loud warning on every start. Setup, plugin signing, key rotation and the honest limits of
the scheme: [`docs/MODULE-SIGNING.md`](docs/MODULE-SIGNING.md).

## Secrets at rest

Module settings fields marked `[SensitiveValue]` (currently `SftpImport`/`SftpExport`'s `Password`
and `PrivateKeyPassphrase`) are masked in the admin UI (shown as dots, with a Show/Hide toggle) and
stored AES-256-GCM encrypted in the database - never in plain text. `ColdNet.SecretTool`, a
command-line tool published alongside Admin and Worker, lets an admin decrypt a value directly
against the database in an emergency (e.g. no access to the Admin UI). See
[`docs/ENCRYPTION.md`](docs/ENCRYPTION.md) for how the encryption works, key management/rotation,
and the tool's commands.

## Known limitations

- **Module coverage**: this is a framework plus a representative module per category (~25
  modules), not a full catalogue of ~90+ possible modules. Host/mainframe conversion (AS/400,
  SAP) and several niche graphics converters (Ghostscript-based PS/PCL page-description
  conversion, ABBYY-specific OCR) are intentionally out of scope - see `docs/MODULES.md` for
  exactly what's covered and what would need a new module. Basic key/value extraction from PDF or
  image files is covered (`ExtractText`, via Tesseract for images - a *searchable* PDF's own text
  layer needs no OCR at all; a *scanned* PDF with no text layer isn't read directly, convert its
  pages to images first), chained into `ParseProperties` for the actual regex-based extraction.
  `SftpImport`/`SftpExport` (SFTP, FTPS, or plain FTP -
  see `ColdNet.Modules/RemoteTransfer`) and `EdmVaultImport` have no reference code at all; they
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
