using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;

class AzureFunctionToVM
{
    public AzureFunctionToVM()
    { 
        var configFun = new Config();
        var regionFun = configFun.Get("azure-native:location")!;

        var functionResourceGroup = new ResourceGroup(
            "Fun-Res-Group",
            new ResourceGroupArgs
            {
                Location = regionFun
            }
        );

        // appService --> consumption plan 
        var appServicePlan = new AppServicePlan("Consumption-Plan", new()
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
        var storageAccount = new StorageAccount("Function-Storage", new StorageAccountArgs
        {
            ResourceGroupName = functionResourceGroup.Name,
            Location = regionFun,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS,  // Most economical SKU
            },
            Kind = Kind.StorageV2,  // Recommended kind for Azure Functions
        });

        var app = new WebApp("Function-To-VM", new()
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