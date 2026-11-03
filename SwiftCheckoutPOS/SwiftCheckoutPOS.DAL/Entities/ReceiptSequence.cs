using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities;

/// <summary>DR-26/PR-Q45: last receipt number used per business day (Asia/Dhaka).</summary>
public class ReceiptSequence
{
    [Key] public DateTime BusinessDate { get; set; }
    public int LastNumber { get; set; }
}
