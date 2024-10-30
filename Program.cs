// define stack resources
using System.Threading.Tasks;
using Pulumi;
using System.IO;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Compute;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using ComputeInputs = Pulumi.AzureNative.Compute.Inputs;
using System.Collections.Immutable;
using System.Collections.Generic;

class VMWithPrivateIPAddress : Stack
{
    public VMWithPrivateIPAddress()
    {
        // Import the program's configuration settings.
        // vars u config contribute to better code management
        var config = new Config();
        var region = config.Get("azure-native:location")!;
        // the most afordable is OS hard disk drive (HDD)
        var vmSize = config.Get("vmSize")!;
        var vmName = config.Get("vmName")!;
        var adminUsername = config.Get("adminUsername")!;
        var adminPassword = config.RequireSecret("password")!; // value will be encrypted and dont be visible and exposed

        // don t use port for URL for now
        //var servicePort = config.Get("servicePort") ?? "80";


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
                // if we have public IP DNS will be good to have
                /*DnsSettings = new NetworkInputs.PublicIPAddressDnsSettingsArgs
                {
                    DomainNameLabel = "dejanas-first-VM",
                }*/
            }
        );

        // Create a NSG (Network Security Group) without a rule for now
        var networkSecurityGroup = new NetworkSecurityGroup("VM-Security-Rule-Group", new()
        {
            ResourceGroupName = networkResourceGroup.Name,
            Location = region /*,
            // beside the default rules, we can define our own rules

            SecurityRules = new[]
            {
            
                // Create a security group allowing inbound access over ports 80 (for HTTP!!) and 22 (for SSH!!)??
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
            }*/
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

        // Create a VM
        // for now login with username and pass but improvements can be: SSH public private key encription + would include some changes in NSG 
        var vm = new VirtualMachine("VM-Azure-Pulumi", new()
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
                ComputerName = vmName,
                AdminUsername = adminUsername,
                AdminPassword = adminPassword,
                WindowsConfiguration = new ComputeInputs.WindowsConfigurationArgs
                {
                    EnableAutomaticUpdates = false, // by default is true but I dont need automatic updates

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
                    // the most afordable version - Linux is better then Windows in terms of costs
                    // alpine linux is maybe better for costs but I will leave the ordinary linux 
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "20.04-LTS",
                    Version = "latest"
                }
            }
        });

        // read the script 
        var initScriptPath = "first-script.sh";
        var initScript = File.ReadAllText(initScriptPath);
        // use the script
        var vmScriptExtension = new VirtualMachineExtension("VM-Custom-Script-Extension", new VirtualMachineExtensionArgs
        {
            ResourceGroupName = VMResourceGroup.Name,
            VmName = vmName,
            Publisher = "Microsoft.Azure.Extensions",
            Type = "CustomScript",
            TypeHandlerVersion = "2.0",
            Settings = new Dictionary<string, object>
            {   //command to execute when VM runs --> run the shell script provided in file named first-script.sh
                    { "commandToExecute", $"chmod +x {initScriptPath} && /bin/bash -c '{initScriptPath}'" }
            }
        }
        );

        // Once the machine is created, fetch its IP address and DNS hostname
        // for public ip only
        var vmAddress = vm.Id.Apply(_ =>
        {
            return GetPublicIPAddress.Invoke(new()
            {
                ResourceGroupName = VMResourceGroup.Name,
                PublicIpAddressName = publicIp.Name,
            });
        });

        // Export the VM's hostname, public IP address, 
        // HTTP URL and SSH missed for now
        // only for public IP
        this.OutputValues =  vmAddress.Apply(addr => {
            var output = ImmutableDictionary<string, object?>.Empty
                        .Add("ip", addr.IpAddress); 
            return output;
            // not to forgive if need
            //["hostname"] = vmAddress.Apply(addr => addr.DnsSettings!.Fqdn),
            //["url"] = vmAddress.Apply(addr => $"http://{addr.DnsSettings!.Fqdn}:{servicePort}")

        });
    }

    [Output]
    public Output<ImmutableDictionary<string, object?>> OutputValues { get; set; }
}

class Program
{
    static Task<int> Main(string[] args) => Pulumi.Deployment.RunAsync<VMWithPrivateIPAddress>();
}
