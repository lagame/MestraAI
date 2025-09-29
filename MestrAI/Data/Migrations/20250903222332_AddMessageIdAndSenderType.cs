using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPGSessionManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageIdAndSenderType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MessageId",
                table: "ChatMessages",
                type: "TEXT",
                maxLength: 26,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SenderType",
                table: "ChatMessages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SenderType",
                table: "ChatMessages");
        }
    }
}
