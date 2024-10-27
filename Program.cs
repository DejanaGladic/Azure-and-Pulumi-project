// define stack resources
using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Network;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Create an Azure Resource Group
        // ResourceGroup() need name and args fo RG config 
        // we can access RG name as resourceGroup.Name
        var resourceGroup = new ResourceGroup(
            "VMResourceGroup",
            new ResourceGroupArgs
            {
                ResourceGroupName = "VMResourceGroup",
                Location = "WestUS" // but location is defined globally too but is recommended to set it for each resource
            }
        );


        // Create a virtual network - VNet
        // VirtualNetwork() use VNet name and args
        var virtualNetwork = new VirtualNetwork(
            "VMvirtualNetwork",
            new VirtualNetworkArgs()
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "WestUS",
                // contains an array of IP address ranges that can be used by subnets
                AddressSpace = new NetworkInputs.AddressSpaceArgs
                {
                    // use CIDR notation - baseIPaddress/subnetMask
                    AddressPrefixes = new[] { "10.0.0.0/16" },
                },                              
                Subnets = new[]
                {
                    // NetworkInputs class has to be specified because we have SubnetArgs() in different classes
                    new NetworkInputs.SubnetArgs
                    {
                        // first subnet has a address range from 10.0.0.0 to 10.0.0.254 (excluded)
                        AddressPrefix = "10.0.0.0/24",
                        Name = "subnet-1-VMvirtualNetwork",
                    },
                }
            }
        );
    }

 
    // Expose the Resource Group name as an output and can be used inside code - posle cemo to 
    // this.ResourceGroupName = resourceGroup.Name;
    // Expose an output that contains the Resource Group name  - posle cemo to
    /*[Output]
    public Output<string> ResourceGroupName { get; private set; }*/
}

class Program
{
    static Task<int> Main(string[] args) => Pulumi.Deployment.RunAsync<VMWithPrivateIPAddress>();
}
