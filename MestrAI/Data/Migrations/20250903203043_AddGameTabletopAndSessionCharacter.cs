using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameTabletopAndSessionCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GameTabletopId",
                table: "Sessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GameTabletops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameTabletops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameTabletops_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterSheetId = table.Column<int>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCharacters_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SessionCharacters_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TabletopMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTabletopId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LeftAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabletopMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TabletopMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TabletopMembers_GameTabletops_GameTabletopId",
                        column: x => x.GameTabletopId,
                        principalTable: "GameTabletops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_GameTabletopId",
                table: "Sessions",
                column: "GameTabletopId");

            migrationBuilder.CreateIndex(
                name: "IX_GameTabletops_IsDeleted",
                table: "GameTabletops",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_GameTabletops_OwnerId",
                table: "GameTabletops",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharacters_CharacterSheetId",
                table: "SessionCharacters",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharacters_SessionId",
                table: "SessionCharacters",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCharacters_SessionId_CharacterSheetId",
                table: "SessionCharacters",
                columns: new[] { "SessionId", "CharacterSheetId" },
                unique: true,
                filter: "IsActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TabletopMembers_GameTabletopId",
                table: "TabletopMembers",
                column: "GameTabletopId");

            migrationBuilder.CreateIndex(
                name: "IX_TabletopMembers_GameTabletopId_UserId",
                table: "TabletopMembers",
                columns: new[] { "GameTabletopId", "UserId" },
                unique: true,
                filter: "IsActive = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TabletopMembers_UserId",
                table: "TabletopMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_GameTabletops_GameTabletopId",
                table: "Sessions",
                column: "GameTabletopId",
                principalTable: "GameTabletops",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_GameTabletops_GameTabletopId",
                table: "Sessions");

            migrationBuilder.DropTable(
                name: "SessionCharacters");

            migrationBuilder.DropTable(
                name: "TabletopMembers");

            migrationBuilder.DropTable(
                name: "GameTabletops");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_GameTabletopId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "GameTabletopId",
                table: "Sessions");
        }
    }
}
