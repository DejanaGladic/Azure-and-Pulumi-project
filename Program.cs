// define stack resources
using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Create an Azure Resource Group
        // ResourceGroup() need name and args fo RG config
        // we can access RG name as resourceGroup.Name
        var resourceGroup = new ResourceGroup(
            "VM-Resource-Group",
            new ResourceGroupArgs
            {
                //ResourceGroupName = "VMResourceGroup", mislim da je nepotrebno
                Location =
                    "WestUS" // but location is defined globally too but is recommended to set it for each resource
                ,
            }
        );

        // Create a virtual network - VNet
        // VirtualNetwork() use VNet name and args
        var virtualNetwork = new VirtualNetwork(
            "VM-Virtual-Network",
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
                        Name = "VMVirtualNetwork-subnet-1",
                    },
                },
            }
        );

        // Create a public IP address for the VM and VNIC - separate from subnet - just for initial project
        // going to be used in VNIC
        var publicIp = new PublicIPAddress(
            "VM-Public-Ip",
            new()
            {
                ResourceGroupName = resourceGroup.Name,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic
               /*DnsSettings = new NetworkInputs.PublicIPAddressDnsSettingsArgs
                {
                    DomainNameLabel = domainNameLabel,
                },*/
            }
        );

        // Create VNIC (Virtual Network Interface Card)
        var networkInterface = new NetworkInterface(
            "VM-NIC",
            new NetworkInterfaceArgs()
            {
                ResourceGroupName = resourceGroup.Name,
                Location = "WestUS",
                //DisableTcpStateTracking = true, ovo bas ne znam da li nam znaci ili ne
                //EnableAcceleratedNetworking = true, ovo bas ne znam da li nam znaci ili ne
                IpConfigurations = new[]
                {
                    new NetworkInputs.NetworkInterfaceIPConfigurationArgs
                    {
                        Name = "VM-ipconfig1",
                        PublicIPAddress = new NetworkInputs.PublicIPAddressArgs
                        {
                            // dynamically assigned public IP address for access the VM from Internet - needs to be changed
                            Id = publicIp.Id!
                                
                        },
                        Subnet = new NetworkInputs.SubnetArgs
                        {
                            // define subnet id in which VNIC exists - subnet was created up in the code
                            // get ID dinamically in code
                            Id = virtualNetwork.Subnets.GetAt(0).Apply(subnet => subnet.Id!),
                        },
                        // dynamically assigned public IP address for access the VM from the same VNet - for secure communication inside the VNet
                        // choose from subnet range (defined above)
                        PrivateIPAllocationMethod = IPAllocationMethod.Dynamic,
                    },
                },
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
