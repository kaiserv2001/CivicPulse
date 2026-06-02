using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPushSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NotifiedLowScoreAt",
                table: "FavoriteLocations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId",
                table: "PushSubscriptions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "NotifiedLowScoreAt",
                table: "FavoriteLocations");
        }
    }
}
