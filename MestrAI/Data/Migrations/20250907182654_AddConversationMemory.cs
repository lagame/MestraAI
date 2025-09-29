using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameTabletopId = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionId = table.Column<int>(type: "INTEGER", nullable: true),
                    SpeakerName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SpeakerType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Context = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Importance = table.Column<int>(type: "INTEGER", nullable: false),
                    EmbeddingVector = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMemories_GameTabletops_GameTabletopId",
                        column: x => x.GameTabletopId,
                        principalTable: "GameTabletops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMemories_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_ContentHash",
                table: "ConversationMemories",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_CreatedAt",
                table: "ConversationMemories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_GameTabletopId",
                table: "ConversationMemories",
                column: "GameTabletopId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_GameTabletopId_IsActive_Importance",
                table: "ConversationMemories",
                columns: new[] { "GameTabletopId", "IsActive", "Importance" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_Importance",
                table: "ConversationMemories",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_IsActive",
                table: "ConversationMemories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_SessionId",
                table: "ConversationMemories",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_SpeakerName",
                table: "ConversationMemories",
                column: "SpeakerName");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMemories_SpeakerType",
                table: "ConversationMemories",
                column: "SpeakerType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationMemories");
        }
    }
}
