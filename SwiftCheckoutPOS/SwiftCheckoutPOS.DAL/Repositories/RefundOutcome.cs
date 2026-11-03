namespace SwiftCheckoutPOS.DAL.Repositories;

/// <summary>Result of a full-sale refund attempt (IMM-04). Only Refunded changed any data.</summary>
public enum RefundOutcome
{
    Refunded,
    NotFound,
    AlreadyRefunded,
    NotRefundable,
    Conflict
}
