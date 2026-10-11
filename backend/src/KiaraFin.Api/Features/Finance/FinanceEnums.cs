namespace KiaraFin.Api.Features.Finance;

public enum FinancialAccountType
{
    Checking,
    Savings,
    Cash,
    Other
}

public enum CategoryType
{
    Income,
    Expense
}

public enum FinancialTransactionType
{
    Income,
    Expense,
    Transfer,
    InvoicePayment
}

public enum FinancialTransactionStatus
{
    Pending,
    Paid,
    Received,
    Overdue,
    Canceled
}

public enum ExpenseScope
{
    Individual,
    Shared
}

public enum RecurrenceFrequency
{
    Monthly
}

public enum RecurringTransactionStatus
{
    Active,
    Paused,
    Ended
}

public enum CreditCardStatus
{
    Active,
    Inactive
}

public enum InvoiceStatus
{
    Upcoming,
    Open,
    Closed,
    Paid,
    Canceled
}

public enum InstallmentSourceType
{
    FinancialAccount,
    CreditCard
}

public enum CashEntryKind
{
    Income,
    Expense,
    TransferIn,
    TransferOut,
    InvoicePayment,
    CreditCardPurchase
}

public enum ProjectionEntryKind
{
    ExpectedIncome,
    PendingExpense,
    InvoiceDue,
    CreditCardPurchase
}

public enum AccountMovementKind
{
    Income,
    Expense,
    TransferIn,
    TransferOut,
    InvoicePayment,
    Reversal
}
