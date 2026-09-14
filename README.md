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
                     PropertyExtraction, GraphicsConversion, Compression, Case, FileHandling)
  ColdNet.EdmVault   EDMVault hand-off: file-drop connector + REST connector + the export module
  ColdNet.Engine     ModuleRegistry (module discovery) + ChainScheduler (the scheduling loop)
  ColdNet.Worker     Background service host - the "d.cold worker" equivalent
  ColdNet.Admin      Blazor Server admin UI - the "d.cold admin / webadmin" equivalent
tests/
  ColdNet.Core.Tests
  ColdNet.Engine.Tests
docs/
  MODULES.md         Full d.cold-module -> ColdNet-module mapping table
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

## Known deviations from d.cold

- **Module coverage**: this is a framework plus a representative module per category (~20
  modules), not a line-for-line port of all ~90 d.cold modules. Host/mainframe conversion
  (AS/400, SAP) and several niche graphics converters (Ghostscript-based PS/PCL handling,
  ABBYY OCR) are intentionally out of scope - see `docs/MODULES.md` for exactly what's covered
  and what would need a new module.
- **Finished jobs are kept**, not deleted or moved to a separate table, so the Jobs page stays
  useful for auditing/debugging. Delete them manually (or add a cleanup task) if that matters
  for your volume.
- **Module settings are edited as raw JSON** in the admin UI (with a "load default template"
  helper), rather than a bespoke form per module - a reasonable framework-v1 tradeoff given the
  number of modules; a typed settings form per module is a natural next step.
- **`SixLabors.ImageSharp`** (used by the graphics-conversion modules) is licensed under the Six
  Labors Split License, not Apache 2.0/MIT - free for small businesses/open source, but check
  https://sixlabors.com/pricing/ before using ColdNet's graphics modules commercially at scale.
  Swap in `Magick.NET` (Apache 2.0) in `ColdNet.Modules/GraphicsConversion` if that's a blocker.
