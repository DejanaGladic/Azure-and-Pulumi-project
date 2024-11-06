using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.Random;

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
            $"Fun-Res-Group-{suffix_fun}",
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
        var storageAccount = new StorageAccount($"STA", new StorageAccountArgs
        {
            ResourceGroupName = functionResourceGroup.Name,
            Location = regionFun,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS,  // Most economical SKU
            },
            Kind = Kind.StorageV2,  // Recommended kind for Azure Functions
        });

        var app = new WebApp($"FUN-{suffix_fun}", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = {
                    /*new NameValuePairArgs
                    {
                        Name = "AzureWebJobsStorage",
                        Value = storageAccount. 
                    },*/
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
            }
            /*StorageAccount = new WebAppStorageAccountArgs
            {
                AccountName = storageAccount.Name,
                AccessKey = storageAccount.PrimaryAccessKey
            }*/
        });

    }
}