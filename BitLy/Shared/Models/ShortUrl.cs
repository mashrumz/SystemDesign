using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models;

public class ShortUrl
{
    [Key]
    public long Id { get; set; }

    [Required]
    [MaxLength(2048)]
    public string LongUrl { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ShortCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CustomAlias { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}