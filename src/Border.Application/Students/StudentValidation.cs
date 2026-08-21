using System.Net.Mail;

namespace Border.Application.Students;

public static class StudentValidation
{
    public static Dictionary<string, string[]> Validate(StudentUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, nameof(request.FirstName), request.FirstName, 100, "Ad");
        Required(errors, nameof(request.LastName), request.LastName, 100, "Soyad");
        Optional(errors, nameof(request.Phone), request.Phone, 30, "Telefon");
        Email(errors, nameof(request.Email), request.Email);
        Optional(errors, nameof(request.Gender), request.Gender, 30, "Cinsiyet");
        Optional(errors, nameof(request.Notes), request.Notes, 2000, "Notlar");
        if (request.RegistrationDate == default) errors[nameof(request.RegistrationDate)] = ["Kayıt tarihi zorunludur."];
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        if (request.RegistrationDate > today) errors[nameof(request.RegistrationDate)] = ["Kayıt tarihi gelecekte olamaz."];
        if (request.BirthDate > today) errors[nameof(request.BirthDate)] = ["Doğum tarihi gelecekte olamaz."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(GuardianUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        Required(errors, nameof(request.FirstName), request.FirstName, 100, "Ad");
        Required(errors, nameof(request.LastName), request.LastName, 100, "Soyad");
        Required(errors, nameof(request.Relationship), request.Relationship, 80, "Yakınlık");
        Optional(errors, nameof(request.Phone), request.Phone, 30, "Telefon");
        Email(errors, nameof(request.Email), request.Email);
        return errors;
    }

    private static void Required(Dictionary<string, string[]> errors, string key, string? value, int max, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) errors[key] = [$"{label} zorunludur."];
        else if (value.Trim().Length > max) errors[key] = [$"{label} en fazla {max} karakter olabilir."];
    }

    private static void Optional(Dictionary<string, string[]> errors, string key, string? value, int max, string label)
    {
        if (value?.Trim().Length > max) errors[key] = [$"{label} en fazla {max} karakter olabilir."];
    }

    private static void Email(Dictionary<string, string[]> errors, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Trim().Length > 256 || !MailAddress.TryCreate(value.Trim(), out _)) errors[key] = ["Geçerli bir e-posta adresi girin."];
    }
}
