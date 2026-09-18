# Module signing

ColdNet discovers its modules by reflection: any class with `[ModuleDefinition]` in a module
assembly becomes a pipeline step that runs inside the Admin/Worker process, with that process's
full rights (file system, network, the decrypted secrets). So the integrity of the module
assemblies is a security boundary - a swapped or injected DLL is arbitrary code execution in the
host. Module signing closes that boundary: **the host only runs module assemblies that carry a
valid signature from a certificate it trusts.**

## Trust model

| | Holds | Where |
|---|---|---|
| **Publisher** (you / your CI) | the **private** key (`signing.pfx`) | CI secret or offline - never in the application, never in git |
| **Application** (Admin, Worker) | only the **public** certificate (`module-signing.cer`) | next to the executable |

The application can *verify* signatures but cannot *create* them, so compromising a running host
does not let an attacker sign their own module. (Putting the private key into the application would
defeat the whole scheme.)

Signatures are **detached**: `ColdNet.Modules.dll` is accompanied by `ColdNet.Modules.dll.sig`, a
small JSON file. Authenticode is deliberately not used - verifying it is Windows-specific, and
ColdNet runs in Linux containers - so this is plain RSA-PSS or ECDSA over SHA-256 through
`System.Security.Cryptography`, identical on every platform.

What is signed is the file's SHA-256 **plus its file name** (with a fixed, versioned context
string), so one module's signature cannot be re-attached to a different, also legitimately signed
DLL. The trusted certificate is **pinned** - no chain building: the signature must name exactly the
trusted certificate's SHA-256 thumbprint, and that certificate must currently be within its
validity period.

## What is checked, and when

At startup, **before any module assembly is loaded**:

1. The trusted certificate (`ColdNet:ModuleSigning:TrustedCertificatePath`) is loaded; if
   `TrustedThumbprint` is set, it must match.
2. The built-in module assemblies (`ColdNet.Modules.dll`, `ColdNet.EdmVault.dll`) in the application
   directory are verified. They are checked *on disk, before the runtime loads them*, so a tampered
   DLL's module initializer never runs.
3. Every `*.dll` in the plugin directory (`plugins/` by default, top level only) is verified, and
   is then loaded **from the very bytes that were verified** - never re-read from disk - so the file
   cannot be swapped between check and load.
4. A plugin may not shadow an assembly the host already runs (e.g. a copy of `ColdNet.Core.dll` in
   `plugins/`): that would give its modules different type identities, or replace host code. Such a
   file is rejected even when signed.

If **anything** fails, the host logs every problem at once (not just the first) and exits with code
1. There is no half-trusted mode.

## Setting it up

### 1. Create the signing certificate (once)

```bash
export COLDNET_SIGNING_PFX_PASSWORD='<a strong password>'
dotnet run --project src/ColdNet.SignTool -- generate-cert \
    --subject "CN=<your company> ColdNet Module Signing" \
    --pfx module-signing.pfx --cer module-signing.cer --years 5
```

Prefer `--algorithm ecdsa` if you like; RSA-3072 (the default) is fine. The password is only ever
read from the environment variable, never from an argument (arguments end up in shell history and
process listings).

- **`module-signing.pfx` is the private key.** Back it up somewhere safe and offline; never commit it.
- `module-signing.cer` is public and safe to keep anywhere (including the repository).

### 2. Give CI the key

The Docker, Windows and Linux release jobs sign the built-in modules and ship the public
certificate next to each application. Add two repository secrets (Settings → Secrets and variables →
Actions):

