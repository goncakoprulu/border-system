using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Border.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3ClassOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "StudioRooms",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "StudioRooms",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudioRooms_IsActive_IsDeleted",
                table: "StudioRooms",
                columns: new[] { "IsActive", "IsDeleted" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_StudioClasses_Capacity",
                table: "StudioClasses",
                sql: "\"Capacity\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_ClassSchedules_StudioClassId_DayOfWeek_StartTime_EndTime",
                table: "ClassSchedules",
                columns: new[] { "StudioClassId", "DayOfWeek", "StartTime", "EndTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudioRooms_IsActive_IsDeleted",
                table: "StudioRooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StudioClasses_Capacity",
                table: "StudioClasses");

            migrationBuilder.DropIndex(
                name: "IX_ClassSchedules_StudioClassId_DayOfWeek_StartTime_EndTime",
                table: "ClassSchedules");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "StudioRooms");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "StudioRooms");
        }
    }
}
