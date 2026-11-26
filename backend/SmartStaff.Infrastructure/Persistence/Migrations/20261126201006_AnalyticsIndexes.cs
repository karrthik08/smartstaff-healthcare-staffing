using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_Status_SubmittedAt",
                table: "Timesheets",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Status_StartDateTime",
                table: "Shifts",
                columns: new[] { "Status", "StartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Placements_CreatedAt",
                table: "Placements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_AppliedAt",
                table: "JobApplications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianCredentials_VerificationStatus_ExpirationDate",
                table: "ClinicianCredentials",
                columns: new[] { "VerificationStatus", "ExpirationDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Timesheets_Status_SubmittedAt",
                table: "Timesheets");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_Status_StartDateTime",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Placements_CreatedAt",
                table: "Placements");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_AppliedAt",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_ClinicianCredentials_VerificationStatus_ExpirationDate",
                table: "ClinicianCredentials");
        }
    }
}
