using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Border.Tests;

public sealed class PersistenceModelTests
{
    private static BorderDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BorderDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test;Username=test;Password=test")
            .Options;
        return new BorderDbContext(options);
    }

    [Fact]
    public void Attendance_HasUniqueLessonAndStudentConstraint()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(Attendance))!;
        var index = entity.GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual([nameof(Attendance.LessonSessionId), nameof(Attendance.StudentId)]));
        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(MembershipPlan), nameof(MembershipPlan.DefaultPrice))]
    [InlineData(typeof(MembershipPriceHistory), nameof(MembershipPriceHistory.Price))]
    [InlineData(typeof(Invoice), nameof(Invoice.Amount))]
    [InlineData(typeof(Payment), nameof(Payment.Amount))]
    public void FinancialAmounts_UseDecimal18_2(Type entityType, string propertyName)
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(entityType)!.FindProperty(propertyName)!;
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(2, property.GetScale());
    }

    [Fact]
    public void HistoricalRelationships_RestrictStudentDeletion()
    {
        using var context = CreateContext();
        var attendance = context.Model.FindEntityType(typeof(Attendance))!;
        var relationship = attendance.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(Student));
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
    }
}
