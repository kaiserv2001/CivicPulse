using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicPulse.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Locations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AirQualityCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Aqi = table.Column<double>(type: "float", nullable: false),
                    Pm25 = table.Column<double>(type: "float", nullable: false),
                    Pm10 = table.Column<double>(type: "float", nullable: false),
                    No2 = table.Column<double>(type: "float", nullable: false),
                    O3 = table.Column<double>(type: "float", nullable: false),
                    DominantPollutant = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirQualityCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirQualityCaches_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    Alias = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SavedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FavoriteLocations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WeatherCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LocationId = table.Column<int>(type: "int", nullable: false),
                    TemperatureCelsius = table.Column<double>(type: "float", nullable: false),
                    WindSpeedKmh = table.Column<double>(type: "float", nullable: false),
                    PrecipitationMm = table.Column<double>(type: "float", nullable: false),
                    WeatherCode = table.Column<int>(type: "int", nullable: false),
                    UvIndex = table.Column<double>(type: "float", nullable: false),
                    RelativeHumidity = table.Column<double>(type: "float", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherCaches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WeatherCaches_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirQualityCaches_LocationId_FetchedAt",
                table: "AirQualityCaches",
                columns: new[] { "LocationId", "FetchedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteLocations_LocationId",
                table: "FavoriteLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FavoriteLocations_UserId_LocationId",
                table: "FavoriteLocations",
                columns: new[] { "UserId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_Latitude_Longitude",
                table: "Locations",
                columns: new[] { "Latitude", "Longitude" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WeatherCaches_LocationId_FetchedAt",
                table: "WeatherCaches",
                columns: new[] { "LocationId", "FetchedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirQualityCaches");

            migrationBuilder.DropTable(
                name: "FavoriteLocations");

            migrationBuilder.DropTable(
                name: "WeatherCaches");

            migrationBuilder.DropTable(
                name: "Locations");
        }
    }
}
