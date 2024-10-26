// define stack resources
using Pulumi;
using Pulumi.AzureNative.Resources;
using System.Threading.Tasks;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Create an Azure Resource Group
        // Class ResourceGroup need Resource name (prop Name...)
        var resourceGroup = new ResourceGroup("VMResourceGroup", new ResourceGroupArgs
        {
            ResourceGroupName = "VMResourceGroup",
            // Location = "WestUS" but location is defined globally 
        });

        // Expose the Resource Group name as an output
        this.ResourceGroupName = resourceGroup.Name;
    }

    // Expose an output that contains the Resource Group name
    // ResourceGroupName can be used inside this class
    [Output]
    public Output<string> ResourceGroupName { get; private set; }
}

class Program
{
    static Task<int> Main(string[] args) => Pulumi.Deployment.RunAsync<VMWithPrivateIPAddress>();
}