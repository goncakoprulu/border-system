using Border.Domain.Common;
using Border.Domain.Entities;
using Border.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Border.Infrastructure.Persistence;

public sealed class BorderDbContext(DbContextOptions<BorderDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Guardian> Guardians => Set<Guardian>();
    public DbSet<Instructor> Instructors => Set<Instructor>();
    public DbSet<StudioRoom> StudioRooms => Set<StudioRoom>();
    public DbSet<StudioClass> StudioClasses => Set<StudioClass>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassEnrollment> ClassEnrollments => Set<ClassEnrollment>();
    public DbSet<LessonSession> LessonSessions => Set<LessonSession>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<StudentMembership> StudentMemberships => Set<StudentMembership>();
    public DbSet<MembershipPriceHistory> MembershipPriceHistory => Set<MembershipPriceHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        ConfigureStudents(builder);
        ConfigureClasses(builder);
        ConfigureAttendance(builder);
        ConfigureFinance(builder);
        ConfigureAudit(builder);
    }

    public override int SaveChanges()
    {
        SetAuditDates();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditDates()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
    }

    private static void ConfigureStudents(ModelBuilder builder)
    {
        builder.Entity<Student>(e =>
        {
            e.ToTable("Students");
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Gender).HasMaxLength(30);
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Phone);
        });
        builder.Entity<Guardian>(e =>
        {
            e.ToTable("Guardians");
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Relationship).HasMaxLength(80).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasOne(x => x.Student).WithMany(x => x.Guardians).HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Instructor>(e =>
        {
            e.ToTable("Instructors");
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(256);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<AppUser>().WithOne().HasForeignKey<Instructor>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureClasses(ModelBuilder builder)
    {
        builder.Entity<StudioRoom>(e =>
        {
            e.ToTable("StudioRooms");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => new { x.IsActive, x.IsDeleted });
        });
        builder.Entity<StudioClass>(e =>
        {
            e.ToTable("StudioClasses");
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Level).HasMaxLength(80);
            e.Property(x => x.AgeGroup).HasMaxLength(80);
            e.ToTable(t => t.HasCheckConstraint("CK_StudioClasses_Capacity", "\"Capacity\" > 0"));
            e.HasIndex(x => new { x.InstructorId, x.Status });
            e.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.StudioRoom).WithMany().HasForeignKey(x => x.StudioRoomId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ClassSchedule>(e =>
        {
            e.ToTable("ClassSchedules");
            e.HasIndex(x => new { x.StudioClassId, x.DayOfWeek });
            e.HasIndex(x => new { x.StudioClassId, x.DayOfWeek, x.StartTime, x.EndTime }).IsUnique();
            e.HasOne(x => x.StudioClass).WithMany().HasForeignKey(x => x.StudioClassId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_ClassSchedules_TimeRange", "\"EndTime\" > \"StartTime\""));
        });
        builder.Entity<ClassEnrollment>(e =>
        {
            e.ToTable("ClassEnrollments");
            e.HasIndex(x => new { x.StudentId, x.StudioClassId, x.StartDate }).IsUnique();
            e.HasIndex(x => new { x.StudioClassId, x.Status });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.StudioClass).WithMany().HasForeignKey(x => x.StudioClassId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_ClassEnrollments_DateRange", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
        });
    }

    private static void ConfigureAttendance(ModelBuilder builder)
    {
        builder.Entity<LessonSession>(e =>
        {
            e.ToTable("LessonSessions", t => t.HasCheckConstraint("CK_LessonSessions_TimeRange", "\"ScheduledEnd\" > \"ScheduledStart\""));
            e.HasIndex(x => new { x.StudioClassId, x.ScheduledStart }).IsUnique();
            e.HasIndex(x => new { x.InstructorId, x.ScheduledStart });
            e.HasOne(x => x.StudioClass).WithMany().HasForeignKey(x => x.StudioClassId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Instructor).WithMany().HasForeignKey(x => x.InstructorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.StudioRoom).WithMany().HasForeignKey(x => x.StudioRoomId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Attendance>(e =>
        {
            e.ToTable("Attendances");
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.LessonSessionId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.RecordedAt });
            e.HasOne(x => x.LessonSession).WithMany().HasForeignKey(x => x.LessonSessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureFinance(ModelBuilder builder)
    {
        builder.Entity<MembershipPlan>(e =>
        {
            e.ToTable("MembershipPlans", t => t.HasCheckConstraint("CK_MembershipPlans_DefaultPrice", "\"DefaultPrice\" >= 0"));
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.DefaultPrice).HasPrecision(18, 2);
        });
        builder.Entity<StudentMembership>(e =>
        {
            e.ToTable("StudentMemberships", t => t.HasCheckConstraint("CK_StudentMemberships_DateRange", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
            e.HasIndex(x => new { x.StudentId, x.Status });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.MembershipPlan).WithMany().HasForeignKey(x => x.MembershipPlanId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<MembershipPriceHistory>(e =>
        {
            e.ToTable("MembershipPriceHistory", t =>
            {
                t.HasCheckConstraint("CK_MembershipPriceHistory_Price", "\"Price\" >= 0 AND (\"DiscountAmount\" IS NULL OR \"DiscountAmount\" >= 0)");
                t.HasCheckConstraint("CK_MembershipPriceHistory_DateRange", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
            });
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.Property(x => x.DiscountReason).HasMaxLength(500);
            e.HasIndex(x => new { x.StudentMembershipId, x.EffectiveFrom }).IsUnique();
            e.HasOne(x => x.StudentMembership).WithMany().HasForeignKey(x => x.StudentMembershipId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices", t => t.HasCheckConstraint("CK_Invoices_Amount", "\"Amount\" > 0"));
            e.Property(x => x.Description).HasMaxLength(500).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.StudentId, x.Status, x.DueDate });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.StudentMembership).WithMany().HasForeignKey(x => x.StudentMembershipId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Payment>(e =>
        {
            e.ToTable("Payments", t => t.HasCheckConstraint("CK_Payments_Amount", "\"Amount\" > 0"));
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasIndex(x => new { x.StudentId, x.PaymentDate });
            e.HasOne(x => x.Student).WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAudit(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.OldValues).HasColumnType("jsonb");
            e.Property(x => x.NewValues).HasColumnType("jsonb");
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.Timestamp });
            e.HasIndex(x => new { x.UserId, x.Timestamp });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
