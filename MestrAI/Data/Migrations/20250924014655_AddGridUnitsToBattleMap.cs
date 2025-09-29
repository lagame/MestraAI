using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGridUnitsToBattleMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GridUnit",
                table: "BattleMaps",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "GridUnitValue",
                table: "BattleMaps",
                type: "REAL",
                nullable: false,
                defaultValue: 0f);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GridUnit",
                table: "BattleMaps");

            migrationBuilder.DropColumn(
                name: "GridUnitValue",
                table: "BattleMaps");
        }
    }
}
