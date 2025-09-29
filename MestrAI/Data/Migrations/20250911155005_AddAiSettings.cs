using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    GeminiProject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    GeminiLocation = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GeminiModel = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LocalEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LocalApiKey = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiSettings_IsActive",
                table: "AiSettings",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiSettings");
        }
    }
}
