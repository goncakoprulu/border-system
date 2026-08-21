using Border.Domain.Common;

namespace Border.Domain.Entities;

public enum StudentStatus { Lead, Trial, Active, Frozen, Passive, Left }
public enum StudioClassStatus { Planned, Active, Paused, Completed, Cancelled }
public enum EnrollmentStatus { Active, Frozen, Completed, Cancelled }
public enum LessonSessionStatus { Scheduled, Completed, Cancelled }
public enum AttendanceStatus { Present, Absent, Excused, Late, MakeUp }
public enum MembershipPlanType { Monthly, LessonPackage, PrivateLessonPackage, Other }
public enum MembershipStatus { Active, Frozen, Expired, Cancelled }
public enum InvoiceStatus { Pending, PartiallyPaid, Paid, Cancelled }
public enum PaymentMethod { Cash, CreditCard, BankTransfer, Other }

public sealed class Student : SoftDeletableEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? Notes { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Lead;
    public DateOnly RegistrationDate { get; set; }
    public ICollection<Guardian> Guardians { get; set; } = [];
}

public sealed class Guardian : Entity
{
    public Guid StudentId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public required string Relationship { get; set; }
    public Student Student { get; set; } = null!;
}

public sealed class Instructor : SoftDeletableEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? UserId { get; set; }
}

public sealed class StudioRoom : SoftDeletableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StudioClass : SoftDeletableEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public Guid InstructorId { get; set; }
    public Guid StudioRoomId { get; set; }
    public int Capacity { get; set; }
    public string? Level { get; set; }
    public string? AgeGroup { get; set; }
    public StudioClassStatus Status { get; set; } = StudioClassStatus.Planned;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public Instructor Instructor { get; set; } = null!;
    public StudioRoom StudioRoom { get; set; } = null!;
}

public sealed class ClassSchedule : Entity
{
    public Guid StudioClassId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public StudioClass StudioClass { get; set; } = null!;
}

public sealed class ClassEnrollment : Entity
{
    public Guid StudentId { get; set; }
    public Guid StudioClassId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public Student Student { get; set; } = null!;
    public StudioClass StudioClass { get; set; } = null!;
}

public sealed class LessonSession : AuditableEntity
{
    public Guid StudioClassId { get; set; }
    public Guid InstructorId { get; set; }
    public Guid StudioRoomId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public LessonSessionStatus Status { get; set; } = LessonSessionStatus.Scheduled;
    public string? Notes { get; set; }
    public StudioClass StudioClass { get; set; } = null!;
    public Instructor Instructor { get; set; } = null!;
    public StudioRoom StudioRoom { get; set; } = null!;
}

public sealed class Attendance : Entity
{
    public Guid LessonSessionId { get; set; }
    public Guid StudentId { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Notes { get; set; }
    public required string RecordedByUserId { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public LessonSession LessonSession { get; set; } = null!;
    public Student Student { get; set; } = null!;
}
