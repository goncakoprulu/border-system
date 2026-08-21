using Border.Domain.Common;

namespace Border.Domain.Entities;

public sealed class MembershipPlan : AuditableEntity
{
    public required string Name { get; set; }
    public MembershipPlanType Type { get; set; }
    public decimal DefaultPrice { get; set; }
    public int? LessonCount { get; set; }
    public int? DurationMonths { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class StudentMembership : AuditableEntity
{
    public Guid StudentId { get; set; }
    public Guid MembershipPlanId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
    public Student Student { get; set; } = null!;
    public MembershipPlan MembershipPlan { get; set; } = null!;
}

public sealed class MembershipPriceHistory : Entity
{
    public Guid StudentMembershipId { get; set; }
    public decimal Price { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public required string ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public StudentMembership StudentMembership { get; set; } = null!;
}

public sealed class Invoice : AuditableEntity
{
    public Guid StudentId { get; set; }
    public Guid? StudentMembershipId { get; set; }
    public required string Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public Student Student { get; set; } = null!;
    public StudentMembership? StudentMembership { get; set; }
}

public sealed class Payment : Entity
{
    public Guid StudentId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public required string ReceivedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Student Student { get; set; } = null!;
    public Invoice? Invoice { get; set; }
}

public sealed class AuditLog : Entity
{
    public string? UserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
