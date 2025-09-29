using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SessionCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "SessionCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SessionCharacters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "SessionAiCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "SessionAiCharacters",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "SessionAiCharacters",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "NpcLongTermMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "NpcLongTermMemories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "NpcLongTermMemories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Media",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "Media",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Media",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "MapTokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "MapTokens",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "MapTokens",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CharacterSheets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "CharacterSheets",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CharacterSheets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "BattleMaps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "BattleMaps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "BattleMaps",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SessionCharacters");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "SessionCharacters");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SessionCharacters");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "SessionAiCharacters");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "SessionAiCharacters");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "SessionAiCharacters");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "NpcLongTermMemories");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "NpcLongTermMemories");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "NpcLongTermMemories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "MapTokens");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "MapTokens");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "MapTokens");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "BattleMaps");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "BattleMaps");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "BattleMaps");
        }
    }
}
