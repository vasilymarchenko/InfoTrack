using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfoTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSightingTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tier",
                table: "Sightings",
                type: "text",
                nullable: false,
                defaultValue: "Featured");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Sightings");
        }
    }
}
