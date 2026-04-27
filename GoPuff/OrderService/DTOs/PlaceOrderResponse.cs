namespace OrderService.DTOs;

public record PlaceOrderResponse(
    int OrderId,
    string UserId,
    List<FulfilledLine> Items);

public record FulfilledLine(
    int ItemId,
    string ItemName,
    int FcId,
    string FcName,
    int Quantity);
