using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FacilityAndRequisition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthcareFacilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FacilityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthcareFacilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacilityLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ZipCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityLocations_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MembershipRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityMemberships_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JobRequisitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EmploymentType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ShiftType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequiredExperience = table.Column<int>(type: "integer", nullable: false),
                    RequiredCertifications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OpenPositions = table.Column<int>(type: "integer", nullable: false),
                    PayRateMin = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PayRateMax = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    AssignedRecruiterId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRequisitions", x => x.Id);
                    table.CheckConstraint("CK_JobRequisitions_PayRange", "\"PayRateMax\" >= \"PayRateMin\"");
                    table.ForeignKey(
                        name: "FK_JobRequisitions_FacilityLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "FacilityLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_HealthcareFacilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "HealthcareFacilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Users_AssignedRecruiterId",
                        column: x => x.AssignedRecruiterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobRequisitions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityLocations_FacilityId",
                table: "FacilityLocations",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityMemberships_FacilityId_UserId",
                table: "FacilityMemberships",
                columns: new[] { "FacilityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityMemberships_UserId",
                table: "FacilityMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthcareFacilities_Name",
                table: "HealthcareFacilities",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_HealthcareFacilities_State_City",
                table: "HealthcareFacilities",
                columns: new[] { "State", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_AssignedRecruiterId",
                table: "JobRequisitions",
                column: "AssignedRecruiterId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_CreatedBy",
                table: "JobRequisitions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_FacilityId",
                table: "JobRequisitions",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_LocationId",
                table: "JobRequisitions",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_Specialty",
                table: "JobRequisitions",
                column: "Specialty");

            migrationBuilder.CreateIndex(
                name: "IX_JobRequisitions_Status",
                table: "JobRequisitions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityMemberships");

            migrationBuilder.DropTable(
                name: "JobRequisitions");

            migrationBuilder.DropTable(
                name: "FacilityLocations");

            migrationBuilder.DropTable(
                name: "HealthcareFacilities");
        }
    }
}
