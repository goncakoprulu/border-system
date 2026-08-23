using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Border.Application.Operations;
using Border.Domain.Entities;
using Border.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Border.Tests;

public sealed class BalanceApiTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    [Fact]
    public async Task Balances_CalculateInvoiceLedgerWithoutNegativeOrUnallocatedDeductions()
    {
        await factory.ResetAsync();
        var seed = await SeedLedgerAsync();
        using var client = Client();

        var response = await client.GetAsync("/api/balances");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var balances = await response.Content.ReadFromJsonAsync<BalancesResponse>(JsonOptions);

        Assert.Equal(825m, balances!.Summary.OpenBalance);
        Assert.Equal(400m, balances.Summary.OverdueTotal);
        Assert.Equal(4, balances.Summary.DebtorCount);
        Assert.Equal(4, balances.Items.Count);

        AssertBalance(balances, seed.Pending, 100m, 0m, 100m, 0m, 1, 0, DebtStatus.Open);
        AssertBalance(balances, seed.Partial, 200m, 75m, 125m, 0m, 1, 0, DebtStatus.Open);
        AssertBalance(balances, seed.Overdue, 500m, 100m, 400m, 400m, 1, 1, DebtStatus.Overdue);
        AssertBalance(balances, seed.Multiple, 500m, 300m, 200m, 0m, 1, 0, DebtStatus.Open);

        var all = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?includeSettled=true", JsonOptions);
        Assert.Equal(8, all!.Items.Count);
        AssertBalance(all, seed.NoInvoice, 0m, 0m, 0m, 0m, 0, 0, DebtStatus.None);
        Assert.NotNull(all.Items.Single(x => x.StudentId == seed.NoInvoice).LastPaymentDate);
        AssertBalance(all, seed.Paid, 300m, 300m, 0m, 0m, 0, 0, DebtStatus.None);
        AssertBalance(all, seed.Cancelled, 0m, 0m, 0m, 0m, 0, 0, DebtStatus.None);
        AssertBalance(all, seed.Overpaid, 100m, 150m, 0m, 0m, 0, 0, DebtStatus.None);
    }

    [Fact]
    public async Task Balances_ApplySearchAndDebtFiltersWithoutChangingGlobalKpis()
    {
        await factory.ResetAsync();
        var seed = await SeedLedgerAsync();
        using var client = Client();

        var search = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?search=Gecikmiş", JsonOptions);
        Assert.Equal(seed.Overdue, Assert.Single(search!.Items).StudentId);
        Assert.Equal(825m, search.Summary.OpenBalance);

        var overdue = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?overdueOnly=true", JsonOptions);
        Assert.Equal(seed.Overdue, Assert.Single(overdue!.Items).StudentId);

        var settledSearch = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?search=Faturasız&includeSettled=true", JsonOptions);
        Assert.Equal(seed.NoInvoice, Assert.Single(settledSearch!.Items).StudentId);

        var openTakesPrecedence = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?openOnly=true&includeSettled=true", JsonOptions);
        Assert.Equal(4, openTakesPrecedence!.Items.Count);
        Assert.All(openTakesPrecedence.Items, x => Assert.True(x.Remaining > 0));
    }

    [Fact]
    public async Task Balances_EmptyDatabaseReturnsZeroSummaryAndNoRows()
    {
        await factory.ResetAsync();
        using var client = Client();
        var balances = await client.GetFromJsonAsync<BalancesResponse>("/api/balances?includeSettled=true", JsonOptions);
        Assert.NotNull(balances);
        Assert.Equal(0m, balances.Summary.OpenBalance);
        Assert.Equal(0m, balances.Summary.OverdueTotal);
        Assert.Equal(0m, balances.Summary.CollectedThisMonth);
        Assert.Equal(0, balances.Summary.DebtorCount);
        Assert.Empty(balances.Items);
    }

    [Fact]
    public async Task OpenInvoices_AggregateMultiplePaymentsAndClampOverpayment()
    {
        await factory.ResetAsync();
        var seed = await SeedLedgerAsync();
        using var client = Client();

        var partial = await client.GetFromJsonAsync<IReadOnlyCollection<InvoiceOptionResponse>>($"/api/students/{seed.Partial}/open-invoices", JsonOptions);
        var invoice = Assert.Single(partial!);
        Assert.Equal(75m, invoice.Paid);
        Assert.Equal(125m, invoice.Remaining);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);

        var overpaid = await client.GetFromJsonAsync<IReadOnlyCollection<InvoiceOptionResponse>>($"/api/students/{seed.Overpaid}/open-invoices", JsonOptions);
        Assert.Empty(overpaid!);
    }

    [Fact]
    public void InvoicePaymentAggregate_TranslatesWithProductionNpgsqlProvider()
    {
        var options = new DbContextOptionsBuilder<BorderDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test")
            .Options;
        using var db = new BorderDbContext(options);
        var query = db.Invoices.AsNoTracking()
            .Where(invoice => !invoice.Student.IsDeleted && invoice.Status != InvoiceStatus.Cancelled)
            .Select(invoice => new
            {
                invoice.StudentId,
                invoice.Amount,
                Paid = db.Payments.Where(payment => payment.InvoiceId == invoice.Id).Sum(payment => (decimal?)payment.Amount) ?? 0m,
            });

        var sql = query.ToQueryString();
        Assert.Contains("Invoices", sql);
        Assert.Contains("Payments", sql);
        Assert.Contains("COALESCE", sql);
    }

    [Fact]
    public void LegacyResponseProjectionFilter_IsRejectedByProductionNpgsqlProvider()
    {
        var options = new DbContextOptionsBuilder<BorderDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=test")
            .Options;
        using var db = new BorderDbContext(options);
        var legacyShape = db.Students.AsNoTracking()
            .Select(student => new BalanceListItemResponse(
                student.Id,
                student.FirstName + " " + student.LastName,
                db.Invoices.Where(invoice => invoice.StudentId == student.Id && invoice.Status != InvoiceStatus.Cancelled).Sum(invoice => (decimal?)invoice.Amount) ?? 0,
                0,
                (db.Invoices.Where(invoice => invoice.StudentId == student.Id && invoice.Status != InvoiceStatus.Cancelled).Sum(invoice => (decimal?)invoice.Amount) ?? 0)
                    - (db.Payments.Where(payment => payment.StudentId == student.Id && payment.InvoiceId != null).Sum(payment => (decimal?)payment.Amount) ?? 0),
                null,
                0,
                0,
                0,
                DebtStatus.None))
            .Where(item => item.Remaining > 0);

        var error = Assert.Throws<InvalidOperationException>(() => legacyShape.ToQueryString());
        Assert.Contains("could not be translated", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LedgerSeed> SeedLedgerAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BorderDbContext>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
        var paymentDate = DateTime.UtcNow.AddMinutes(-5);

        Student Student(string firstName) => new() { FirstName = firstName, LastName = "Öğrenci", RegistrationDate = today, Status = StudentStatus.Active };
        var noInvoice = Student("Faturasız");
        var pending = Student("Bekleyen");
        var partial = Student("Kısmi");
        var paid = Student("Ödenmiş");
        var cancelled = Student("İptal");
        var overdue = Student("Gecikmiş");
        var overpaid = Student("Fazla");
        var multiple = Student("Çoklu");
        db.Students.AddRange(noInvoice, pending, partial, paid, cancelled, overdue, overpaid, multiple);

        Invoice Invoice(Student student, decimal amount, InvoiceStatus status, DateOnly dueDate, string description) => new() { Student = student, Amount = amount, Status = status, DueDate = dueDate, Description = description };
        var pendingInvoice = Invoice(pending, 100m, InvoiceStatus.Pending, today.AddDays(5), "Bekleyen fatura");
        var partialInvoice = Invoice(partial, 200m, InvoiceStatus.PartiallyPaid, today.AddDays(5), "Kısmi fatura");
        var paidInvoice = Invoice(paid, 300m, InvoiceStatus.Paid, today.AddDays(-5), "Ödenmiş fatura");
        var cancelledInvoice = Invoice(cancelled, 400m, InvoiceStatus.Cancelled, today.AddDays(-5), "İptal fatura");
        var overdueInvoice = Invoice(overdue, 500m, InvoiceStatus.PartiallyPaid, today.AddDays(-1), "Gecikmiş fatura");
        var overpaidInvoice = Invoice(overpaid, 100m, InvoiceStatus.PartiallyPaid, today.AddDays(-1), "Fazla ödenen fatura");
        var multipleOpen = Invoice(multiple, 300m, InvoiceStatus.PartiallyPaid, today.AddDays(5), "Çoklu açık");
        var multiplePaid = Invoice(multiple, 200m, InvoiceStatus.Paid, today.AddDays(-5), "Çoklu kapalı");
        db.Invoices.AddRange(pendingInvoice, partialInvoice, paidInvoice, cancelledInvoice, overdueInvoice, overpaidInvoice, multipleOpen, multiplePaid);

        Payment Payment(Student student, decimal amount, Invoice? invoice = null) => new() { Student = student, Invoice = invoice, Amount = amount, PaymentMethod = PaymentMethod.Cash, PaymentDate = paymentDate, ReceivedByUserId = "test-user" };
        db.Payments.AddRange(
            Payment(noInvoice, 75m),
            Payment(partial, 50m, partialInvoice), Payment(partial, 25m, partialInvoice),
            Payment(paid, 300m, paidInvoice),
            Payment(cancelled, 100m, cancelledInvoice),
            Payment(overdue, 100m, overdueInvoice),
            Payment(overpaid, 150m, overpaidInvoice),
            Payment(multiple, 100m, multipleOpen), Payment(multiple, 200m, multiplePaid));
        await db.SaveChangesAsync();
        return new(noInvoice.Id, pending.Id, partial.Id, paid.Id, cancelled.Id, overdue.Id, overpaid.Id, multiple.Id);
    }

    private static void AssertBalance(BalancesResponse response, Guid studentId, decimal totalDebt, decimal paid, decimal remaining, decimal overdue, int openInvoices, int overdueInvoices, DebtStatus status)
    {
        var item = response.Items.Single(x => x.StudentId == studentId);
        Assert.Equal(totalDebt, item.TotalDebt);
        Assert.Equal(paid, item.Paid);
        Assert.Equal(remaining, item.Remaining);
        Assert.Equal(overdue, item.OverdueBalance);
        Assert.Equal(openInvoices, item.OpenInvoiceCount);
        Assert.Equal(overdueInvoices, item.OverdueInvoiceCount);
        Assert.Equal(status, item.Status);
    }

    private HttpClient Client()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Role", "Management");
        return client;
    }

    private sealed record LedgerSeed(Guid NoInvoice, Guid Pending, Guid Partial, Guid Paid, Guid Cancelled, Guid Overdue, Guid Overpaid, Guid Multiple);
}
