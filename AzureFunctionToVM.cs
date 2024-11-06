using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.Random;
using System.Linq;
using System.Collections.Immutable;

class AzureFunctionToVM
{
    public AzureFunctionToVM()
    { 
        // to make a unique names - can be deleted but ok
        var suffix_fun = new RandomString("sufix_fun", new RandomStringArgs
        {
            Length = 2,
            Special = false,
        });
        
        var configFun = new Config();
        var regionFun = configFun.Get("location")!;

        var functionResourceGroup = new ResourceGroup(
            $"Fun-Res-Group",
            new ResourceGroupArgs
            {
                Location = regionFun
            }
        );

        // appService --> consumption plan 
        var appServicePlan = new AppServicePlan($"Consumption-Plan-{suffix_fun}", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            Location = regionFun,
            Kind = "FunctionApp",  // Specify that this is for Function Apps
            Sku = new SkuDescriptionArgs
            {
                Capacity = 1, //Current number of instances assigned to the resource.
                Name = "Y1",        // Size for the Consumption Plan
                Tier = "Dynamic", //consumption plan for azure function
            },
        });

        // storage account - most affordable
        // Create a Storage Account - don t use for now
        var storageAccount = new StorageAccount("sta", new StorageAccountArgs
        {
            ResourceGroupName = functionResourceGroup.Name,
            Location = regionFun,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS,  // Most economical SKU
            },
            Kind = Kind.StorageV2,  // Recommended kind for Azure Functions
        });

        // Retrieve storage account keys
        var storageAccountKeys = Output.Tuple(functionResourceGroup.Name, storageAccount.Name).Apply(async names =>
        {
            var keys = await ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
            {
                ResourceGroupName = names.Item1,
                AccountName = names.Item2,
            });
            return keys.Keys.First().Value;
        });

        var app = new WebApp($"httpFunToVM", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = {
                    new NameValuePairArgs
                    {
                        // connection to azure storage
                        Name = "AzureWebJobsStorage",
                        Value = Output.Format($"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={storageAccountKeys};EndpointSuffix=core.windows.net")
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "dotnet" // Runtime used for function
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~4" // Version of Azure func
                    }
                }
            },
            HttpsOnly = true
        });

        // Export the Function App endpoint
        // this.FunctionEndpoint = Output.Format($"https://{app.DefaultHostName}/api/FunConnToVM");
        this.FunctionEndpoint =  app.DefaultHostName.Apply(hostname => {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("webAppUrl", hostname);
            return output;
        });

    }

    [Output]
    public Output<ImmutableDictionary<string, object?>> FunctionEndpoint { get; set; }
}