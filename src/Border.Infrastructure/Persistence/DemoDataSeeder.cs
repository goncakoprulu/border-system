using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Border.Application.Auth;
using Border.Domain.Entities;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Border.Infrastructure.Persistence;

public sealed class DemoDataSeeder(
    BorderDbContext db,
    UserManager<AppUser> userManager,
    IConfiguration configuration,
    ILogger<DemoDataSeeder> logger)
{
    private static readonly DateOnly Today = new(2026, 8, 8);

    public async Task<DemoSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        var password = configuration["DEMO_USER_PASSWORD"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("SEED_DEMO_DATA=true iken DEMO_USER_PASSWORD tanımlanmalıdır.");

        var result = new DemoSeedResult();
        var users = await SeedUsersAsync(password, result, cancellationToken);
        var instructors = await SeedInstructorsAsync(users, result, cancellationToken);
        var rooms = await SeedRoomsAsync(result, cancellationToken);
        var students = await SeedStudentsAsync(result, cancellationToken);
        await SeedGuardiansAsync(students, result, cancellationToken);
        var classes = await SeedClassesAsync(instructors, rooms, result, cancellationToken);
        await SeedEnrollmentsAsync(students, classes, result, cancellationToken);
        var memberships = await SeedFinanceAsync(students, users["volkan"].Id, result, cancellationToken);
        await SeedInvoicesAndPaymentsAsync(students, memberships, users["reception"].Id, result, cancellationToken);
        await SeedSessionsAndAttendanceAsync(classes, students, users["volkan"].Id, result, cancellationToken);
        await SeedAuditAsync(users["volkan"].Id, classes, students, result, cancellationToken);

        logger.LogInformation("BORDER demo verisi hazırlandı. Oluşturulan kayıtlar: {Counts}", JsonSerializer.Serialize(result));
        return result;
    }

    private async Task<Dictionary<string, AppUser>> SeedUsersAsync(string password, DemoSeedResult result, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new UserSeed("volkan", "volkan.hoca@border.demo", "Volkan Hoca", new[] { Roles.Management, Roles.Instructor }),
            new UserSeed("reception", "resepsiyon@border.demo", "Elif Yıldız", new[] { Roles.Reception }),
            new UserSeed("gonca", "gonca@border.demo", "Gonca Arslan", new[] { Roles.Instructor }),
            new UserSeed("ece", "ece@border.demo", "Ece Karaca", new[] { Roles.Instructor }),
            new UserSeed("mert", "mert@border.demo", "Mert Kaya", new[] { Roles.Instructor }),
            new UserSeed("deniz", "deniz@border.demo", "Deniz Aydın", new[] { Roles.Instructor }),
            new UserSeed("selin", "selin@border.demo", "Selin Öztürk", new[] { Roles.Instructor }),
            new UserSeed("can", "can@border.demo", "Can Demir", new[] { Roles.Instructor }),
        };
        var users = new Dictionary<string, AppUser>();
        foreach (var definition in definitions)
        {
            var user = await userManager.FindByEmailAsync(definition.Email);
            if (user is null)
            {
                user = new AppUser { Id = Id($"user:{definition.Key}").ToString(), UserName = definition.Email, Email = definition.Email, EmailConfirmed = true, DisplayName = definition.DisplayName, IsActive = true };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded) throw new InvalidOperationException($"Demo kullanıcı oluşturulamadı ({definition.Email}): {string.Join(", ", created.Errors.Select(x => x.Description))}");
                result.Users++;
            }
            foreach (var role in definition.Roles)
                if (!await userManager.IsInRoleAsync(user, role)) await userManager.AddToRoleAsync(user, role);
            users[definition.Key] = user;
        }
        return users;
    }

    private async Task<Dictionary<string, Instructor>> SeedInstructorsAsync(Dictionary<string, AppUser> users, DemoSeedResult result, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new InstructorSeed("volkan", "Volkan", "Yılmaz", "0532 410 10 10"), new InstructorSeed("gonca", "Gonca", "Arslan", "0532 410 10 11"),
            new InstructorSeed("ece", "Ece", "Karaca", "0532 410 10 12"), new InstructorSeed("mert", "Mert", "Kaya", "0532 410 10 13"),
            new InstructorSeed("deniz", "Deniz", "Aydın", "0532 410 10 14"), new InstructorSeed("selin", "Selin", "Öztürk", "0532 410 10 15"),
            new InstructorSeed("can", "Can", "Demir", "0532 410 10 16"),
        };
        var items = new Dictionary<string, Instructor>();
        foreach (var definition in definitions)
        {
            var id = Id($"instructor:{definition.Key}");
            var item = await db.Instructors.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null)
            {
                item = new Instructor { Id = id, FirstName = definition.FirstName, LastName = definition.LastName, Phone = definition.Phone, Email = users[definition.Key].Email, UserId = users[definition.Key].Id };
                db.Instructors.Add(item); result.Instructors++;
            }
            items[definition.Key] = item;
        }
        await db.SaveChangesAsync(cancellationToken);
        return items;
    }

    private async Task<Dictionary<string, StudioRoom>> SeedRoomsAsync(DemoSeedResult result, CancellationToken cancellationToken)
    {
        var definitions = new[] { ("ana", "Ana Salon", 30, "Geniş grup dersleri ve gösteri provaları"), ("a", "Studio A", 22, "Aynalı dans stüdyosu"), ("b", "Studio B", 18, "Çocuk ve butik grup dersleri"), ("workshop", "Workshop Studio", 16, "Workshop ve freestyle çalışmaları") };
        var items = new Dictionary<string, StudioRoom>();
        foreach (var (key, name, capacity, description) in definitions)
        {
            var id = Id($"room:{key}"); var item = await db.StudioRooms.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null) { item = new StudioRoom { Id = id, Name = name, Capacity = capacity, Description = description, IsActive = true }; db.StudioRooms.Add(item); result.StudioRooms++; }
            items[key] = item;
        }
        await db.SaveChangesAsync(cancellationToken); return items;
    }

    private async Task<List<Student>> SeedStudentsAsync(DemoSeedResult result, CancellationToken cancellationToken)
    {
        string[] firstNames = ["Ayşe", "Zeynep", "Elif", "Duru", "Defne", "İrem", "Ceren", "Eylül", "Melis", "Naz", "Selin", "Yağmur", "Buse", "Mina", "Ada", "Lina", "Arda", "Emir", "Mert", "Efe", "Kerem", "Can", "Deniz", "Bora", "Doruk", "Kaan", "Alp", "Umut", "Ozan", "Baran"];
        string[] lastNames = ["Yılmaz", "Kaya", "Demir", "Şahin", "Çelik", "Aydın", "Arslan", "Koç", "Kurt", "Öztürk", "Aksoy", "Güneş"];
        var items = new List<Student>();
        for (var index = 0; index < 60; index++)
        {
            var id = Id($"student:{index:00}"); var item = await db.Students.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null)
            {
                var child = index < 16; var firstName = firstNames[index % firstNames.Length]; var lastName = lastNames[(index * 5) % lastNames.Length];
                var status = index switch { < 38 => StudentStatus.Active, < 44 => StudentStatus.Trial, < 49 => StudentStatus.Lead, < 53 => StudentStatus.Frozen, < 57 => StudentStatus.Passive, _ => StudentStatus.Left };
                item = new Student { Id = id, FirstName = firstName, LastName = lastName, Phone = $"05{30 + index % 10} {200 + index:000} {10 + index % 80:00} {20 + index % 70:00}", Email = $"{Ascii(firstName)}.{Ascii(lastName)}{index + 1}@example.local", BirthDate = child ? new DateOnly(2011 + index % 6, 1 + index % 12, 2 + index % 25) : new DateOnly(1988 + index % 18, 1 + index % 12, 2 + index % 25), Gender = index % 2 == 0 ? "Kadın" : "Erkek", Notes = index % 9 == 0 ? "Ders saatleri için önceden bilgilendirilmeli." : index % 11 == 0 ? "Uzun süreli BORDER öğrencisi." : null, Status = status, RegistrationDate = Today.AddDays(-(30 + index * 11)) };
                db.Students.Add(item); result.Students++;
            }
            items.Add(item);
        }
        await db.SaveChangesAsync(cancellationToken); return items;
    }

    private async Task SeedGuardiansAsync(List<Student> students, DemoSeedResult result, CancellationToken cancellationToken)
    {
        string[] names = ["Aylin", "Burcu", "Gül", "Seda", "Pınar", "Nihan", "Serkan", "Hakan", "Onur", "Tolga", "Murat", "Levent"];
        for (var index = 0; index < 14; index++)
        {
            var id = Id($"guardian:{index:00}"); if (await db.Guardians.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            db.Guardians.Add(new Guardian { Id = id, StudentId = students[index].Id, FirstName = names[index % names.Length], LastName = students[index].LastName, Relationship = index % 3 == 0 ? "Baba" : "Anne", Phone = $"0533 700 {index:00} {30 + index:00}", Email = $"veli{index + 1}@example.local" }); result.Guardians++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<ClassSeeded>> SeedClassesAsync(Dictionary<string, Instructor> instructors, Dictionary<string, StudioRoom> rooms, DemoSeedResult result, CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            C("hiphop-beginner", "Hip Hop Beginner", "volkan", "ana", 18, "Başlangıç", "Genç Yetişkin", (DayOfWeek.Tuesday,19,0,20,15),(DayOfWeek.Thursday,19,0,20,15)),
            C("hiphop-intermediate", "Hip Hop Intermediate", "volkan", "ana", 18, "Orta", "Genç Yetişkin", (DayOfWeek.Tuesday,20,30,21,45),(DayOfWeek.Thursday,20,30,21,45)),
            C("hiphop-advanced", "Hip Hop Advanced", "gonca", "a", 16, "İleri", "Genç Yetişkin", (DayOfWeek.Monday,20,0,21,15),(DayOfWeek.Wednesday,20,0,21,15)),
            C("hiphop-kids", "Hip Hop Kids", "ece", "b", 14, "Başlangıç", "Çocuk", (DayOfWeek.Saturday,11,0,12,0)),
            C("kpop-beginner", "K-Pop Beginner", "deniz", "a", 20, "Başlangıç", "Genç", (DayOfWeek.Tuesday,18,0,19,0),(DayOfWeek.Thursday,18,0,19,0)),
            C("commercial", "Commercial Choreography", "gonca", "ana", 22, "Advanced", "Genç Yetişkin", (DayOfWeek.Monday,18,30,19,45),(DayOfWeek.Wednesday,18,30,19,45)),
            C("heels", "Heels", "selin", "b", 16, "Orta", "Yetişkin", (DayOfWeek.Tuesday,20,0,21,15),(DayOfWeek.Thursday,20,0,21,15)),
            C("breaking", "Breaking Beginner", "mert", "workshop", 14, "Başlangıç", "Genç", (DayOfWeek.Monday,19,0,20,15),(DayOfWeek.Wednesday,19,0,20,15)),
            C("popping", "Popping", "can", "workshop", 14, "Temel", "Genç", (DayOfWeek.Tuesday,20,0,21,15),(DayOfWeek.Thursday,20,0,21,15)),
            C("house", "House", "deniz", "a", 16, "Orta", "Genç Yetişkin", (DayOfWeek.Friday,20,0,21,15)),
            C("contemporary", "Contemporary", "ece", "b", 15, "Temel", "Genç Yetişkin", (DayOfWeek.Monday,18,30,19,45),(DayOfWeek.Wednesday,18,30,19,45)),
            C("workshop", "Workshop / Choreo Class", "volkan", "workshop", 16, "Advanced", "Genç Yetişkin", (DayOfWeek.Saturday,14,0,16,0)),
        };
        var items = new List<ClassSeeded>();
        foreach (var definition in definitions)
        {
            var id = Id($"class:{definition.Key}"); var item = await db.StudioClasses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null) { item = new StudioClass { Id = id, Name = definition.Name, Description = $"BORDER {definition.Name} düzenli grup dersi.", InstructorId = instructors[definition.Instructor].Id, StudioRoomId = rooms[definition.Room].Id, Capacity = definition.Capacity, Level = definition.Level, AgeGroup = definition.AgeGroup, Status = StudioClassStatus.Active, StartDate = new(2026, 1, 5) }; db.StudioClasses.Add(item); result.Classes++; }
            items.Add(new(item, definition.Schedules));
        }
        await db.SaveChangesAsync(cancellationToken);
        foreach (var seeded in items)
        foreach (var schedule in seeded.Schedules)
        {
            var id = Id($"schedule:{seeded.Class.Id}:{schedule.Day}:{schedule.StartHour}:{schedule.StartMinute}");
            if (await db.ClassSchedules.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            db.ClassSchedules.Add(new ClassSchedule { Id = id, StudioClassId = seeded.Class.Id, DayOfWeek = schedule.Day, StartTime = new(schedule.StartHour, schedule.StartMinute), EndTime = new(schedule.EndHour, schedule.EndMinute) }); result.ClassSchedules++;
        }
        await db.SaveChangesAsync(cancellationToken); return items;
    }

    private async Task SeedEnrollmentsAsync(List<Student> students, List<ClassSeeded> classes, DemoSeedResult result, CancellationToken cancellationToken)
    {
        int[] targets = [17, 14, 10, 12, 19, 13, 9, 7, 8, 6, 11, 5];
        for (var classIndex = 0; classIndex < classes.Count; classIndex++)
        for (var position = 0; position < targets[classIndex]; position++)
        {
            var student = students[(position * 7 + classIndex * 11) % 46]; var id = Id($"enrollment:{classes[classIndex].Class.Id}:{student.Id}:active");
            if (await db.ClassEnrollments.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            db.ClassEnrollments.Add(new ClassEnrollment { Id = id, StudentId = student.Id, StudioClassId = classes[classIndex].Class.Id, StartDate = new(2026, 2 + classIndex % 4, 1 + position % 20), Status = EnrollmentStatus.Active }); result.ClassEnrollments++;
        }
        for (var index = 0; index < 12; index++)
        {
            var student = students[48 + index % 12]; var studioClass = classes[index % classes.Count].Class; var id = Id($"enrollment:{studioClass.Id}:{student.Id}:history");
            if (await db.ClassEnrollments.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            db.ClassEnrollments.Add(new ClassEnrollment { Id = id, StudentId = student.Id, StudioClassId = studioClass.Id, StartDate = new(2025, 9, 1 + index % 20), EndDate = new(2026, 3, 1 + index % 20), Status = EnrollmentStatus.Completed }); result.ClassEnrollments++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<StudentMembership>> SeedFinanceAsync(List<Student> students, string approverId, DemoSeedResult result, CancellationToken cancellationToken)
    {
        var plans = new[] { new PlanSeed("single", "Aylık Tek Branş", MembershipPlanType.Monthly, 2500, null, 1), new PlanSeed("double", "Aylık İki Branş", MembershipPlanType.Monthly, 4000, null, 1), new PlanSeed("eight", "8 Ders Paketi", MembershipPlanType.LessonPackage, 2800, 8, null), new PlanSeed("twelve", "12 Ders Paketi", MembershipPlanType.LessonPackage, 3600, 12, null), new PlanSeed("private", "Özel Ders Paketi", MembershipPlanType.PrivateLessonPackage, 6000, 4, 1), new PlanSeed("dropin", "Drop-in / Tek Ders", MembershipPlanType.Other, 500, 1, null) };
        var planEntities = new List<MembershipPlan>();
        foreach (var definition in plans)
        {
            var id = Id($"plan:{definition.Key}"); var entity = await db.MembershipPlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity is null) { entity = new MembershipPlan { Id = id, Name = definition.Name, Type = definition.Type, DefaultPrice = definition.Price, LessonCount = definition.Lessons, DurationMonths = definition.Months, IsActive = true }; db.MembershipPlans.Add(entity); result.MembershipPlans++; }
            planEntities.Add(entity);
        }
        await db.SaveChangesAsync(cancellationToken);
        decimal[] prices = [2500, 2300, 2200, 2000, 1800, 1500]; string[] reasons = ["", "Volkan Hoca özel indirimi", "Kardeş indirimi", "Uzun süreli öğrenci", "İki branş indirimi", "Öğrenci indirimi"];
        var memberships = new List<StudentMembership>();
        for (var index = 0; index < 48; index++)
        {
            var id = Id($"membership:{index:00}"); var membership = await db.StudentMemberships.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (membership is null) { membership = new StudentMembership { Id = id, StudentId = students[index].Id, MembershipPlanId = planEntities[index % planEntities.Count].Id, StartDate = new(2025 + index % 2, 1 + index % 7, 1), Status = index is 38 or 39 ? MembershipStatus.Frozen : MembershipStatus.Active }; db.StudentMemberships.Add(membership); result.StudentMemberships++; }
            memberships.Add(membership);
        }
        await db.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < memberships.Count; index++)
        {
            var plan = planEntities[index % planEntities.Count]; var currentPrice = plan.Type == MembershipPlanType.Monthly ? prices[index % prices.Length] : plan.DefaultPrice;
            if (index < 10)
            {
                var oldId = Id($"price:{index:00}:old"); if (!await db.MembershipPriceHistory.AnyAsync(x => x.Id == oldId, cancellationToken)) { db.MembershipPriceHistory.Add(new MembershipPriceHistory { Id = oldId, StudentMembershipId = memberships[index].Id, Price = 1800, DiscountAmount = Math.Max(0, plan.DefaultPrice - 1800), DiscountReason = reasons[1 + index % (reasons.Length - 1)], EffectiveFrom = new(2025, 9, 1), EffectiveTo = new(2025, 11, 30), ApprovedByUserId = approverId }); result.MembershipPriceHistory++; }
                currentPrice = 2100;
            }
            var id = Id($"price:{index:00}:current"); if (await db.MembershipPriceHistory.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            db.MembershipPriceHistory.Add(new MembershipPriceHistory { Id = id, StudentMembershipId = memberships[index].Id, Price = currentPrice, DiscountAmount = Math.Max(0, plan.DefaultPrice - currentPrice), DiscountReason = currentPrice < plan.DefaultPrice ? reasons[1 + index % (reasons.Length - 1)] : null, EffectiveFrom = index < 10 ? new(2025, 12, 1) : memberships[index].StartDate, ApprovedByUserId = approverId }); result.MembershipPriceHistory++;
        }
        await db.SaveChangesAsync(cancellationToken); return memberships;
    }

    private async Task SeedInvoicesAndPaymentsAsync(List<Student> students, List<StudentMembership> memberships, string receiverId, DemoSeedResult result, CancellationToken cancellationToken)
    {
        for (var studentIndex = 0; studentIndex < 36; studentIndex++)
        for (var monthOffset = 0; monthOffset < 3; monthOffset++)
        {
            var id = Id($"invoice:{studentIndex:00}:{monthOffset}"); var invoice = await db.Invoices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
            var amount = new decimal[] { 2500, 2300, 2200, 2000, 1800, 1500 }[(studentIndex + monthOffset) % 6]; var statusPattern = (studentIndex + monthOffset) % 5; var status = statusPattern <= 2 ? InvoiceStatus.Paid : statusPattern == 3 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending;
            if (invoice is null) { invoice = new Invoice { Id = id, StudentId = students[studentIndex].Id, StudentMembershipId = memberships[studentIndex].Id, Description = $"{new DateOnly(2026, 5 + monthOffset, 1):MMMM yyyy} üyelik bedeli", Amount = amount, DueDate = new(2026, 5 + monthOffset, 5), Status = status }; db.Invoices.Add(invoice); result.Invoices++; await db.SaveChangesAsync(cancellationToken); }
            if (status == InvoiceStatus.Pending) continue;
            var paymentId = Id($"payment:{studentIndex:00}:{monthOffset}"); if (await db.Payments.AnyAsync(x => x.Id == paymentId, cancellationToken)) continue;
            db.Payments.Add(new Payment { Id = paymentId, StudentId = students[studentIndex].Id, InvoiceId = invoice.Id, Amount = status == InvoiceStatus.Paid ? amount : decimal.Round(amount / 2, 2), PaymentMethod = (PaymentMethod)((studentIndex + monthOffset) % 3), PaymentDate = new DateTime(2026, 5 + monthOffset, 4 + studentIndex % 20, 10 + studentIndex % 8, 0, 0, DateTimeKind.Utc), Notes = status == InvoiceStatus.PartiallyPaid ? "Kalan tutar sonraki ödeme tarihinde alınacak." : null, ReceivedByUserId = receiverId }); result.Payments++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSessionsAndAttendanceAsync(List<ClassSeeded> classes, List<Student> students, string recorderId, DemoSeedResult result, CancellationToken cancellationToken)
    {
        var enrollments = await db.ClassEnrollments.AsNoTracking().Where(x => x.Status == EnrollmentStatus.Active).ToListAsync(cancellationToken);
        foreach (var seeded in classes)
        foreach (var schedule in seeded.Schedules)
        for (var week = 1; week <= 4; week++)
        {
            var sessionDate = Previous(schedule.Day, Today.AddDays(-7 * week)); var localStart = sessionDate.ToDateTime(new(schedule.StartHour, schedule.StartMinute)); var localEnd = sessionDate.ToDateTime(new(schedule.EndHour, schedule.EndMinute)); var id = Id($"session:{seeded.Class.Id}:{sessionDate:yyyy-MM-dd}:{schedule.StartHour}:{schedule.StartMinute}");
            if (!await db.LessonSessions.AnyAsync(x => x.Id == id, cancellationToken)) { db.LessonSessions.Add(new LessonSession { Id = id, StudioClassId = seeded.Class.Id, InstructorId = seeded.Class.InstructorId, StudioRoomId = seeded.Class.StudioRoomId, ScheduledStart = IstanbulUtc(localStart), ScheduledEnd = IstanbulUtc(localEnd), Status = LessonSessionStatus.Completed, Notes = week == 4 ? "Aylık program dersi" : null }); result.LessonSessions++; await db.SaveChangesAsync(cancellationToken); }
            var eligible = enrollments.Where(x => x.StudioClassId == seeded.Class.Id && x.StartDate <= sessionDate && (x.EndDate == null || x.EndDate >= sessionDate)).ToList();
            foreach (var enrollment in eligible)
            {
                var attendanceId = Id($"attendance:{id}:{enrollment.StudentId}"); if (await db.Attendances.AnyAsync(x => x.Id == attendanceId, cancellationToken)) continue;
                var number = Math.Abs(HashCode.Combine(id, enrollment.StudentId)) % 20; var status = number switch { 0 or 1 => AttendanceStatus.Absent, 2 => AttendanceStatus.Excused, 3 => AttendanceStatus.Late, 4 => AttendanceStatus.MakeUp, _ => AttendanceStatus.Present };
                db.Attendances.Add(new Attendance { Id = attendanceId, LessonSessionId = id, StudentId = enrollment.StudentId, Status = status, Notes = status == AttendanceStatus.Excused ? "Önceden bilgi verildi." : status == AttendanceStatus.Late ? "Derse 10 dakika geç katıldı." : null, RecordedByUserId = recorderId, RecordedAt = IstanbulUtc(localEnd.AddMinutes(5)), UpdatedAt = IstanbulUtc(localEnd.AddMinutes(5)) }); result.Attendances++;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedAuditAsync(string userId, List<ClassSeeded> classes, List<Student> students, DemoSeedResult result, CancellationToken cancellationToken)
    {
        for (var index = 0; index < 24; index++)
        {
            var id = Id($"audit:{index:00}"); if (await db.AuditLogs.AnyAsync(x => x.Id == id, cancellationToken)) continue;
            var isClass = index % 2 == 0; var targetId = isClass ? classes[index % classes.Count].Class.Id : students[index % students.Count].Id;
            db.AuditLogs.Add(new AuditLog { Id = id, UserId = userId, Action = isClass ? "DemoClassReviewed" : "DemoStudentReviewed", EntityType = isClass ? nameof(StudioClass) : nameof(Student), EntityId = targetId.ToString(), NewValues = JsonSerializer.Serialize(new { Source = "DevelopmentDemoSeeder", Note = "Geliştirme görünüm verisi" }), Timestamp = DateTime.UtcNow.AddDays(-(index + 1)), IpAddress = "127.0.0.1" }); result.AuditLogs++;
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ClassDefinition C(string key, string name, string instructor, string room, int capacity, string level, string ageGroup, params (DayOfWeek Day, int StartHour, int StartMinute, int EndHour, int EndMinute)[] schedules) => new(key, name, instructor, room, capacity, level, ageGroup, schedules.Select(x => new ScheduleSeed(x.Day, x.StartHour, x.StartMinute, x.EndHour, x.EndMinute)).ToArray());
    private static DateOnly Previous(DayOfWeek day, DateOnly from) { while (from.DayOfWeek != day) from = from.AddDays(-1); return from; }
    private static DateTime IstanbulUtc(DateTime local) => new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(3)).UtcDateTime;
    private static Guid Id(string key) { var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"border-development-demo:{key}")); return new Guid(bytes[..16]); }
    private static string Ascii(string value) => value.ToLowerInvariant().Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i").Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");

    private sealed record UserSeed(string Key, string Email, string DisplayName, string[] Roles);
    private sealed record InstructorSeed(string Key, string FirstName, string LastName, string Phone);
    private sealed record PlanSeed(string Key, string Name, MembershipPlanType Type, decimal Price, int? Lessons, int? Months);
    private sealed record ScheduleSeed(DayOfWeek Day, int StartHour, int StartMinute, int EndHour, int EndMinute);
    private sealed record ClassDefinition(string Key, string Name, string Instructor, string Room, int Capacity, string Level, string AgeGroup, ScheduleSeed[] Schedules);
    private sealed record ClassSeeded(StudioClass Class, ScheduleSeed[] Schedules);
}

public sealed class DemoSeedResult
{
    public int Users { get; set; }
    public int Instructors { get; set; }
    public int StudioRooms { get; set; }
    public int Students { get; set; }
    public int Guardians { get; set; }
    public int Classes { get; set; }
    public int ClassSchedules { get; set; }
    public int ClassEnrollments { get; set; }
    public int MembershipPlans { get; set; }
    public int StudentMemberships { get; set; }
    public int MembershipPriceHistory { get; set; }
    public int Invoices { get; set; }
    public int Payments { get; set; }
    public int LessonSessions { get; set; }
    public int Attendances { get; set; }
    public int AuditLogs { get; set; }
}
