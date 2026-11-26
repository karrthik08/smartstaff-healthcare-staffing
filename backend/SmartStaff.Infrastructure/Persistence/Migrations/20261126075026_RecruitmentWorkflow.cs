using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecruitmentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicianProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProfessionalTitle = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    PreferredLocations = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EmploymentPreference = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AvailabilityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ProfileStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicianProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicianProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicianCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CredentialName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IssuingOrganization = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CredentialNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VerifiedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicianCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicianCredentials_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RecruiterId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobRequisitions_JobRequisitionId",
                        column: x => x.JobRequisitionId,
                        principalTable: "JobRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistory_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Placements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AgreedPayRate = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Placements", x => x.Id);
                    table.CheckConstraint("CK_Placements_PayRate", "\"AgreedPayRate\" > 0");
                    table.ForeignKey(
                        name: "FK_Placements_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Placements_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Placements_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistory_JobApplicationId",
                table: "ApplicationStatusHistory",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianCredentials_ClinicianProfileId",
                table: "ClinicianCredentials",
                column: "ClinicianProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianProfiles_Specialty",
                table: "ClinicianProfiles",
                column: "Specialty");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianProfiles_UserId",
                table: "ClinicianProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ClinicianProfileId",
                table: "JobApplications",
                column: "ClinicianProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobRequisitionId_ClinicianProfileId",
                table: "JobApplications",
                columns: new[] { "JobRequisitionId", "ClinicianProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_Status",
                table: "JobApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Placements_ClinicianProfileId",
                table: "Placements",
                column: "ClinicianProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Placements_FacilityId",
                table: "Placements",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Placements_JobApplicationId",
                table: "Placements",
                column: "JobApplicationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationStatusHistory");

            migrationBuilder.DropTable(
                name: "ClinicianCredentials");

            migrationBuilder.DropTable(
                name: "Placements");

            migrationBuilder.DropTable(
                name: "JobApplications");

            migrationBuilder.DropTable(
                name: "ClinicianProfiles");
        }
    }
}
