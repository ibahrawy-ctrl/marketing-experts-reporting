using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGovernanceItemApplicationScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationScope",
                table: "governance_items",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "User");

            // Backfill للسجلّات القائمة بأولوية المرجع السياقي الموجود فعلًا:
            // تقرير مرتبط ← RelatedReport؛ موظّف ← User؛ فريق ← Team؛ إدارة ← Department؛ وإلا يبقى User (الافتراضي).
            migrationBuilder.Sql(@"
                UPDATE governance_items SET ""ApplicationScope"" =
                    CASE
                        WHEN ""RelatedSubmissionId"" IS NOT NULL THEN 'RelatedReport'
                        WHEN ""RelatedUserId"" IS NOT NULL THEN 'User'
                        WHEN ""TeamId"" IS NOT NULL THEN 'Team'
                        WHEN ""DepartmentId"" IS NOT NULL THEN 'Department'
                        ELSE 'User'
                    END;");

            migrationBuilder.CreateIndex(
                name: "IX_governance_items_ApplicationScope",
                table: "governance_items",
                column: "ApplicationScope");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_governance_items_ApplicationScope",
                table: "governance_items");

            migrationBuilder.DropColumn(
                name: "ApplicationScope",
                table: "governance_items");
        }
    }
}
