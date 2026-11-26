using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceAndTimesheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClockInTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClockOutTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttendanceStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.CheckConstraint("CK_Attendance_TimeRange", "\"ClockOutTime\" IS NULL OR \"ClockOutTime\" > \"ClockInTime\"");
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_ShiftAssignments_ShiftAssignmentId",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OvertimePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JurisdictionCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RegularHoursPerDay = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    RegularHoursPerPeriod = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvertimePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Timesheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicianProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalRegularHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    TotalOvertimeHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timesheets", x => x.Id);
                    table.CheckConstraint("CK_Timesheets_Hours", "\"TotalRegularHours\" >= 0 AND \"TotalOvertimeHours\" >= 0");
                    table.CheckConstraint("CK_Timesheets_Period", "\"PeriodEnd\" > \"PeriodStart\"");
                    table.ForeignKey(
                        name: "FK_Timesheets_ClinicianProfiles_ClinicianProfileId",
                        column: x => x.ClinicianProfileId,
                        principalTable: "ClinicianProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Timesheets_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetApprovalHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetApprovalHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimesheetApprovalHistory_Timesheets_TimesheetId",
                        column: x => x.TimesheetId,
                        principalTable: "Timesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BreakMinutes = table.Column<int>(type: "integer", nullable: false),
                    RegularHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetEntries", x => x.Id);
                    table.CheckConstraint("CK_TimesheetEntries_Break", "\"BreakMinutes\" >= 0");
                    table.CheckConstraint("CK_TimesheetEntries_Hours", "\"RegularHours\" >= 0 AND \"OvertimeHours\" >= 0");
                    table.CheckConstraint("CK_TimesheetEntries_Time", "\"EndTime\" > \"StartTime\"");
                    table.ForeignKey(
                        name: "FK_TimesheetEntries_ShiftAssignments_ShiftAssignmentId",
                        column: x => x.ShiftAssignmentId,
                        principalTable: "ShiftAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimesheetEntries_Timesheets_TimesheetId",
                        column: x => x.TimesheetId,
                        principalTable: "Timesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OriginalHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    AdjustedHours = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimesheetAdjustments_TimesheetEntries_TimesheetEntryId",
                        column: x => x.TimesheetEntryId,
                        principalTable: "TimesheetEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OvertimePolicies",
                columns: new[] { "Id", "CreatedAt", "IsActive", "JurisdictionCode", "RegularHoursPerDay", "RegularHoursPerPeriod" },
                values: new object[,]
                {
                    { new Guid("6f4e2a10-7c1d-4b8a-9e11-0a1b2c3d4e51"), new DateTimeOffset(new DateTime(2026, 11, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "US-FED", 8m, 40m },
                    { new Guid("6f4e2a10-7c1d-4b8a-9e11-0a1b2c3d4e52"), new DateTimeOffset(new DateTime(2026, 11, 26, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "TX", 8m, 40m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ClinicianProfileId_ClockInTime",
                table: "AttendanceRecords",
                columns: new[] { "ClinicianProfileId", "ClockInTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_ShiftAssignmentId",
                table: "AttendanceRecords",
                column: "ShiftAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OvertimePolicies_JurisdictionCode",
                table: "OvertimePolicies",
                column: "JurisdictionCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetAdjustments_TimesheetEntryId",
                table: "TimesheetAdjustments",
                column: "TimesheetEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetApprovalHistory_TimesheetId",
                table: "TimesheetApprovalHistory",
                column: "TimesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_ShiftAssignmentId",
                table: "TimesheetEntries",
                column: "ShiftAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetEntries_TimesheetId",
                table: "TimesheetEntries",
                column: "TimesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_ClinicianProfileId_FacilityId_PeriodStart",
                table: "Timesheets",
                columns: new[] { "ClinicianProfileId", "FacilityId", "PeriodStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_FacilityId_Status",
                table: "Timesheets",
                columns: new[] { "FacilityId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "OvertimePolicies");

            migrationBuilder.DropTable(
                name: "TimesheetAdjustments");

            migrationBuilder.DropTable(
                name: "TimesheetApprovalHistory");

            migrationBuilder.DropTable(
                name: "TimesheetEntries");

            migrationBuilder.DropTable(
                name: "Timesheets");
        }
    }
}
