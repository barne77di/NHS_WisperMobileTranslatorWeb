using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhisperTranslator.Web.Migrations
{
    /// <inheritdoc />
    public partial class @new : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UniqueRef",
                table: "wisper_conversations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_wisper_conversations_UniqueRef",
                table: "wisper_conversations",
                column: "UniqueRef",
                unique: true,
                filter: "[UniqueRef] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wisper_conversations_UniqueRef",
                table: "wisper_conversations");

            migrationBuilder.DropColumn(
                name: "UniqueRef",
                table: "wisper_conversations");
        }
    }
}
