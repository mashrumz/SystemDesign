using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WriteService.DTOs;

public class ShortenUrlRequest
{
    [Required]
    [Url]
    [JsonPropertyName("long_url")]
    public string LongUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    [JsonPropertyName("custom_alias")]
    public string? CustomAlias { get; set; }

    [JsonPropertyName("expiration_date")]
    public DateTime? ExpirationDate { get; set; }
}