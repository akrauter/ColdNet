using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdNet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobLogEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModuleOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ModuleTypeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobLogEntries_JobId_TimestampUtc",
                table: "JobLogEntries",
                columns: new[] { "JobId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobLogEntries");
        }
    }
}
