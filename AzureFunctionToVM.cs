using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.Random;
using System.Linq;
using System.Collections.Immutable;
using Azure.Storage.Blobs;
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
            AccountName = storageAccount.Name,
            ContainerName = "zips"
        });

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
                Capacity = 1, //Current number of instances assigned to the resource.
                Name = "Y1",        // Size for the Consumption Plan
                Tier = "Dynamic", //consumption plan for azure function
            },
        });


        // Retrieve storage account keys for connection
        var storageAccountKeys = Output.Tuple(functionResourceGroup.Name, storageAccount.Name).Apply(async names =>
        {
            var keys = await ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
            {
                ResourceGroupName = names.Item1,
                AccountName = names.Item2,
            });
            return keys.Keys.First().Value;
        });

        // storageAccount.Name - maybe can be a problem
        // Conection string for storage
        // var connStringStorage = Output.Format($"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={storageAccountKeys};EndpointSuffix=core.windows.net");
        var connStringStorage = storageAccountKeys.Apply(key => 
            $"DefaultEndpointsProtocol=https;AccountName={storageAccount.Name};AccountKey={key};EndpointSuffix=core.windows.net"
        );

        // blob url - not sure for that
        var codeBlobUrl = storageAccountKeys.Apply(key => {
            // Construct the BlobServiceClient using the storage account credentials
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageAccount.Name}.blob.core.windows.net"), 
                new StorageSharedKeyCredential(storageAccount.Name, key));

            // Reference the container and blob within the storage account
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("zips"); // Specify your container name
            var blobClient = blobContainerClient.GetBlobClient("AzureFunLogic.zip"); // Specify the blob name

            // Build the SAS token with read permissions
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "zips", // Container name
                BlobName = "AzureFunLogic.zip", // Blob name
                Resource = "b", // The resource type (b = blob)
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1), // Expiry time for the SAS token
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read); // Grant read permissions

            // Generate the SAS token for the blob
            var sasToken = sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(storageAccount.Name, key)).ToString();

            // Combine the base URI of the blob with the SAS token to create the full URL
            return $"{blobClient.Uri}?{sasToken}";
        });

        // Web app is used as function app when the plan is consumption plan
        var app = new WebApp($"httpFunToVM", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            Kind = "FunctionApp",
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = {
                    new NameValuePairArgs
                    {
                        Name = "AzureWebJobsFeatureFlags",
                        Value = "EnableWorkerIndexing"
                    },
                    new NameValuePairArgs
                    {
                        Name = "AzureWebJobsDashboard",
                        Value = "true"
                    },
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
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~4" // Version of Azure func
                    },
                    new NameValuePairArgs
                    {
                        Name = "WEBSITE_NODE_DEFAULT_VERSION",
                        Value = "~18"
                    },
                    new NameValuePairArgs
                    {
                        Name = "WEBSITE_RUN_FROM_PACKAGE",
                        Value = codeBlobUrl
                    }
                }
            },
            HttpsOnly = true
        });


        // Compile the the app. - da ne moram rucno, ovo je korisno
        /*var outputPath = "publish";
        var publishCommand = Run.Invoke(new()
        {
            Command = $"dotnet publish --output {outputPath}",
            Dir = appPath,
        });*/


        // proveri i ovo
        // Export the Function App endpoint
        // this.FunctionEndpoint = Output.Format($"https://{app.DefaultHostName}/api/FunConnToVM");
        this.functionEndpoint = app.DefaultHostName.Apply(hostname =>
        {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("webAppUrl", $"https://{hostname}/api/SimpleHttpFunction");
            return Output.Create(output);
        });

    }

    public Output<ImmutableDictionary<string, object?>> functionEndpoint { get; set; }
}