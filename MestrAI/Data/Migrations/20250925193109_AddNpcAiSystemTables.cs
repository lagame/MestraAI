using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcAiSystemTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiNpcStateChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    PropertyChanged = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "TEXT", nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", nullable: true),
                    ChangedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ChangeSource = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiNpcStateChanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NpcLongTermMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MemoryType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Importance = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: true),
                    RelatedEntities = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcLongTermMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcLongTermMemories_CharacterSheets_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcLongTermMemories_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionAiCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: false),
                    AiCharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    PersonalitySettings = table.Column<string>(type: "TEXT", nullable: true),
                    InteractionFrequency = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionAiCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionAiCharacters_CharacterSheets_AiCharacterId",
                        column: x => x.AiCharacterId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionAiCharacters_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiNpcStateChange_ChangedAt",
                table: "AiNpcStateChanges",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AiNpcStateChange_ChangedBy",
                table: "AiNpcStateChanges",
                column: "ChangedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AiNpcStateChange_CharacterId",
                table: "AiNpcStateChanges",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_AiNpcStateChange_Session_Character_Date",
                table: "AiNpcStateChanges",
                columns: new[] { "SessionId", "CharacterId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiNpcStateChange_SessionId",
                table: "AiNpcStateChanges",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcLongTermMemory_Character_Importance_Active",
                table: "NpcLongTermMemories",
                columns: new[] { "CharacterId", "Importance", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NpcLongTermMemory_Character_Type_Active",
                table: "NpcLongTermMemories",
                columns: new[] { "CharacterId", "MemoryType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NpcLongTermMemory_CharacterId",
                table: "NpcLongTermMemories",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcLongTermMemory_LastAccessed",
                table: "NpcLongTermMemories",
                column: "LastAccessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NpcLongTermMemory_SessionId",
                table: "NpcLongTermMemories",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiCharacter_CharacterId",
                table: "SessionAiCharacters",
                column: "AiCharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiCharacter_Session_Active_Visible",
                table: "SessionAiCharacters",
                columns: new[] { "SessionId", "IsActive", "IsVisible" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiCharacter_Session_Character",
                table: "SessionAiCharacters",
                columns: new[] { "SessionId", "AiCharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionAiCharacter_SessionId",
                table: "SessionAiCharacters",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiNpcStateChanges");

            migrationBuilder.DropTable(
                name: "NpcLongTermMemories");

            migrationBuilder.DropTable(
                name: "SessionAiCharacters");
        }
    }
}
