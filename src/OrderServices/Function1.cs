using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using System.Net.Http;

namespace OrderServices
{
    public static class Function1
    {
        private static string logicAppUri = Environment.GetEnvironmentVariable("LogicAppBaseUrl", EnvironmentVariableTarget.Process);
        private static HttpClient httpClient = new HttpClient();

        [FunctionName("OrderItemsReserver")]
        public static async Task Run(
            [ServiceBusTrigger("ordersmessages", Connection = "ServiceBusConnection")] string queueMessage,
            [Blob("orderscontainer/{rand-guid}.json", FileAccess.Write, Connection = "AzureWebJobsStorage")] Stream blobContainer,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var content = Encoding.UTF8.GetBytes(queueMessage);
            try
            {
                // Exception to test if sending mail LogicApp works
                // throw new Exception("test exception");
                using (var ms = new MemoryStream(content))
                    await blobContainer.WriteAsync(content);
            }
            catch (Exception)
            {
                await httpClient.PostAsync(logicAppUri, new StringContent(queueMessage, Encoding.UTF8, "application/json"));
            }

        }
    }

    public static class Function2
    {
        [FunctionName("DeliveryOrderDetails")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [CosmosDB(
                databaseName: "Orders",
                collectionName: "DeliveryOrderDetails",
                ConnectionStringSetting = "CosmosDbConnectionString")] IAsyncCollector<dynamic> documentsOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            documentsOut.AddAsync(data);
            log.LogInformation("Order details were added to CosmosDb.");
        }
    }
}
