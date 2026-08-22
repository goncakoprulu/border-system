using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Border.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueLessonSessionSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                WITH ranked_attendances AS (
                    SELECT attendance."Id",
                           ROW_NUMBER() OVER (
                               PARTITION BY session."StudioClassId", session."ScheduledStart", attendance."StudentId"
                               ORDER BY CASE WHEN session."Status" = 1 THEN 0 ELSE 1 END, session."Id", attendance."Id"
                           ) AS row_number
                    FROM "Attendances" AS attendance
                    INNER JOIN "LessonSessions" AS session ON session."Id" = attendance."LessonSessionId"
                )
                DELETE FROM "Attendances" AS duplicate_attendance
                USING ranked_attendances
                WHERE ranked_attendances.row_number > 1
                  AND duplicate_attendance."Id" = ranked_attendances."Id";

                WITH ranked_sessions AS (
                    SELECT "Id",
                           FIRST_VALUE("Id") OVER (PARTITION BY "StudioClassId", "ScheduledStart" ORDER BY CASE WHEN "Status" = 1 THEN 0 ELSE 1 END, "Id") AS keeper_id,
                           ROW_NUMBER() OVER (PARTITION BY "StudioClassId", "ScheduledStart" ORDER BY CASE WHEN "Status" = 1 THEN 0 ELSE 1 END, "Id") AS row_number
                    FROM "LessonSessions"
                )
                UPDATE "Attendances" AS attendance
                SET "LessonSessionId" = duplicate_session.keeper_id
                FROM ranked_sessions AS duplicate_session
                WHERE duplicate_session.row_number > 1
                  AND attendance."LessonSessionId" = duplicate_session."Id";

                WITH ranked_sessions AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "StudioClassId", "ScheduledStart" ORDER BY CASE WHEN "Status" = 1 THEN 0 ELSE 1 END, "Id") AS row_number
                    FROM "LessonSessions"
                )
                DELETE FROM "LessonSessions" AS duplicate_session
                USING ranked_sessions
                WHERE ranked_sessions.row_number > 1
                  AND duplicate_session."Id" = ranked_sessions."Id";
                """);

            migrationBuilder.DropIndex(
                name: "IX_LessonSessions_StudioClassId_ScheduledStart",
                table: "LessonSessions");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessions_StudioClassId_ScheduledStart",
                table: "LessonSessions",
                columns: new[] { "StudioClassId", "ScheduledStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LessonSessions_StudioClassId_ScheduledStart",
                table: "LessonSessions");

            migrationBuilder.CreateIndex(
                name: "IX_LessonSessions_StudioClassId_ScheduledStart",
                table: "LessonSessions",
                columns: new[] { "StudioClassId", "ScheduledStart" });
        }
    }
}
