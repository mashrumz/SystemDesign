namespace AvailabilityService.DTOs;

public record ItemAvailability(
    int ItemId,
    string ItemName,
    int TotalAvailable,
    bool InStock);

public record AvailabilityResponse(
    List<ItemAvailability> Items,
    List<int> NearbyFcIds);
