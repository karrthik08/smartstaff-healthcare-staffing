using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchedulingAndShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "HealthcareFacilities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "America/Chicago");

            migrationBuilder.CreateTable(
                name: "ClinicianAvailability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailabilityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicianAvailability", x => x.Id);
                    table.CheckConstraint("CK_ClinicianAvailability_Range", "\"AvailableUntil\" > \"AvailableFrom\"");
                    table.ForeignKey(
                        name: "FK_ClinicianAvailability_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityLocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobRequisitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RequiredClinicians = table.Column<int>(type: "integer", nullable: false),
                    Specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.CheckConstraint("CK_Shifts_TimeRange", "\"EndDateTime\" > \"StartDateTime\"");
                    table.ForeignKey(
                        name: "FK_Shifts_FacilityLocations_FacilityLocationId",
                        column: x => x.FacilityLocationId,
                        principalTable: "FacilityLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Shifts_JobRequisitions_JobRequisitionId",
                        column: x => x.JobRequisitionId,
                        principalTable: "JobRequisitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AssignedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Placements_PlacementId",
                        column: x => x.PlacementId,
                        principalTable: "Placements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShiftChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RequestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftChangeRequests_ShiftAssignments_ShiftAssignmentId",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianAvailability_ClinicianProfileId_AvailableFrom",
                table: "ClinicianAvailability",
                columns: new[] { "ClinicianProfileId", "AvailableFrom" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ClinicianProfileId",
                table: "ShiftAssignments",
                column: "ClinicianProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_PlacementId",
                table: "ShiftAssignments",
                column: "PlacementId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId_ClinicianProfileId",
                table: "ShiftAssignments",
                columns: new[] { "ShiftId", "ClinicianProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftChangeRequests_ShiftAssignmentId",
                table: "ShiftChangeRequests",
                column: "ShiftAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_FacilityId_StartDateTime",
                table: "Shifts",
                columns: new[] { "FacilityId", "StartDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_FacilityLocationId",
                table: "Shifts",
                column: "FacilityLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_JobRequisitionId",
                table: "Shifts",
                column: "JobRequisitionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicianAvailability");

            migrationBuilder.DropTable(
                name: "ShiftChangeRequests");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "HealthcareFacilities");
        }
    }
}
