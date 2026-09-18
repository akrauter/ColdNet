# ColdNet module templates

Two [`dotnet new`](https://learn.microsoft.com/dotnet/core/tools/custom-templates) item templates
that scaffold a new ColdNet module - the same `dotnet new` templating engine both Visual Studio
and JetBrains Rider use, so installing this once makes the templates show up in either IDE's
"Add New Item" / "New from Template" dialog, as well as on the command line.

| Short name | Produces | Use for |
|---|---|---|
| `coldnet-module` | An `IColdModule` (regular pipeline step) | Most modules - anything that isn't module 0 of a chain. |
| `coldnet-import-module` | An `IJobImportModule` | A module meant to sit at position 0 of a chain (watches/pulls new work and creates jobs) - see `ColdImportModule`/`SftpImportModule`/`EdmVaultImportModule` for existing examples. |

## Install

From the repository root:

```bash
dotnet new install ./templates
```

This installs both templates at once (each subfolder here is its own independent template). Run
`dotnet new uninstall ./templates` to remove them again, or `dotnet new update` after pulling
changes to this folder to pick up edits.

## Use

### Command line

```bash
# Regular module
dotnet new coldnet-module -n TextUppercase --Category TextConversion \
  --DisplayName "Text Uppercase" --ModuleDescription "Converts a text file to uppercase."

# Import module
dotnet new coldnet-import-module -n WebhookImport \
  --DisplayName "Webhook Import" --ModuleDescription "Accepts pushed files via an inbound webhook."
```

Run `dotnet new coldnet-module --help` (or `coldnet-import-module --help`) to see all available
parameters, including the full list of valid `--Category` values.

Both commands generate one `.cs` file named after `-n`. Move it into the matching
`ColdNet.Modules`/`ColdNet.EdmVault` subfolder (or your own plugin assembly project) - the file
itself has a `TODO` comment reminding you to fix the namespace to match. No other registration
step is needed: `ModuleRegistry` discovers any `[ModuleDefinition(...)]`-decorated class in the
loaded module assemblies automatically, and it appears in the admin UI's "Add module" picker.

A module in a **plugin assembly** (a separate project whose DLL you drop into the host's `plugins/`
folder, rather than a class added to `ColdNet.Modules`) is only loaded if that DLL - and any other
DLL the plugin brings along - has been signed with the publisher's key
(`ColdNet.SignTool sign ...`). See [`docs/MODULE-SIGNING.md`](../docs/MODULE-SIGNING.md).

### Visual Studio

After installing, right-click a project (or folder) in Solution Explorer → **Add** → **New Item...**
and search for "ColdNet" - both templates appear there once VS has indexed installed `dotnet new`
templates (may need a restart the first time after installing).

### Rider

After installing, **File** → **New...** (or right-click a folder → **New**) shows both templates
under their `ColdNet` classification, using the same parameters as the command line above.

## What you still have to write

The generated file is a starting point, not a finished module - it compiles and runs (the regular
module template copies its input file to the output path unchanged; the import module template
discovers nothing and returns no jobs), but every `TODO` comment marks something you need to fill
in: the settings your module actually needs, its real processing/discovery logic, and - for a
regular module - whether it reads/writes one file, several, or none at all (see
`docs/MODULES.md`'s "Adding a module" section and an existing module of a similar shape for the
established patterns, e.g. `ColdNet.Modules/TextConversion/TextReplaceModule.cs` for a simple
one-file-in/one-file-out module, or `ColdNet.Modules/RemoteTransfer/SftpImportModule.cs` for an
import module that also needs a local de-dup state file).
