using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.Random;
using System.Linq;
using System.Collections.Immutable;
using Azure.Storage.Sas;
using Azure.Storage;
using System;


class AzureFunctionToVM
{
    // pogledaj sad onaj kod i uporedi sa svojim mozda ne treba sve nemam pojma...
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

        // make a resource group
        var functionResourceGroup = new ResourceGroup(
            $"Fun-Res-Group",
            new ResourceGroupArgs
            {
                Location = regionFun
            }
        );

        // create a storage account - most affordable one
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

        // create a container in which you will put AzureFunLogic folder as a Zip in Azure
        var codeContainer = new BlobContainer("zips", new BlobContainerArgs
        {
            ResourceGroupName = functionResourceGroup.Name,
            AccountName = storageAccount.Name
        });

        // Compile the the app. - ne moram jer cu vec zip kod da stavim u storage account

        // put a AzureFunLogic folder in previously created container
        var codeBlob = new Blob("zip", new BlobArgs
        {
            ResourceGroupName = functionResourceGroup.Name,
            AccountName = storageAccount.Name,
            ContainerName = codeContainer.Name,
            Source = new FileArchive("./AzureFunLogic")
        });

        // appService --> consumption plan 
        var appServicePlan = new AppServicePlan($"Consumption-Plan-{suffix_fun}", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            Location = regionFun,
            Kind = "FunctionApp",  // Specify that this is for Function Apps
            Sku = new SkuDescriptionArgs
            {
                Name = "Y1",        // Size for the Consumption Plan
                Tier = "Dynamic", //consumption plan for azure function
            },
        });


        // Retrieve storage account keys for connection - storage account is an output and some features will be known after deploy, so we use Apply and async
        var storageAccountKeys = Output.Tuple(functionResourceGroup.Name, storageAccount.Name).Apply(async names =>
        {
            var keys = await ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
            {
                ResourceGroupName = names.Item1,
                AccountName = names.Item2,
            });
            return keys.Keys.First().Value;
        });
 
        // Conection string for storage
        var connStringStorage = storageAccountKeys.Apply(key => 
            $"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={key};EndpointSuffix=core.windows.net"
        );

        // key for SAS
        var keySAS = storageAccountKeys.Apply(key => key);
        
        // url for website code in blob
        var blobUrl = Output.Tuple(storageAccount.Name, codeContainer.Name, codeBlob.Name, keySAS).Apply(values => {
                            var accountName = values.Item1;
                            var containerName = values.Item2;
                            var blobName = values.Item3;
                            var sasToken = new BlobSasBuilder {
                                BlobContainerName = containerName,
                                BlobName = blobName,
                                Resource = "b",  // Resource type: blob
                                ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)  // Set appropriate expiry
                            };
                            var key = values.Item4;
                            sasToken.SetPermissions(BlobSasPermissions.Read);                     
                            var token = sasToken.ToSasQueryParameters(new StorageSharedKeyCredential(accountName, key));
                            return $"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{token}";
        });

        // Web app is used as function app when the plan is consumption plan
        var app = new WebApp($"httpFunToVM", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            Kind = "FunctionApp",
            SiteConfig = new SiteConfigArgs
            {
                DetailedErrorLoggingEnabled = true,
                HttpLoggingEnabled = true,
                AppSettings = {
                    new NameValuePairArgs
                    {
                        // connection to azure storage
                        Name = "AzureWebJobsStorage",
                        Value = connStringStorage
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "dotnet" // Runtime used for function
                    },
                    new NameValuePairArgs
                    {
                        Name = "WEBSITE_RUN_FROM_PACKAGE",
                        Value = blobUrl
                    }
                },
                /*Cors = new CorsSettingsArgs {
                    AllowedOrigins = new[]
                    {
                        "*",
                    },
                }*/
            },
            
            HttpsOnly = true
        });

        // Export the Function App endpoint - mora apply zbog cekanja na vrednost
        this.functionEndpoint = app.DefaultHostName.Apply(hostname =>
        {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("apiURL", $"https://{hostname}/api/SimpleHttpFunction")
                        .Add("siteURL",storageAccount.PrimaryEndpoints.Apply(primaryEndpoints => primaryEndpoints.Web))
                        .Add("blob url", blobUrl);
            return Output.Create(output);
        });

    }

    public Output<ImmutableDictionary<string, object?>> functionEndpoint { get; set; }
}