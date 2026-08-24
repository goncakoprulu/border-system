namespace Border.Application.Classes;

public static class StudioClassOptions
{
    public static readonly string[] Levels = ["Temel", "Başlangıç", "Orta", "İleri", "Advanced"];
    public static readonly string[] AgeGroups = ["Çocuk", "Genç", "Genç Yetişkin", "Yetişkin"];

    public static bool IsValidLevel(string? value) => IsEmptyOrAllowed(value, Levels);
    public static bool IsValidAgeGroup(string? value) => IsEmptyOrAllowed(value, AgeGroups);

    private static bool IsEmptyOrAllowed(string? value, string[] allowed) =>
        string.IsNullOrWhiteSpace(value) || allowed.Contains(value.Trim(), StringComparer.Ordinal);
}
