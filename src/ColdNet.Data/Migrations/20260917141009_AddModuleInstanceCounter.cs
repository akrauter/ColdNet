using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ColdNet.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleInstanceCounter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Counter",
                table: "ModuleInstances",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Counter",
                table: "ModuleInstances");
        }
    }
}
