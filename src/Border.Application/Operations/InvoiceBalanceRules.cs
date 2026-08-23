using Border.Domain.Entities;

namespace Border.Application.Operations;

public static class InvoiceBalanceRules
{
    public static bool CountsAsDebt(InvoiceStatus status) => status != InvoiceStatus.Cancelled;

    public static decimal Remaining(InvoiceStatus status, decimal amount, decimal paid) =>
        status is InvoiceStatus.Cancelled or InvoiceStatus.Paid ? 0m : Math.Max(0m, amount - paid);
}
