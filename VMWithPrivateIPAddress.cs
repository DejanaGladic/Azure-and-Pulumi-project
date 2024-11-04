using Pulumi;
using System.IO;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Compute;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using ComputeInputs = Pulumi.AzureNative.Compute.Inputs;
using System.Collections.Immutable;
using Tls = Pulumi.Tls;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Import the program's configuration settings.
        // vars in config contribute to better code management
        var config = new Config();
        var region = config.Get("azure-native:location")!;
        // the most afordable is OS hard disk drive (HDD)
        var vmSize = config.Require("vmSize")!;
        var vmName = config.Require("vmName")!;
        var adminUsername = config.Get("adminUsername")!;
        var adminPassword = config.RequireSecret("adminPassword")!; // value will be encrypted and dont be visible and exposed
        // for checking
        Log.Info($"vmSize: {vmSize}");
        Log.Info($"vmName: {vmName}");

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
        // has to be removed later
        var publicIp = new PublicIPAddress(
            "VM-Public-Ip",
            new()
            {
                ResourceGroupName = VMResourceGroup.Name,
                PublicIPAllocationMethod = IPAllocationMethod.Dynamic,
            }
        );

        // Create a NSG (Network Security Group) with a rule for SSH access (to run a VM)
        var networkSecurityGroup = new NetworkSecurityGroup("VM-Security-Rule-Group", new()
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
                EnableAcceleratedNetworking = false, // for better performance, network offloading, better com between VM set to true
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

        // Public key from my PC
        var publicKey = File.ReadAllText("/Users/user/.ssh/my_azure_key.pub");
        // Create a VM
        // login with SSH public private key encription (pass has not been used for login and authentication to azure VM)
        var vm = new VirtualMachine(vmName, new()
        {
            ResourceGroupName = VMResourceGroup.Name,
            NetworkProfile = new ComputeInputs.NetworkProfileArgs
            {
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
                ComputerName = vmName, // set the name for VM
                AdminUsername = adminUsername,
                AdminPassword = adminPassword,
                LinuxConfiguration = new ComputeInputs.LinuxConfigurationArgs {
                    DisablePasswordAuthentication = true,
                    Ssh = new ComputeInputs.SshConfigurationArgs {
                        PublicKeys = new[]
                        {
                            // get public key from my PC and sets it in this path on linux VM
                            new ComputeInputs.SshPublicKeyArgs {
                                KeyData = publicKey,
                                Path = $"/home/{adminUsername}/.ssh/authorized_keys",
                            },
                        },
                    },
                },
            },
            StorageProfile = new ComputeInputs.StorageProfileArgs
            {
                // OS HDD is created up and this is its configs
                OsDisk = new ComputeInputs.OSDiskArgs
                {
                    Name = "VM-OS-disk",
                    // we dont create image ourself
                    CreateOption = DiskCreateOptionTypes.FromImage,
                    // use ManagedDisk and store it in Standard_LRS
                    ManagedDisk = new ComputeInputs.ManagedDiskParametersArgs
                    {
                        StorageAccountType = "Standard_LRS"  // Name for standard HDD
                    }
                },
                // image reference refers to OS creations (definition for OS creation)
                ImageReference = new ComputeInputs.ImageReferenceArgs
                {
                    // the most afordable version - Linux is better then Windows in terms of costs
                    // alpine linux is maybe better for costs but I will leave the ordinary linux 
                    Publisher = "Canonical",
                    Offer = "ubuntu-24_04-lts",
                    Sku = "server",
                    Version = "latest"
                }
            }
        });

        // Custom Script Extension has been removed

        // Once the machine is created, fetch its IP address
        // for public ip only
        var vmAddress = vm.Id.Apply(addr =>
        {
            return GetPublicIPAddress.Invoke(new()
            {
                ResourceGroupName = VMResourceGroup.Name,
                PublicIpAddressName = publicIp.Name,
            });
        });

        // Create an SSH private key for VM
        var sshKey = new Tls.PrivateKey("ssh-key", new()
        {
            Algorithm = "RSA",
            RsaBits = 4096,
        });

        // Export the public IP address and private key for SSH connection
        // only for public IP
        this.OutputValues =  vmAddress.Apply(addr => {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("ip", addr.IpAddress)
                        .Add("privatekey", sshKey.PrivateKeyOpenssh);
            return output;
        });
    }

    [Output]
    public Output<ImmutableDictionary<string, object?>> OutputValues { get; set; }
}