using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientDocumentVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VisibilityType",
                table: "client_documents",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "ClientScoped");

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibilityUpdatedAtUtc",
                table: "client_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisibilityUpdatedByUserId",
                table: "client_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "client_document_allowed_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_document_allowed_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_document_allowed_roles_client_documents_ClientDocume~",
                        column: x => x.ClientDocumentId,
                        principalTable: "client_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_document_allowed_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_document_allowed_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_document_allowed_users_client_documents_ClientDocume~",
                        column: x => x.ClientDocumentId,
                        principalTable: "client_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_ClientId_VisibilityType",
                table: "client_documents",
                columns: new[] { "ClientId", "VisibilityType" });

            migrationBuilder.CreateIndex(
                name: "IX_client_document_allowed_roles_ClientDocumentId",
                table: "client_document_allowed_roles",
                column: "ClientDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_client_document_allowed_roles_DocumentId_RoleName",
                table: "client_document_allowed_roles",
                columns: new[] { "ClientDocumentId", "RoleName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_document_allowed_users_ClientDocumentId",
                table: "client_document_allowed_users",
                column: "ClientDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_client_document_allowed_users_DocumentId_UserId",
                table: "client_document_allowed_users",
                columns: new[] { "ClientDocumentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_document_allowed_users_UserId",
                table: "client_document_allowed_users",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "client_document_allowed_roles");

            migrationBuilder.DropTable(
                name: "client_document_allowed_users");

            migrationBuilder.DropIndex(
                name: "IX_client_documents_ClientId_VisibilityType",
                table: "client_documents");

            migrationBuilder.DropColumn(
                name: "VisibilityType",
                table: "client_documents");

            migrationBuilder.DropColumn(
                name: "VisibilityUpdatedAtUtc",
                table: "client_documents");

            migrationBuilder.DropColumn(
                name: "VisibilityUpdatedByUserId",
                table: "client_documents");
        }
    }
}