| Secret | Value |
|---|---|
| `MODULE_SIGNING_PFX_BASE64` | `base64 -w0 module-signing.pfx` (PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes('module-signing.pfx'))`) |
| `MODULE_SIGNING_PFX_PASSWORD` | the PFX password |

**The release pipeline fails closed:** without these secrets it stops with an error instead of
publishing unsigned artifacts that would refuse to start. The Docker build receives them as BuildKit
secrets (mounted for one `RUN` only, never stored in an image layer); without them a *local*
`docker build` still works but yields an unsigned image.

### 3. Local development

`appsettings.Development.json` sets `ColdNet:ModuleSigning:Enforce` to `false`, so `dotnet run`
keeps working without signing anything. The host then logs a warning on every start and loads
plugins without verification. **Never run like this outside a developer machine.** A locally built
Docker image is unsigned too; `docker-compose.yml` therefore sets
`ColdNet__ModuleSigning__Enforce=false` for the services it builds - remove those lines when you run
a signed image from Docker Hub.

To try the real thing locally, run the published output in the `Production` environment after
signing it yourself:

```bash
dotnet publish src/ColdNet.Worker -c Release -o out/worker
dotnet run --project src/ColdNet.SignTool -- sign --pfx module-signing.pfx out/worker/ColdNet.Modules.dll out/worker/ColdNet.EdmVault.dll
cp module-signing.cer out/worker/
DOTNET_ENVIRONMENT=Production dotnet out/worker/ColdNet.Worker.dll
```

## Plugin modules

Drop a module assembly (and every other DLL it needs that the host doesn't already have) into
`plugins/` **together with its `.sig` files**, then restart:

```bash
dotnet run --project src/ColdNet.SignTool -- sign --pfx module-signing.pfx plugins/Acme.Modules.dll
```

Plugins are signed by *you* (the holder of the trusted key) - signing is how you approve them. A
third party can hand you a DLL, but it runs only after you have reviewed and signed it. Notes:

- Plugin assemblies can reference each other; everything else (ColdNet.Core, the framework)
  resolves from the host. Don't ship copies of host assemblies (rejected, see above).
- Native libraries next to a plugin aren't covered by signing and aren't loaded from the plugin
  directory - avoid them.
- Scaffold a complete plugin project with `dotnet new coldnet-plugin` (see [`templates/`](../templates/README.md)).

## SecretTool

`ColdNet.SecretTool` prints decrypted passwords, so it applies the same gate: before `list-modules` or
`decrypt-module` it verifies `ColdNet.Modules.dll`/`ColdNet.EdmVault.dll` (and loads signed plugins)
against `module-signing.cer` in its own folder, and refuses otherwise. The release package ships it
signed. `generate-key`, `encrypt` and `decrypt` never load module code and work without signatures.
To run it from a developer build set `ColdNet__ModuleSigning__Enforce=false`.

## Configuration

`ColdNet:ModuleSigning` in `appsettings*.json` / environment variables (`ColdNet__ModuleSigning__...`):

| Key | Default | Meaning |
|---|---|---|
| `Enforce` | `true` | Verify every module assembly; refuse to start on any failure. `false` = trust everything, with a loud warning. |
| `TrustedCertificatePath` | `module-signing.cer` | Public certificate. Relative paths resolve against the application directory. |
| `TrustedThumbprint` | *(empty)* | Optional pin: SHA-256 thumbprint (hex) the certificate file must have, so replacing the `.cer` alone is not enough. |
| `PluginDirectory` | `plugins` | Where external module assemblies are loaded from. A missing directory just means "no plugins". |

## `ColdNet.SignTool`

```text
generate-cert --subject "CN=..." --pfx signing.pfx --cer signing.cer [--years 5] [--algorithm rsa|ecdsa]
export-cer    --pfx signing.pfx --out module-signing.cer
sign          --pfx signing.pfx <assembly.dll>...     # writes <assembly.dll>.sig
verify        --cer module-signing.cer <assembly.dll>...
```

## Rotating or replacing the certificate

There is no revocation list and no timestamping; **rotation is the revocation mechanism.** Generate
a new certificate, re-sign the built-in modules and every plugin, and ship the new public
certificate with the release. Old signatures fail the thumbprint check by design. The host warns in
its log when the trusted certificate expires within 30 days - after expiry every module signature
fails and the host refuses to start, so rotate before that.

## What this does *not* protect against

Be honest about the boundary:

- **A fully compromised host.** Whoever can write the application directory can also replace
  `ColdNet.Engine.dll` (which does the checking), `appsettings.json`, or the `.cer` itself. Signing
  defends against untrusted or tampered *modules* and post-build tampering by less-privileged
  actors, not against an attacker who already owns the machine. Protect the directory with file
  permissions; `TrustedThumbprint` in a config the attacker cannot write adds a second lock.
- **A small window for built-in modules.** They are verified on disk and then loaded by the runtime
  from disk moments later (plugins do not have this gap - they load from the verified bytes).
- **What a signed module does.** A valid signature says *who approved* the code, not that it is
  harmless. The `ShellExecute` module, for instance, runs arbitrary commands by design and is
  unaffected.
- **Rollback.** A validly signed *older* version of a module can be put back; signatures carry no
  version binding. Rotate the certificate to invalidate old builds.
- **Assemblies not in `ColdNet.Modules.dll`/`ColdNet.EdmVault.dll`/`plugins/`** (the host's own
  libraries, the .NET runtime, NuGet dependencies) are outside this mechanism; use your normal
  supply-chain controls for those.

## Troubleshooting

| Message | Cause |
|---|---|
| `the trusted module-signing certificate '...' does not exist` | No `module-signing.cer` next to the app - not a signed release, or the path setting is wrong. |
| `no signature file (X.dll.sig)` | The module wasn't signed (or the `.sig` wasn't copied along with the DLL). |
| `signed by a different certificate` | Signed with another key than the one whose `.cer` the host trusts. |
| `the signature does not match the file (modified after signing, or signed for a different file name)` | The DLL changed after signing, or the file was renamed. |
| `the trusted certificate is not valid at this time` | The trusted certificate has expired (or isn't valid yet) - rotate it. |
| `assembly 'X' is already loaded by the host` | A plugin-folder DLL duplicates a host assembly - remove it. |
