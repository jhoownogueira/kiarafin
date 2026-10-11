namespace KiaraFin.Api.Features.Finance;

public sealed record InstallmentAllocation(int Number, decimal Amount);

public static class InstallmentAllocator
{
    public static IReadOnlyList<InstallmentAllocation> Split(decimal totalAmount, int installmentCount)
    {
        FinanceGuard.PositiveMoney(totalAmount, nameof(totalAmount));
        if (installmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(installmentCount));

        var totalCents = decimal.ToInt64(totalAmount * 100m);
        if (totalCents < installmentCount)
            throw new ArgumentException("Total amount must allocate at least one cent to each installment.", nameof(totalAmount));

        var baseCents = totalCents / installmentCount;
        var remainder = totalCents % installmentCount;

        var result = new InstallmentAllocation[installmentCount];
        for (var index = 0; index < installmentCount; index++)
        {
            var cents = baseCents + (index < remainder ? 1 : 0);
            result[index] = new InstallmentAllocation(index + 1, cents / 100m);
        }

        return result;
    }
}

public static class MonthlyDateCalculator
{
    public static DateOnly Resolve(int year, int month, int desiredDay)
    {
        FinanceGuard.DayOfMonth(desiredDay, nameof(desiredDay));
        var day = Math.Min(desiredDay, DateTime.DaysInMonth(year, month));
        return new DateOnly(year, month, day);
    }

    public static DateOnly AddMonthsKeepingDesiredDay(DateOnly date, int months, int desiredDay)
    {
        FinanceGuard.DayOfMonth(desiredDay, nameof(desiredDay));
        var target = new DateOnly(date.Year, date.Month, 1).AddMonths(months);
        return Resolve(target.Year, target.Month, desiredDay);
    }
}

public sealed record InvoiceCycle(DateOnly StartDate, DateOnly ClosingDate, DateOnly DueDate);

public static class InvoiceCycleCalculator
{
    // Purchases made on the adjusted closing date remain in the cycle that closes that day.
    public static InvoiceCycle ForPurchase(DateOnly purchaseDate, int closingDay, int dueDay)
    {
        FinanceGuard.DayOfMonth(closingDay, nameof(closingDay));
        FinanceGuard.DayOfMonth(dueDay, nameof(dueDay));

        var candidateClosing = MonthlyDateCalculator.Resolve(
            purchaseDate.Year,
            purchaseDate.Month,
            closingDay);
        var closingDate = purchaseDate <= candidateClosing
            ? candidateClosing
            : MonthlyDateCalculator.AddMonthsKeepingDesiredDay(candidateClosing, 1, closingDay);

        var previousClosing = MonthlyDateCalculator.AddMonthsKeepingDesiredDay(closingDate, -1, closingDay);
        var dueMonthOffset = dueDay <= closingDay ? 1 : 0;
        var dueDate = MonthlyDateCalculator.AddMonthsKeepingDesiredDay(closingDate, dueMonthOffset, dueDay);

        // Month shortening can make both adjusted dates equal (for example, closing 31 and due 30 in February).
        if (dueDate <= closingDate)
            dueDate = MonthlyDateCalculator.AddMonthsKeepingDesiredDay(closingDate, 1, dueDay);

        return new InvoiceCycle(previousClosing.AddDays(1), closingDate, dueDate);
    }
}

public sealed record CashLedgerEntry(decimal Amount, CashEntryKind Kind, bool IsSettled, bool IsCanceled = false);

public static class AccountBalanceCalculator
{
    public static decimal Calculate(decimal initialBalance, IEnumerable<CashLedgerEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        FinanceGuard.Money(initialBalance, nameof(initialBalance), allowNegative: true);

        var balance = initialBalance;
        foreach (var entry in entries)
        {
            FinanceGuard.Money(entry.Amount, nameof(entries));
            if (!entry.IsSettled || entry.IsCanceled)
                continue;

            balance += entry.Kind switch
            {
                CashEntryKind.Income or CashEntryKind.TransferIn => entry.Amount,
                CashEntryKind.Expense or CashEntryKind.TransferOut or CashEntryKind.InvoicePayment => -entry.Amount,
                CashEntryKind.CreditCardPurchase => 0m,
                _ => throw new ArgumentOutOfRangeException(nameof(entries), "Unknown cash entry kind.")
            };
        }

        return balance;
    }
}

public sealed record ProjectionEntry(
    decimal Amount,
    ProjectionEntryKind Kind,
    DateOnly DueDate,
    bool IsCanceled = false);

public sealed record FinancialProjection(
    decimal CurrentBalance,
    decimal ExpectedIncome,
    decimal PendingExpenses,
    decimal ExpectedInvoices,
    decimal ProjectedBalance);

public static class FinancialProjectionCalculator
{
    public static FinancialProjection Calculate(
        decimal currentBalance,
        DateOnly throughDate,
        IEnumerable<ProjectionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        FinanceGuard.Money(currentBalance, nameof(currentBalance), allowNegative: true);

        var income = 0m;
        var expenses = 0m;
        var invoices = 0m;

        foreach (var entry in entries)
        {
            FinanceGuard.Money(entry.Amount, nameof(entries));
            if (entry.IsCanceled || entry.DueDate > throughDate)
                continue;

            switch (entry.Kind)
            {
                case ProjectionEntryKind.ExpectedIncome:
                    income += entry.Amount;
                    break;
                case ProjectionEntryKind.PendingExpense:
                    expenses += entry.Amount;
                    break;
                case ProjectionEntryKind.InvoiceDue:
                    invoices += entry.Amount;
                    break;
                case ProjectionEntryKind.CreditCardPurchase:
                    // Informational only: its cash obligation is represented by InvoiceDue.
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entries), "Unknown projection entry kind.");
            }
        }

        return new FinancialProjection(
            currentBalance,
            income,
            expenses,
            invoices,
            currentBalance + income - expenses - invoices);
    }
}
