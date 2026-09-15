using ColdNet.Core.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ColdNet.Data;

public static class DbInitializer
{
    /// <summary>Applies pending migrations and seeds the built-in "default" process group and sample workflows, mirroring d.cold's always-present default group.</summary>
    public static async Task MigrateAndSeedAsync(ColdNetDbContext db, CancellationToken cancellationToken = default)
    {
        EnsureSqliteDirectoryExists(db);

        await db.Database.MigrateAsync(cancellationToken);

        var defaultGroup = await db.ProcessGroups
            .Include(g => g.Chains)
            .FirstOrDefaultAsync(g => g.IsDefault, cancellationToken);

        if (defaultGroup is null)
        {
            defaultGroup = new ProcessGroup
            {
                Name = "default",
                ShortName = "default",
                Description = "Built-in default process group.",
                IsDefault = true,
            };
            db.ProcessGroups.Add(defaultGroup);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedSampleWorkflowsAsync(db, defaultGroup, cancellationToken);
    }

    private static async Task SeedSampleWorkflowsAsync(ColdNetDbContext db, ProcessGroup defaultGroup, CancellationToken cancellationToken)
    {
        var hasChanges = false;

        if (!await db.ProcessChains.AnyAsync(c => c.Name == "Office-zu-PDF", cancellationToken))
        {
            var officeChain = new ProcessChain
            {
                ProcessGroupId = defaultGroup.Id,
                Name = "Office-zu-PDF",
                Description = "Konvertiert Office-Dokumente (.docx) in PDF via LibreOffice und legt Quelldokument und PDF im Ausgabeverzeichnis ab.",
                WorkerName = "default",
                JobsPerStep = 10,
                Enabled = true,
                IsRunning = true,
                Modules =
                [
                    new ModuleInstance
                    {
                        Order = 0,
                        ModuleTypeName = "ColdImport",
                        DisplayName = "1. Office-Dokumente importieren",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            Directory = "data/input/office",
                            Save = false,
                        },
                        SettingsJson = """{"FileMask":"*.docx","GenerateUniqueJobId":false,"SearchSubdirectories":false,"IgnoreZeroByteFiles":true}""",
                    },
                    new ModuleInstance
                    {
                        Order = 1,
                        ModuleTypeName = "RenameFiles",
                        DisplayName = "2. Import-Marker entfernen ($docx -> docx)",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings(),
                        SettingsJson = """{"FromExtension":"$docx","ToExtension":"docx"}""",
                    },
                    new ModuleInstance
                    {
                        Order = 2,
                        ModuleTypeName = "OfficeToPdf",
                        DisplayName = "3. Office zu PDF konvertieren",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            FileExtension = "docx",
                            OutputFileExtension = "pdf",
                            DeleteSourceFile = false,
                        },
                        SettingsJson = """{"LibreOfficePath":"soffice","TimeoutSeconds":120}""",
                    },
                    new ModuleInstance
                    {
                        Order = 3,
                        ModuleTypeName = "FileMover",
                        DisplayName = "4. Quelldokument & PDF in Ausgabe verschieben",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            OutputDirectory = "data/output/office",
                        },
                        SettingsJson = """{"IncludeSubdirectories":false,"OverwriteExisting":true}""",
                    },
                ],
            };

            db.ProcessChains.Add(officeChain);
            hasChanges = true;
        }

        if (!await db.ProcessChains.AnyAsync(c => c.Name == "Text-zu-PDF", cancellationToken))
        {
            var textChain = new ProcessChain
            {
                ProcessGroupId = defaultGroup.Id,
                Name = "Text-zu-PDF",
                Description = "Konvertiert Textdateien (.txt) in PDF und legt Quelldatei und PDF im Ausgabeverzeichnis ab.",
                WorkerName = "default",
                JobsPerStep = 10,
                Enabled = true,
                IsRunning = true,
                Modules =
                [
                    new ModuleInstance
                    {
                        Order = 0,
                        ModuleTypeName = "ColdImport",
                        DisplayName = "1. Textdateien importieren",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            Directory = "data/input/text",
                            Save = false,
                        },
                        SettingsJson = """{"FileMask":"*.txt","GenerateUniqueJobId":false,"SearchSubdirectories":false,"IgnoreZeroByteFiles":true}""",
                    },
                    new ModuleInstance
                    {
                        Order = 1,
                        ModuleTypeName = "RenameFiles",
                        DisplayName = "2. Import-Marker entfernen ($txt -> txt)",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings(),
                        SettingsJson = """{"FromExtension":"$txt","ToExtension":"txt"}""",
                    },
                    new ModuleInstance
                    {
                        Order = 2,
                        ModuleTypeName = "TextToPdf",
                        DisplayName = "3. Text zu PDF konvertieren",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            FileExtension = "txt",
                            OutputFileExtension = "pdf",
                            DeleteSourceFile = false,
                        },
                        SettingsJson = """{"FontFamily":"Courier","FontSize":10,"Landscape":false,"MarginPoints":36}""",
                    },
                    new ModuleInstance
                    {
                        Order = 3,
                        ModuleTypeName = "FileMover",
                        DisplayName = "4. Quelldokument & PDF in Ausgabe verschieben",
                        Enabled = true,
                        CommonSettings = new CommonModuleSettings
                        {
                            OutputDirectory = "data/output/text",
                        },
                        SettingsJson = """{"IncludeSubdirectories":false,"OverwriteExisting":true}""",
                    },
                ],
            };

            db.ProcessChains.Add(textChain);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// SQLite (unlike SQL Server) never creates a missing directory for its data file, it just
    /// fails with "unable to open database file" - bites every fresh deployment (a published
    /// release, a first `dotnet run`, ...) where the configured "data" folder doesn't exist yet.
    /// </summary>
    private static void EnsureSqliteDirectoryExists(ColdNetDbContext db)
    {
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
