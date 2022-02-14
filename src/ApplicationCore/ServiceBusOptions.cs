namespace Microsoft.eShopWeb;

public class ServiceBusOptions
{
    public const string ServiceBus = "ServiceBus";
    public string ConnectionString { get; set; }
    public string Queue { get; set; }
}
