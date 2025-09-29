using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class Fix_NarratorId_Refactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_AspNetUsers_OwnerUserId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTabletops_AspNetUsers_OwnerId",
                table: "GameTabletops");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                table: "GameTabletops",
                newName: "NarratorId");

            migrationBuilder.RenameIndex(
                name: "IX_GameTabletops_OwnerId",
                table: "GameTabletops",
                newName: "IX_GameTabletops_NarratorId");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "CharacterSheets",
                newName: "PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterSheets_OwnerUserId",
                table: "CharacterSheets",
                newName: "IX_CharacterSheets_PlayerId");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "TabletopMembers",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "GameTabletopId",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Sessions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "Sessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Sessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSpectators",
                table: "GameTabletops",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "GameTabletops",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayers",
                table: "GameTabletops",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ScenarioName",
                table: "GameTabletops",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SystemName",
                table: "GameTabletops",
                type: "TEXT",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_AspNetUsers_PlayerId",
                table: "CharacterSheets",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTabletops_AspNetUsers_NarratorId",
                table: "GameTabletops",
                column: "NarratorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_AspNetUsers_PlayerId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_GameTabletops_AspNetUsers_NarratorId",
                table: "GameTabletops");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "AllowSpectators",
                table: "GameTabletops");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "GameTabletops");

            migrationBuilder.DropColumn(
                name: "MaxPlayers",
                table: "GameTabletops");

            migrationBuilder.DropColumn(
                name: "ScenarioName",
                table: "GameTabletops");

            migrationBuilder.DropColumn(
                name: "SystemName",
                table: "GameTabletops");

            migrationBuilder.RenameColumn(
                name: "NarratorId",
                table: "GameTabletops",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_GameTabletops_NarratorId",
                table: "GameTabletops",
                newName: "IX_GameTabletops_OwnerId");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                table: "CharacterSheets",
                newName: "OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterSheets_PlayerId",
                table: "CharacterSheets",
                newName: "IX_CharacterSheets_OwnerUserId");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "TabletopMembers",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "GameTabletopId",
                table: "Sessions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_AspNetUsers_OwnerUserId",
                table: "CharacterSheets",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GameTabletops_AspNetUsers_OwnerId",
                table: "GameTabletops",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
