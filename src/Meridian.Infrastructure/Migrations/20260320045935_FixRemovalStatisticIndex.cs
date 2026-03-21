using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meridian.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixRemovalStatisticIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_removal_statistics_Year_Month_Nationality",
                table: "removal_statistics");

            migrationBuilder.AlterColumn<string>(
                name: "Nationality",
                table: "removal_statistics",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.CreateIndex(
                name: "IX_removal_statistics_Year_Month_NationalityDisplay",
                table: "removal_statistics",
                columns: new[] { "Year", "Month", "NationalityDisplay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_removal_statistics_Year_Month_NationalityDisplay",
                table: "removal_statistics");

            migrationBuilder.AlterColumn<string>(
                name: "Nationality",
                table: "removal_statistics",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.CreateIndex(
                name: "IX_removal_statistics_Year_Month_Nationality",
                table: "removal_statistics",
                columns: new[] { "Year", "Month", "Nationality" },
                unique: true);
        }
    }
}
