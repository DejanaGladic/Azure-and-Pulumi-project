using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AzureFunLogic
{
    public static class SimpleHttpFunction
    {
        [FunctionName("SimpleHttpFunction")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            try{
                var res = req.HttpContext.Response;

                res.Headers.Append("Access-Control-Allow-Origin", "*");
                res.Headers.Append("Access-Control-Allow-Methods", "GET");

                if (HttpMethods.IsOptions(req.Method)) {

                    return new NoContentResult();

                }

                res.Headers.Append("Content-Type", "application/json");
                log.LogInformation("C# HTTP trigger function processed a request.");

                return new OkObjectResult("Dejana has created AzureFunction!");
            }
            catch (Exception ex) {
                log.LogError($"Error occurred: {ex.Message}", ex);
                return new StatusCodeResult(500);  // Internal server error
            }
        }
    }
}
