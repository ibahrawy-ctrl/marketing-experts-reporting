using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClient360Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientTypeCode",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "clients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "RelationshipStartDate",
                table: "clients",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SectorCode",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceCode",
                table: "clients",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeNameEn",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "clients",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_brand_profiles",
                columns: table => new
                {
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessOverview = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProductsAndServices = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TargetAudience = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TargetLocations = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UniqueSellingProposition = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Strengths = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Competitors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ToneOfVoice = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreferredMessages = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProhibitedMessages = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    BrandGuidelinesUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_brand_profiles", x => x.ClientId);
                    table.ForeignKey(
                        name: "FK_client_brand_profiles_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_contacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Department = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WhatsApp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PreferredContactMethodCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsFinancialContact = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_contacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_contacts_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_digital_channels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlatformCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UsernameOrHandle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProfileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AccessStatusCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessManagerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AdAccountId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PixelId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ga4PropertyId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GtmContainerId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_digital_channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_digital_channels_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_contacts_ClientId_ActivePrimary",
                table: "client_contacts",
                column: "ClientId",
                unique: true,
                filter: "\"IsPrimary\" = true AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_client_digital_channels_ClientId",
                table: "client_digital_channels",
                column: "ClientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_brand_profiles");

            migrationBuilder.DropTable(
                name: "client_contacts");

            migrationBuilder.DropTable(
                name: "client_digital_channels");

            migrationBuilder.DropColumn(
                name: "City",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "ClientTypeCode",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "RelationshipStartDate",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "SectorCode",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "SourceCode",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "TradeNameEn",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "clients");
        }
    }
}
