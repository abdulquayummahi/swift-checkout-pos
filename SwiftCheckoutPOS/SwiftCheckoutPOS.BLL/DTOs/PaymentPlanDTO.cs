namespace SwiftCheckoutPOS.BLL.DTOs;

/// <summary>One requested tender. Amount null = "the rest of the bill" (only one line may omit it).</summary>
public sealed class TenderLineDTO
{
    public string Tender { get; set; } = "Cash";
    public decimal? Amount { get; set; }
    public decimal? Tendered { get; set; }
    public string? Reference { get; set; }
}

/// <summary>BP-17/PR-Q85: up to two tenders per sale.</summary>
public sealed class PaymentPlanDTO
{
    public List<TenderLineDTO> Lines { get; set; } = new();
    public static PaymentPlanDTO Single(string tender, decimal? tendered = null, string? reference = null) =>
        new() { Lines = [new TenderLineDTO { Tender = tender, Tendered = tendered, Reference = reference }] };
    public string Summary => string.Join("+", Lines.Select(l => l.Tender));
}

/// <summary>Manager/Admin credentials typed at the till to approve an exception (PR-Q67/86/93).</summary>
public sealed class ApprovalRequestDTO
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

/// <summary>A resolved, saved tender (what the receipt prints).</summary>
public sealed class ResolvedTenderDTO
{
    public string Tender { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal? Tendered { get; set; }
    public decimal Change { get; set; }
    public string? Reference { get; set; }
}
