using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;

public class DeliveryOrderDetails
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("buyerId")]
    public string BuyerId { get; set; }

    [JsonPropertyName("shipToAddress")]
    public Address ShipToAddress { get; set; }

    [JsonPropertyName("orderItems")]
    public IReadOnlyCollection<OrderItem> OrderItems { get; set; }

    [JsonPropertyName("finalPrice")]
    public decimal FinalPrice { get; set; }

    public DeliveryOrderDetails(Order order)
    {
        Id = Guid.NewGuid();
        BuyerId = order.BuyerId;
        ShipToAddress = order.ShipToAddress;
        OrderItems = order.OrderItems;
        FinalPrice = order.Total();
    }
}
