# Chain templates

Exported process chains (`.coldchain.json`, via the Admin UI's "Export" button on a chain, or
`/api/export/chains/{id}`) kept here as reusable starting points and backups. Any `[SensitiveValue]`
field (e.g. an SFTP password) is redacted on export - re-enter it after importing.

Import one via the Admin UI's process-group-level "Import chain" button, or `POST` the file's
content to the running app's import endpoint.

| File | Notes |
|---|---|
| `Extract Values by Key.coldchain.json` | Key/value extraction from PDF/image files - `ExtractText` -> `ParseProperties` -> `PropertiesToCsv`. |
| `Office-zu-PDF.coldchain.json` | Office documents to PDF via LibreOffice. |
| `Text-zu-PDF.coldchain.json` | Text files to PDF. |
| `Transfer to SFTP.coldchain.json` | Uploads finished jobs to a remote SFTP/FTPS/FTP server. |
| `UnzipFiles.coldchain.json` | Extracts incoming ZIP archives. |
