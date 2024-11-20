using Pulumi;
using System.IO;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Compute;
using NetworkInputs = Pulumi.AzureNative.Network.Inputs;
using ComputeInputs = Pulumi.AzureNative.Compute.Inputs;
using Tls = Pulumi.Tls;
using Pulumi.Random;

class VMWithPrivateIPAddress
{
    public VMWithPrivateIPAddress(VirtualNetwork vNet)
    {
        // to make a unique names  - can be deleted but ok
        var suffix = new RandomString("sufix", new RandomStringArgs
        {
            Length = 2,
            Special = false,
        });
        // Import the program's configuration settings.
        // vars in config contribute to better code management
        var config = new Config();
        var region = config.Get("location")!;
        // the most afordable is OS hard disk drive (HDD)
        var vmSize = config.Require("vmSize")!;
        var vmName = config.Require("vmName")!;
        var adminUsername = config.Get("adminUsername")!;
        var adminPassword = config.RequireSecret("password")!; // value will be encrypted and dont be visible and exposed

        // resource group for VM and its parts
        var VMResourceGroup = new ResourceGroup(
            $"VM-Resource-Group-{suffix}",
            new ResourceGroupArgs
            {
                Location = region
            }
        );

        //Subnet 1 id
        var subnet1Id = vNet.Subnets.GetAt(0).Apply(subnet => subnet.Id!);
        subnet1Id.Apply(id => {
            Log.Info(id);
            return id;
        });

        // Create VNIC (Virtual Network Interface Card)
        var networkInterface = new NetworkInterface(
            $"VM-NIC-{suffix}",
            new NetworkInterfaceArgs()
            {
                ResourceGroupName = VMResourceGroup.Name,
                Location = region,
                EnableAcceleratedNetworking = false, // for better performance, network offloading, better com between VM set to true
                IpConfigurations = new[]
                {
                    new NetworkInputs.NetworkInterfaceIPConfigurationArgs
                    {
                        Name = $"VM-ipconfig1-{suffix}",
                        // optionally!!!!! - no instance level public IP but public IP assigned to vNIC which is connected to VM (not the same)
                        Subnet = new NetworkInputs.SubnetArgs
                        {
                            // define subnet id in which VNIC exists - subnet was created up in the code
                            // get ID dinamically in code
                            Id = subnet1Id!,
                        },
                        // staticaly assigned public IP address for access the VM from the same VNet to stabile connection 
                        PrivateIPAllocationMethod = IPAllocationMethod.Static,
                        PrivateIPAddress = "10.0.0.100"
                    }
                }
            }
        );

        /*var customScript = "#!/bin/bash\n" +
                           "echo 'Dejana and VM'";*/


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
                //CustomData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(customScript)), // Custom data must be base64 encoded
                LinuxConfiguration = new ComputeInputs.LinuxConfigurationArgs
                {
                    DisablePasswordAuthentication = true,
                    Ssh = new ComputeInputs.SshConfigurationArgs
                    {
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
                    Name = $"VM-OS-disk-{suffix}",
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

        // Create an SSH private key for VM
        var sshKey = new Tls.PrivateKey("ssh-key", new()
        {
            Algorithm = "RSA",
            RsaBits = 4096,
        });

        // export private ssh key
        this.PrivateSshKey = sshKey.PrivateKeyOpenssh;
        // export private ip address
        /*this.PrivateIpAddress = networkInterface.IpConfigurations.Apply(ipConfigs =>
        {
            if (ipConfigs != null && ipConfigs.Count() > 0) {
                // Access the first IP configuration and return the private IP address
                return ipConfigs[0].PrivateIPAddress;
            }

        })!;*/
        this.PrivateIpAddress = Output.Create("10.0.0.100");
    }

    public Output<string> PrivateSshKey { get; set; }
    public Output<string> PrivateIpAddress { get; set; }
}