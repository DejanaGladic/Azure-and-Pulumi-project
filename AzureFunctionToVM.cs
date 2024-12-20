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
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Network.Inputs;
using SubnetArgs = Pulumi.AzureNative.Network.SubnetArgs;

class AzureFunctionToVM
{
    public AzureFunctionToVM(ResourceGroup netRG, VirtualNetwork vNet)
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
            "Fun-Res-Group",
            new ResourceGroupArgs
            {
                Location = regionFun
            }
        );

        // Blob container for zip project folder did not work but i will leave it commented for future works
        // create a storage account - most affordable one
        /*var storageAccount = new StorageAccount("sta", new StorageAccountArgs
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

       // put a AzureFunLogic folder in previously created container
       var codeBlob = new Blob("zip", new BlobArgs
       {
           ResourceGroupName = functionResourceGroup.Name,
           AccountName = storageAccount.Name,
           ContainerName = codeContainer.Name,
           Source = new FileArchive("./HttpTrigger")
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

       // key for SAS
       var keySAS = storageAccountKeys.Apply(key => key);
       var storageAccName = storageAccount.Name.Apply(name => name);
       var connStringStorage = Output.Tuple(keySAS, storageAccName).Apply(values => {
           return $"DefaultEndpointsProtocol=https;AccountName={values.Item2};AccountKey={values.Item1};EndpointSuffix=core.windows.net";
       });

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
       });*/

        // Create a subnet for FunctionApp
        //Subnet 2 id - i will use the same as for VM
        var subnet2Id = vNet.Subnets.GetAt(0).Apply(subnet => subnet.Id!);
        subnet2Id.Apply(id => {
            Log.Info(id);
            return id;
        });

        //private endpoint for web app to communicate with resources in the same vNet
        var privateEndpoint = new PrivateEndpoint("privateEndpoint", new()
        {
            Location = regionFun, 
            ResourceGroupName = netRG.Name,
            Subnet = subnet2Id.Apply(id => new Pulumi.AzureNative.Network.Inputs.SubnetArgs{
                    Id = id
            }),
            PrivateLinkServiceConnections = new[] {
                new PrivateLinkServiceConnectionArgs
                {
                    Name = "myPrivateLinkConnection",
                    GroupIds = new[] { "sites" }, // Za Web App, ili specifičan 'groupId' za vašu uslugu
                    PrivateLinkServiceId = "/subscriptions/61ba28de-0793-479b-b7a8-95b060c074eb/resourceGroups/Fun-Res-Group/providers/Microsoft.Web/sites/httpFunToVM", // ID vaše Azure Function App ili druge usluge
                    RequestMessage = "Please approve my connection.", // Opcionalna poruka
                },
            },
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

        // Web app is used as function app when the plan is consumption plan
        var app = new WebApp("httpFunToVM", new()
        {
            ResourceGroupName = functionResourceGroup.Name,
            ServerFarmId = appServicePlan.Id,
            Kind = "FunctionApp",
            VirtualNetworkSubnetId = subnet2Id,  // subnet from VNet-a for our WebApp
            SiteConfig = new SiteConfigArgs
            {
                DetailedErrorLoggingEnabled = true,
                HttpLoggingEnabled = true,
                VnetRouteAllEnabled = true, // to enable a communication inside VNet-a
                AppSettings = {
                    /* for blob storage too
                    new NameValuePairArgs
                    {
                        // connection to azure storage
                        Name = "AzureWebJobsStorage",
                        Value = connStringStorage
                    },*/
                  /*new NameValuePairArgs
                    {
                        Name = "WEBSITE_RUN_FROM_PACKAGE",
                        Value = blobUrl
                    }*/
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_WORKER_RUNTIME",
                        Value = "dotnet" // Runtime used for function
                    },
                    new NameValuePairArgs
                    {
                        Name = "FUNCTIONS_EXTENSION_VERSION",
                        Value = "~4"
                    },

                },
                Cors = new CorsSettingsArgs
                {
                    AllowedOrigins = new[]
                    {
                        "https://portal.azure.com",
                    },
                }
            },

            HttpsOnly = true
        });

        // Export the Function App endpoint - mora apply zbog cekanja na vrednost - for storage container version
        /*this.functionEndpoint = app.DefaultHostName.Apply(hostname =>
        {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("apiURL", $"https://{hostname}/api/HttpTrigger")
                        .Add("siteURL",storageAccount.PrimaryEndpoints.Apply(primaryEndpoints => primaryEndpoints.Web))
                        .Add("blob url", blobUrl);
            return Output.Create(output);
        });*/
        this.WebAppName = app.Name;

    }

    //public Output<ImmutableDictionary<string, object?>> functionEndpoint { get; set; }
    public Output<string> WebAppName { get; set; }
}