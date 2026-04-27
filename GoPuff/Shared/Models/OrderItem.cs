namespace Shared.Models;

public class OrderItem
{
    public int OrderId { get; set; }
    public int ItemId { get; set; }
    public int FcId { get; set; }
    public int Quantity { get; set; }

    public Item Item { get; set; } = null!;
    public FulfillmentCentre FulfillmentCentre { get; set; } = null!;
}
