using Border.Domain.Entities;

namespace Border.Application.Operations;

public static class AttendanceReportingRules
{
    public static bool CountsAsAttended(AttendanceStatus status) =>
        status is AttendanceStatus.Present or AttendanceStatus.Late;

    public static decimal Rate(int present, int late, int total) =>
        RateFromAttended(present + late, total);

    public static decimal RateFromAttended(int attended, int total) =>
        total == 0 ? 0 : Math.Round(100m * attended / total, 1);
}
