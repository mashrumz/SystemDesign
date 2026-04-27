using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Shared.Utils;

/// <summary>
/// Typed HTTP client for calling NearbyService from AvailabilityService and OrderService.
/// Register with:
///   services.AddHttpClient&lt;NearbyClient&gt;(c => c.BaseAddress = new Uri(nearbyBaseUrl));
/// </summary>
public class NearbyClient
{
    private readonly HttpClient _http;

    public NearbyClient(HttpClient http) => _http = http;

    public async Task<List<int>> GetNearbyFcIdsAsync(double lat, double lon, double radiusMiles = 30)
    {
        var url = $"/nearby?lat={lat}&lon={lon}&radiusMiles={radiusMiles}";
        var response = await _http.GetFromJsonAsync<NearbyResponse>(url);
        return response?.FcIds ?? [];
    }
}

public record NearbyResponse([property: JsonPropertyName("fcIds")] List<int> FcIds);
