// define stack resources
using System.Threading.Tasks;
using Pulumi;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Compute;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using ComputeInputs = Pulumi.AzureNative.Compute.Inputs;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Import the program's configuration settings.
        // zasto u config ovo izdvajamo npr?
        var config = new Config();
        var region = config.Get("azure-native:location")!;
        // the most afordable is OS hard disk drive (HDD)
        var vmSize = config.Get("vmSize")!;
        var vmName = config.Get("vmName")!;
        var adminUsername = config.Get("adminUsername")!;
        var adminPassword = config.RequireSecret("password")!; // value will be encrypted and dont be visible and exposed

        // ??
        var servicePort = config.Get("servicePort") ?? "80";
       

        // Create an Azure Resource Groups
        // ResourceGroup() need name and args fo RG config
        // we can access RG name as resourceGroup.Name
        // resource group for networking
        var networkResourceGroup = new ResourceGroup(
            "Network-Resource-Group",
            new ResourceGroupArgs
            {
                Location = region
            }
        );

        // resource group for VM and its parts
        var VMResourceGroup = new ResourceGroup(
            "VM-Resource-Group",
            new ResourceGroupArgs
            {
                Location = region
            }
        );

        // Create a public IP address for the VM and VNIC - separate from subnet - just for initial project
        // going to be used in VNIC
        var publicIp = new PublicIPAddress(
            "VM-Public-Ip",
            new()
            {
                ResourceGroupName = VMResourceGroup.Name,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic
                /*DnsSettings = new NetworkInputs.PublicIPAddressDnsSettingsArgs
                 {
                     DomainNameLabel = domainNameLabel,
                 },*/
            }
        );

        // Create a NSG (Network Security Group) with a rule
        // Create a security group allowing inbound access over ports 80 (for HTTP) and 22 (for SSH)??
        // ovo je sve demonstrativno za sada jer sve zavisi sta nama treba
        var networkSecurityGroup = new NetworkSecurityGroup("VM-Security-Rule-Group", new()
        {
            ResourceGroupName = networkResourceGroup.Name,
            Location = region,
            // beside the default rules, we can define our own rules
            SecurityRules = new[]
            {
                new NetworkInputs.SecurityRuleArgs {
                    Name = $"VM-Security-Rule-1",
                    Priority = 1000, // The lower the priority number, the higher the priority of the rule; must be unique for every rule; from 100 to 4096
                    Direction = SecurityRuleDirection.Inbound, // rule is for inbound (incoming) traffic
                    Access = "Allow", // network traffic is allowed
                    Protocol = "Tcp", // rule is applied only on Tcp protocols
                    // opet nam ovo zavisi od cega se stitimo...
                    SourcePortRange = "*", // * means all source ports
                    SourceAddressPrefix = "*", // * means all source Ip addresses
                    DestinationAddressPrefix = "*",
                    DestinationPortRanges = new[]
                    {
                        servicePort,
                        "22",
                    } // This Security Rule 1 means that all TCP traffic from all source ports and all source IP addresses 
                      // are allowed to all destination IP addresses, but only to the ports specified in DestinationPortRanges (e.g., servicePort and 22)
                }
            },
        });

        // Create a virtual network - VNet
        // VirtualNetwork() use VNet name and args
        var virtualNetwork = new VirtualNetwork(
            "VM-Virtual-Network",
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
                        Name = "VMVirtualNetwork-subnet-1",
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

        // Create VNIC (Virtual Network Interface Card)
        var networkInterface = new NetworkInterface(
            "VM-NIC",
            new NetworkInterfaceArgs()
            {
                ResourceGroupName = VMResourceGroup.Name,
                Location = region,
                //DisableTcpStateTracking = true, ovo bas ne znam da li nam znaci ili ne
                //EnableAcceleratedNetworking = true, ovo bas ne znam da li nam znaci ili ne
                IpConfigurations = new[]
                {
                    new NetworkInputs.NetworkInterfaceIPConfigurationArgs
                    {
                        Name = "VM-ipconfig1",
                        // optionally!!!!! - no instance level public IP but public IP assigned to vNIC which is connected to VM (not the same)
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
                    }
                }
            }
        );

        // Create a VM
        // for now login with username and pass but improvements can be: SSH public private key encription + would include some changes in NSG 
        var vm = new VirtualMachine("VM-Azure-Pulumi", new()
        {
            ResourceGroupName = VMResourceGroup.Name,
            NetworkProfile = new ComputeInputs.NetworkProfileArgs {
                NetworkInterfaces = new[] {
                    new ComputeInputs.NetworkInterfaceReferenceArgs {
                        Id = networkInterface.Id,
                        Primary = true, //need to be set when we have more NICs for 1 VM
                    }
                }
            },
            HardwareProfile = new ComputeInputs.HardwareProfileArgs 
            {
                VmSize = vmSize,
            },
            OsProfile = new ComputeInputs.OSProfileArgs
            {
                ComputerName = vmName,
                // da li mi treba admin user ako nemam RDP vec hocu Bastion?
                AdminUsername = adminUsername,
                AdminPassword = adminPassword, 
                WindowsConfiguration = new ComputeInputs.WindowsConfigurationArgs
                {
                    EnableAutomaticUpdates = true, // by default is true but I dont need automatic updates
                    
                }
            },
            StorageProfile = new ComputeInputs.StorageProfileArgs
            {
                // OS HDD is created up and this is its configs
                OsDisk = new ComputeInputs.OSDiskArgs
                {
                    Name = $"{vmName}-osdisk",
                    // we dont create image ourself
                    CreateOption = DiskCreateOptionTypes.FromImage,
                    // use ManagedDisk
                    ManagedDisk = new ComputeInputs.ManagedDiskParametersArgs
                    {
                        StorageAccountType = "Standard_LRS"  // Name for standard HDD
                    }
                },
                // image reference refers to OS creations (definition for OS creation)
                ImageReference = new ComputeInputs.ImageReferenceArgs 
                {
                    // the most afordable version
                    Publisher = "MicrosoftWindowsServer",
                    Offer = "WindowsServer",
                    Sku = "2019-Datacenter",
                    Version = "latest"
                }
            }
        });
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
