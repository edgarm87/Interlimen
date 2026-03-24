using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDetentionStaysAndIceDetainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UniqueIdentifier",
                table: "enforcement_actions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "detention_stays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StayBookInDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacilityBookInDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacilityBookOutDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StayBookOutDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FacilityReleaseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StayReleaseReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaseStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaseCategory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HasFinalOrder = table.Column<bool>(type: "boolean", nullable: true),
                    CaseThreatLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BookInCriminality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CitizenshipCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    EntryStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InitialBondAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    BondPostedAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DataSource = table.Column<int>(type: "integer", nullable: false),
                    UniqueIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_detention_stays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_detention_stays_detention_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "detention_facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ice_detainers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DetainerPrepareDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FacilityState = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    FacilityAor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetentionFacilityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetentionFacilityCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FacilityCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PortOfDeparture = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DepartureCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DepartedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CaseStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaseCategory = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetainerCriminality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ThreatLevel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProcessingDisposition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FinalProgram = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    HasFinalOrder = table.Column<bool>(type: "boolean", nullable: true),
                    FinalOrderDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprehensionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CitizenshipCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BirthCountry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BirthYear = table.Column<int>(type: "integer", nullable: true),
                    Gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    EntryStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsFelon = table.Column<bool>(type: "boolean", nullable: true),
                    PriorFelony = table.Column<bool>(type: "boolean", nullable: true),
                    AggravatedFelony = table.Column<bool>(type: "boolean", nullable: true),
                    ViolentMisdemeanor = table.Column<bool>(type: "boolean", nullable: true),
                    MostSeriousConvictionCharge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetainerType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DetainerLiftReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DataSource = table.Column<int>(type: "integer", nullable: false),
                    UniqueIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ice_detainers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enforcement_actions_UniqueIdentifier",
                table: "enforcement_actions",
                column: "UniqueIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_detention_stays_CitizenshipCountry",
                table: "detention_stays",
                column: "CitizenshipCountry");

            migrationBuilder.CreateIndex(
                name: "IX_detention_stays_FacilityId",
                table: "detention_stays",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_detention_stays_FacilityId_StayBookInDate",
                table: "detention_stays",
                columns: new[] { "FacilityId", "StayBookInDate" });

            migrationBuilder.CreateIndex(
                name: "IX_detention_stays_StayBookInDate",
                table: "detention_stays",
                column: "StayBookInDate");

            migrationBuilder.CreateIndex(
                name: "IX_detention_stays_UniqueIdentifier",
                table: "detention_stays",
                column: "UniqueIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_ice_detainers_CitizenshipCountry",
                table: "ice_detainers",
                column: "CitizenshipCountry");

            migrationBuilder.CreateIndex(
                name: "IX_ice_detainers_DepartureCountry",
                table: "ice_detainers",
                column: "DepartureCountry");

            migrationBuilder.CreateIndex(
                name: "IX_ice_detainers_DetainerPrepareDate",
                table: "ice_detainers",
                column: "DetainerPrepareDate");

            migrationBuilder.CreateIndex(
                name: "IX_ice_detainers_FacilityState",
                table: "ice_detainers",
                column: "FacilityState");

            migrationBuilder.CreateIndex(
                name: "IX_ice_detainers_UniqueIdentifier",
                table: "ice_detainers",
                column: "UniqueIdentifier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "detention_stays");

            migrationBuilder.DropTable(
                name: "ice_detainers");

            migrationBuilder.DropIndex(
                name: "IX_enforcement_actions_UniqueIdentifier",
                table: "enforcement_actions");

            migrationBuilder.DropColumn(
                name: "UniqueIdentifier",
                table: "enforcement_actions");
        }
    }
}
