using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdNet.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessChainId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FilePrefix = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WorkDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentModuleOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ErrorModuleOrder = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessChains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRunning = table.Column<bool>(type: "INTEGER", nullable: false),
                    WorkerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JobsPerStep = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessChains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessChains_ProcessGroups_ProcessGroupId",
                        column: x => x.ProcessGroupId,
                        principalTable: "ProcessGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProcessChainId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    ModuleTypeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Append = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeleteSourceFile = table.Column<bool>(type: "INTEGER", nullable: false),
                    Directory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileExtension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MaskForDms = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutputDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    OutputFileExtension = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Save = table.Column<bool>(type: "INTEGER", nullable: false),
                    DmsDocumentType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DmsSupportEnabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleInstances_ProcessChains_ProcessChainId",
                        column: x => x.ProcessChainId,
                        principalTable: "ProcessChains",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProcessChainId_FilePrefix",
                table: "Jobs",
                columns: new[] { "ProcessChainId", "FilePrefix" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProcessChainId_Status_CurrentModuleOrder",
                table: "Jobs",
                columns: new[] { "ProcessChainId", "Status", "CurrentModuleOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleInstances_ProcessChainId_Order",
                table: "ModuleInstances",
                columns: new[] { "ProcessChainId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessChains_ProcessGroupId",
                table: "ProcessChains",
                column: "ProcessGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessChains_WorkerName",
                table: "ProcessChains",
                column: "WorkerName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "ModuleInstances");

            migrationBuilder.DropTable(
                name: "ProcessChains");

            migrationBuilder.DropTable(
                name: "ProcessGroups");
        }
    }
}
