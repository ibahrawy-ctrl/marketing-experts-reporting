using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientDocumentsAndExternalLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_external_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_external_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_external_links_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_document_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScanEngine = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScanDetail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_document_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "client_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CategoryCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfidentialityCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LifecycleStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ApprovalStatusCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    VersionCount = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArchiveReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeleteReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_documents_client_document_versions_CurrentVersionId",
                        column: x => x.CurrentVersionId,
                        principalTable: "client_document_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_documents_clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_client_document_versions_ClientDocumentId_Sha256",
                table: "client_document_versions",
                columns: new[] { "ClientDocumentId", "Sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_client_document_versions_DocumentId_Current",
                table: "client_document_versions",
                column: "ClientDocumentId",
                unique: true,
                filter: "\"IsCurrent\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_client_document_versions_DocumentId_VersionNo",
                table: "client_document_versions",
                columns: new[] { "ClientDocumentId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_document_versions_StorageKey",
                table: "client_document_versions",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_ClientId",
                table: "client_documents",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_ClientId_CategoryCode",
                table: "client_documents",
                columns: new[] { "ClientId", "CategoryCode" });

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_ClientId_Visibility",
                table: "client_documents",
                columns: new[] { "ClientId", "IsArchived", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_client_documents_CurrentVersionId",
                table: "client_documents",
                column: "CurrentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_client_external_links_ClientId",
                table: "client_external_links",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_client_external_links_ClientId_CategoryCode",
                table: "client_external_links",
                columns: new[] { "ClientId", "CategoryCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_client_document_versions_client_documents_ClientDocumentId",
                table: "client_document_versions",
                column: "ClientDocumentId",
                principalTable: "client_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_client_document_versions_client_documents_ClientDocumentId",
                table: "client_document_versions");

            migrationBuilder.DropTable(
                name: "client_external_links");

            migrationBuilder.DropTable(
                name: "client_documents");

            migrationBuilder.DropTable(
                name: "client_document_versions");
        }
    }
}
