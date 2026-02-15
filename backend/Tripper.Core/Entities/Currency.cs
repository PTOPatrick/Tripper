using System.ComponentModel.DataAnnotations;

namespace Tripper.Core.Entities;

public class Currency
{
    // ISO 4217 Code, z.B. "CHF", "EUR", "USD"
    [Key]
    [MaxLength(3)]
    public string Code { get; set; } = string.Empty;
}