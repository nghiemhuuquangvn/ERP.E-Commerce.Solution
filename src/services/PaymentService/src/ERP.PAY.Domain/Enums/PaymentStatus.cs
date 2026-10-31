namespace ERP.PAY.Domain.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
    PartiallyRefunded = 6,
    /// <summary>Cash on delivery of an order in several parcels: some parcels were delivered and paid, the rest are still on their way (ROADMAP R43).</summary>
    PartiallyCaptured = 7
}

public enum PaymentMethod
{
    CreditCard = 0,
    DebitCard = 1,
    BankTransfer = 2,
    EWallet = 3,
    Cash = 4,
    CryptoPayment = 5
}

public enum RefundStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum SettlementStatus
{
    Pending = 0,
    Processing = 1,
    Settled = 2,
    Failed = 3
}
