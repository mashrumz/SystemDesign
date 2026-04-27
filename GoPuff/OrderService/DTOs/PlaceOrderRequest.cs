using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public record PlaceOrderRequest(
    [Required] string UserId,
    [Required] double DeliveryLat,
    [Required] double DeliveryLon,
    [Required] List<OrderLineItem> Items,
    double RadiusMiles = 30);

public record OrderLineItem(
    [Required] int ItemId,
    [Required][Range(1, 100)] int Quantity);
