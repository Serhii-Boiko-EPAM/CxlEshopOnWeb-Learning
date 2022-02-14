using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Options;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly IOptions<OrderServicesOptions> _options;
    private readonly IOptions<ServiceBusOptions> _serviceBusOptions;
    private readonly IAppLogger<OrderService> _logger;

    public OrderService(
        IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        IOptions<OrderServicesOptions> options,
        IOptions<ServiceBusOptions> serviceBusOptions,
        IAppLogger<OrderService> logger)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _options = options;
        _serviceBusOptions = serviceBusOptions;
        _logger = logger;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.GetBySpecAsync(basketSpec);

        Guard.Against.NullBasket(basketId, basket);
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        var order = new Order(basket.BuyerId, shippingAddress, items);

        await _orderRepository.AddAsync(order);
        await CallOrderItemsReserverAsync(order.OrderItems.ToList());
        await SaveDeliveryOrderDetails(order);
    }

    private async Task CallOrderItemsReserverAsync(List<OrderItem> orderItems)
    {
        await using var client = new ServiceBusClient(_serviceBusOptions.Value.ConnectionString);
        await using ServiceBusSender sender = client.CreateSender(_serviceBusOptions.Value.Queue);
        try
        {
            var itemsReserveData = orderItems.Select(item => new { itemId = item.Id, quantity = item.Units });
            string json = JsonSerializer.Serialize(itemsReserveData);
            var message = new ServiceBusMessage(json);
            await sender.SendMessageAsync(message);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex.Message);
        }
        finally
        {
            await sender.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    private async Task SaveDeliveryOrderDetails(Order order)
    {
        var deliveryOrderDetails = new DeliveryOrderDetails(order);
        string json = JsonSerializer.Serialize(deliveryOrderDetails);

        var url = $"{_options.Value.DeliveryOrderDetailsURL}{_options.Value.DeliveryOrderDetailsFuncKey}";

        await TriggerFunction(json, url);
    }

    private async Task TriggerFunction(string jsonData,string url)
    {
        using (var httpClient = new HttpClient())
        using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, url))
        using (var httpContent = new StringContent(jsonData, Encoding.UTF8, "application/json"))
        {
            httpRequest.Content = httpContent;

            await httpClient.SendAsync(httpRequest).ConfigureAwait(false);
        }
    }
}
