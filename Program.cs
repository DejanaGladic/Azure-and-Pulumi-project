// define stack resources
using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Network;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
class AzureStack : Stack
{
    public AzureStack()
    {
        var config = new Config();
        var region = config.Get("location")!;

        // Azure networking
        var networkResourceGroup = new ResourceGroup(
            $"Network-Resource-Group",
            new ResourceGroupArgs
            {
                Location = region
            }
        );

        // Create a NSG (Network Security Group) with a rule for SSH access (to run a VM)
        var networkSecurityGroup = new NetworkSecurityGroup($"VM-Security-Rule-Group", new()
        {
            ResourceGroupName = networkResourceGroup.Name,
            Location = region,
            // beside the default rules, we can define our own rules
            SecurityRules = new[]
            {        
                // Create a security group allowing inbound access over ports 22 for SSH!! to be able to connect tpo VM
                new NetworkInputs.SecurityRuleArgs {
                    Name = $"VM-Security-Rule-1",
                    Priority = 1000, // The lower the priority number, the higher the priority of the rule; must be unique for every rule; from 100 to 4096
                    Direction = SecurityRuleDirection.Inbound, // rule is for inbound (incoming) traffic
                    Access = "Allow", // network traffic is allowed
                    Protocol = "Tcp", // rule is applied only on Tcp protocols - SSH use this protocol
                    // it is not recommended to set * because of security but okay for now
                    SourcePortRange = "*", // * means all source ports
                    SourceAddressPrefix = "*", // * means all source Ip addresses
                    DestinationAddressPrefix = "*",
                    DestinationPortRanges = new[] // this is port from my VM for SSH traffic
                    {
                        "22"
                    } // This Security Rule 1 means that all TCP traffic from all source ports and all source IP addresses 
                      // are allowed to all destination IP addresses, but only to the ports specified in DestinationPortRanges (e.g., port 22)
                }
            }
        });

        // Create a virtual network - VNet
        // VirtualNetwork() use VNet name and args
        var virtualNetwork = new VirtualNetwork(
            $"VM-Virtual-Network",
            new VirtualNetworkArgs()
            {
                ResourceGroupName = networkResourceGroup.Name,
                Location = region,
                // contains an array of IP address ranges that can be used by subnets
                AddressSpace = new NetworkInputs.AddressSpaceArgs
                {
                    // use CIDR notation - baseIPaddress/subnetMask
                    // just private IP addresses within the VNet
                    AddressPrefixes = new[] { "10.0.0.0/16" },
                },
                Subnets = new[]
                {
                    // NetworkInputs class has to be specified because we have SubnetArgs() in different classes
                    new NetworkInputs.SubnetArgs
                    {
                        Name = $"VMVirtualNetwork-subnets",
                        // first subnet has a address range from 10.0.0.0 to 10.0.0.254 (excluded)
                        AddressPrefix = "10.0.0.0/24", 
                        // Make an association NSG with Subnet
                        // Association NSG with NIC has been deleted because NSG on NIC can be hard to manage
                        NetworkSecurityGroup = new NetworkInputs.NetworkSecurityGroupArgs
                        {
                            Id = networkSecurityGroup.Id
                        }
                    }
                }
            }
        );

        // da bismo osigurali da ce se prvo kreirati virtual network i subnetovi
        virtualNetwork.Subnets.Apply(subnets => {
            // Kreirajte VM resurs - for now i dont use that
            var vmResource = new VMWithPrivateIPAddress(virtualNetwork);

            // Kreirajte Azure Function resurs
            var functionResource = new AzureFunctionToVM(networkResourceGroup, virtualNetwork);

            // Register the outputs
            this.PrivateSshKey = vmResource.PrivateSshKey!;
            this.PrivateSshKey = vmResource.PrivateIpAddress!;
            //this.FunctionEndpoint = functionResource.functionEndpoint!;
            this.WebAppName = functionResource.WebAppName;
            return 0;
        });



    }

    //[Output]
    //public Output<ImmutableDictionary<string, object?>> FunctionEndpoint { get; set; }

    [Output]
    public Output<string> WebAppName { get; set; }
    
    [Output]
    public Output<string> PrivateSshKey { get; set; }

    [Output]
    public Output<string> PrivateIpAddress { get; set; }
}

class Program
{
    static Task<int> Main() => Pulumi.Deployment.RunAsync<AzureStack>();

}