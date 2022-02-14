namespace Microsoft.eShopWeb;

public class OrderServicesOptions
{
    public const string OrderServices = "OrderServices";
    public string OrderItemsReserverURL { get; set; }
    public string OrderItemsReserverFuncKey { get; set; }
    public string DeliveryOrderDetailsURL { get; set; }
    public string DeliveryOrderDetailsFuncKey { get; set; }

}
