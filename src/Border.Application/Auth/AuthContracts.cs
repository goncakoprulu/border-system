namespace Border.Application.Auth;

public static class Roles
{
    public const string Management = "Management";
    public const string Instructor = "Instructor";
    public const string Reception = "Reception";
    public const string Admin = "Admin";
    public static readonly string[] All = [Management, Instructor, Reception, Admin];
}

public static class Policies
{
    public const string ManagementOnly = "ManagementOnly";
    public const string InstructorOnly = "InstructorOnly";
    public const string AdminOnly = "AdminOnly";
    public const string StudentsAccess = "StudentsAccess";
    public const string StudentsArchive = "StudentsArchive";
    public const string ClassesAccess = "ClassesAccess";
    public const string ClassesManage = "ClassesManage";
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
public sealed record CurrentUserResponse(string Id, string Email, string DisplayName, IReadOnlyCollection<string> Roles);
