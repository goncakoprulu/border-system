namespace Border.Application.Classes;

public static class ClassValidation
{
    public static Dictionary<string, string[]> Validate(StudioClassUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors[nameof(request.Name)] = ["Sınıf adı zorunludur."];
        else if (request.Name.Trim().Length > 160) errors[nameof(request.Name)] = ["Sınıf adı en fazla 160 karakter olabilir."];
        if (request.InstructorId == Guid.Empty) errors[nameof(request.InstructorId)] = ["Eğitmen seçimi zorunludur."];
        if (request.StudioRoomId == Guid.Empty) errors[nameof(request.StudioRoomId)] = ["Stüdyo seçimi zorunludur."];
        if (request.Capacity <= 0 || request.Capacity > 500) errors[nameof(request.Capacity)] = ["Kapasite 1 ile 500 arasında olmalıdır."];
        if (request.StartDate == default) errors[nameof(request.StartDate)] = ["Başlangıç tarihi zorunludur."];
        if (request.EndDate.HasValue && request.EndDate < request.StartDate) errors[nameof(request.EndDate)] = ["Bitiş tarihi başlangıç tarihinden önce olamaz."];
        if (request.Description?.Trim().Length > 2000) errors[nameof(request.Description)] = ["Açıklama en fazla 2000 karakter olabilir."];
        if (request.Level?.Trim().Length > 80) errors[nameof(request.Level)] = ["Seviye en fazla 80 karakter olabilir."];
        if (request.AgeGroup?.Trim().Length > 80) errors[nameof(request.AgeGroup)] = ["Yaş grubu en fazla 80 karakter olabilir."];
        ValidateSchedules(request.Schedules, errors);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(ClassScheduleRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.EndTime <= request.StartTime) errors[nameof(request.EndTime)] = ["Bitiş saati başlangıç saatinden sonra olmalıdır."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(StudioRoomUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors[nameof(request.Name)] = ["Stüdyo adı zorunludur."];
        else if (request.Name.Trim().Length > 120) errors[nameof(request.Name)] = ["Stüdyo adı en fazla 120 karakter olabilir."];
        if (request.Description?.Trim().Length > 500) errors[nameof(request.Description)] = ["Açıklama en fazla 500 karakter olabilir."];
        if (request.Capacity is <= 0 or > 1000) errors[nameof(request.Capacity)] = ["Kapasite 1 ile 1000 arasında olmalıdır."];
        return errors;
    }

    public static Dictionary<string, string[]> Validate(InstructorUpsertRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FirstName)) errors[nameof(request.FirstName)] = ["Ad zorunludur."];
        else if (request.FirstName.Trim().Length > 100) errors[nameof(request.FirstName)] = ["Ad en fazla 100 karakter olabilir."];
        if (string.IsNullOrWhiteSpace(request.LastName)) errors[nameof(request.LastName)] = ["Soyad zorunludur."];
        else if (request.LastName.Trim().Length > 100) errors[nameof(request.LastName)] = ["Soyad en fazla 100 karakter olabilir."];
        if (request.Phone?.Trim().Length > 30) errors[nameof(request.Phone)] = ["Telefon en fazla 30 karakter olabilir."];
        if (request.Email?.Trim().Length > 256 || request.Email is not null && !request.Email.Contains('@')) errors[nameof(request.Email)] = ["Geçerli bir e-posta adresi girin."];
        return errors;
    }

    private static void ValidateSchedules(IReadOnlyCollection<ClassScheduleRequest> schedules, Dictionary<string, string[]> errors)
    {
        if (schedules.Count > 14) errors[nameof(StudioClassUpsertRequest.Schedules)] = ["Bir sınıf için en fazla 14 haftalık program satırı eklenebilir."];
        if (schedules.Any(x => !Enum.IsDefined(x.DayOfWeek))) errors[nameof(StudioClassUpsertRequest.Schedules)] = ["Tüm program satırlarında geçerli bir gün seçilmelidir."];
        if (schedules.Any(x => x.EndTime <= x.StartTime)) errors[nameof(StudioClassUpsertRequest.Schedules)] = ["Tüm program satırlarında bitiş saati başlangıçtan sonra olmalıdır."];
        if (schedules.GroupBy(x => new { x.DayOfWeek, x.StartTime, x.EndTime }).Any(x => x.Count() > 1)) errors[nameof(StudioClassUpsertRequest.Schedules)] = ["Aynı program satırı birden fazla kez eklenemez."];
    }
}
