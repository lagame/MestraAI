using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Battlemap_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleMaps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    BackgroundUrl = table.Column<string>(type: "TEXT", nullable: true),
                    GridSize = table.Column<int>(type: "INTEGER", nullable: false),
                    ZoomMin = table.Column<float>(type: "REAL", nullable: false),
                    ZoomMax = table.Column<float>(type: "REAL", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleMaps_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MapTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BattleMapId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    X = table.Column<float>(type: "REAL", nullable: false),
                    Y = table.Column<float>(type: "REAL", nullable: false),
                    Scale = table.Column<float>(type: "REAL", nullable: false),
                    Rotation = table.Column<float>(type: "REAL", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    Z = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MapTokens_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MapTokens_BattleMaps_BattleMapId",
                        column: x => x.BattleMapId,
                        principalTable: "BattleMaps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleMaps_SessionId",
                table: "BattleMaps",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MapTokens_BattleMapId",
                table: "MapTokens",
                column: "BattleMapId");

            migrationBuilder.CreateIndex(
                name: "IX_MapTokens_OwnerId",
                table: "MapTokens",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapTokens");

            migrationBuilder.DropTable(
                name: "BattleMaps");
        }
    }
}
