// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.TrafficManager;

namespace CreateVirtualMachinesInParallel
{
    public class Program
    {
        private static readonly string Username = Utilities.CreateUsername();
        private static readonly string Password = Utilities.CreatePassword();

        /**
         * Azure compute sample for creating multiple virtual machines in parallel.
         *  - Define 1 virtual network per region
         *  - Define 1 storage account per region
         *  - Create 5 virtual machines in 2 regions using defined virtual network and storage account
         *  - Create a traffic manager to route traffic across the virtual machines
         */
        public static async Task RunSample(ArmClient client)
        {
            string rgName = Utilities.CreateRandomName("rgCOMV");
            var networkName = Utilities.CreateRandomName("vnetCOPD-");
            var storageAccountName = Utilities.CreateRandomName("stgcopd");
            var storageAccountSkuName = Utilities.CreateRandomName("stasku");
            var trafficManagerName = Utilities.CreateRandomName("tra");
            IDictionary<AzureLocation, int> virtualMachinesByLocation = new Dictionary<AzureLocation, int>();

            virtualMachinesByLocation.Add(AzureLocation.EastUS, 5);
            virtualMachinesByLocation.Add(AzureLocation.SouthCentralUS, 5);
            try
            {
                //=============================================================
                // Create a resource group (Where all resources gets created)
                //
                var lro = await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                var resourceGroup = lro.Value;
                var virtualMachineCollection = resourceGroup.GetVirtualMachines();
                var publicIpAddressCollection = resourceGroup.GetPublicIPAddresses();

                Utilities.Log($"Created a new resource group - {resourceGroup.Id}");

                var publicIpCreatableKeys = new List<string>();
                // Prepare a batch of Creatable definitions
                //
                var creatableVirtualMachines = new List<VirtualMachineResource>();

                foreach (var entry in virtualMachinesByLocation)
                {
                    var region = entry.Key;
                    var vmCount = entry.Value;

                    //=============================================================
                    // Create 1 network creatable per region
                    // Prepare Creatable Network definition (Where all the virtual machines get added to)
                    //
                    var networkCollection = resourceGroup.GetVirtualNetworks();
                    var networkData = new VirtualNetworkData()
                    {
                        Location = region,
                        AddressPrefixes =
                    {
                        "172.16.0.0/16"
                    }
                    };
                    var networkCreatable = (await networkCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, networkName, networkData)).Value;

                    //=============================================================
                    // Create 1 storage creatable per region (For storing VMs disk)
                    //
                    var storageAccountCollection = resourceGroup.GetStorageAccounts();
                    var storageAccountData = new StorageAccountCreateOrUpdateContent(new StorageSku(storageAccountSkuName), StorageKind.Storage, region);
                    {
                    };
                    var storageAccountCreatable = await storageAccountCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageAccountData);

                    var linuxVMNamePrefix = Utilities.CreateRandomName("vm-");
                    for (int i = 1; i <= vmCount; i++)
                    {
                        //=============================================================
                        // Create 1 public IP address creatable
                        var publicIPAddressData = new PublicIPAddressData()
                        {
                            Location = region,
                            DnsSettings =
                            {
                                DomainNameLabel = $"{linuxVMNamePrefix}-{i}"
                            }
                        };
                        var publicIpAddressCreatable = (await publicIpAddressCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, $"{linuxVMNamePrefix}-{i}", publicIPAddressData)).Value;

                        publicIpCreatableKeys.Add(publicIpAddressCreatable.Data.Name);

                        //=============================================================
                        // Create 1 virtual machine creatable
                        //Create a subnet
                        Utilities.Log("Creating a Linux subnet...");
                        var subnetName = Utilities.CreateRandomName("subnet_");
                        var subnetData = new SubnetData()
                        {
                            ServiceEndpoints =
                    {
                        new ServiceEndpointProperties()
                        {
                            Service = "Microsoft.Storage"
                        }
                    },
                            Name = subnetName,
                            AddressPrefix = "10.0.0.0/28",
                        };
                        var subnetLRro = await networkCreatable.GetSubnets().CreateOrUpdateAsync(WaitUntil.Completed, subnetName, subnetData);
                        var subnet = subnetLRro.Value;
                        Utilities.Log("Created a Linux subnet with name : " + subnet.Data.Name);

                        //Create a networkInterface
                        Utilities.Log("Created a linux networkInterface");
                        var networkInterfaceData = new NetworkInterfaceData()
                        {
                            Location = region,
                            IPConfigurations =
                    {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "internal",
                            Primary = true,
                            Subnet = new SubnetData
                            {
                                Name = subnetName,
                                Id = new ResourceIdentifier($"{networkCreatable.Data.Id}/subnets/{subnetName}")
                            },
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            PublicIPAddress = publicIpAddressCreatable.Data,
                        }
                    }
                        };
                        var networkInterfaceName = Utilities.CreateRandomName("networkInterface");
                        var nic = (await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(WaitUntil.Completed, networkInterfaceName, networkInterfaceData)).Value;
                        Utilities.Log("Created a Linux networkInterface with name : " + nic.Data.Name);
                        //Create a VM with the Public IP address
                        Utilities.Log("Creating a zonal VM with implicitly zoned related resources (PublicIP, Disk)");
                        var linuxComputerName = Utilities.CreateRandomName("linuxComputer");
                        var linuxVmdata = new VirtualMachineData(region)
                        {
                            HardwareProfile = new VirtualMachineHardwareProfile()
                            {
                                VmSize = "Standard_D2a_v4"
                            },
                            OSProfile = new VirtualMachineOSProfile()
                            {
                                AdminUsername = Username,
                                AdminPassword = Password,
                                ComputerName = linuxComputerName,
                            },
                            NetworkProfile = new VirtualMachineNetworkProfile()
                            {
                                NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic.Id,
                                Primary = true,
                            }
                        }
                            },
                            StorageProfile = new VirtualMachineStorageProfile()
                            {
                                OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                                {
                                    OSType = SupportedOperatingSystemType.Linux,
                                    Caching = CachingType.ReadWrite,
                                    ManagedDisk = new VirtualMachineManagedDisk()
                                    {
                                        StorageAccountType = StorageAccountType.StandardLrs
                                    }
                                },
                                ImageReference = new ImageReference()
                                {
                                    Publisher = "Canonical",
                                    Offer = "UbuntuServer",
                                    Sku = "16.04-LTS",
                                    Version = "latest",
                                },
                            },
                            Zones =
                    {
                        "1"
                    },
                        };
                        var virtualMachine1Lro = await virtualMachineCollection.CreateOrUpdateAsync(WaitUntil.Completed, $"{linuxVMNamePrefix}-{i}", linuxVmdata);
                        var virtualMachineCreatable = virtualMachine1Lro.Value;
                        Utilities.Log("Created a zonal virtual machine: " + virtualMachineCreatable.Id);
                        creatableVirtualMachines.Add(virtualMachineCreatable);
                    }
                }

                //=============================================================
                // Create !!

                var t1 = DateTimeOffset.Now.UtcDateTime;
                Utilities.Log("Creating the virtual machines");

                var virtualMachines = virtualMachineCollection.GetAll();

                var t2 = DateTimeOffset.Now.UtcDateTime;
                Utilities.Log("Created virtual machines");

                foreach (var virtualMachine in virtualMachines)
                {
                    Utilities.Log(virtualMachine.Id);
                }

                Utilities.Log($"Virtual machines create: took {(t2 - t1).TotalSeconds } seconds to create == " + creatableVirtualMachines.Count + " == virtual machines");

                var publicIpResourceIds = new List<string>();
                var publicIpAddresses = publicIpAddressCollection.GetAll();
                foreach (var publicIpAddress in publicIpAddresses)
                {
                    publicIpResourceIds.Add(publicIpAddress.Id);
                }

                //=============================================================
                // Create 1 Traffic Manager Profile
                //
                var trafficManagerCollection = resourceGroup.GetTrafficManagerProfiles();
                var trafficData = new TrafficManagerProfileData()
                {
                };
                var trafficManagerProfile_lro = await trafficManagerCollection.CreateOrUpdateAsync(WaitUntil.Completed, trafficManagerName, trafficData);
                var trafficManagerProfile = trafficManagerProfile_lro.Value;
                int endpointPriority = 1;
                var endpointCollection = trafficManagerProfile.GetTrafficManagerEndpoints();
                foreach (var publicIpResourceId in publicIpResourceIds)
                {
                    var endpointName = $"azendpoint-{endpointPriority}";
                    if (endpointPriority == 1)
                    {
                        var data = new TrafficManagerEndpointData()
                        {
                            TargetResourceId = new ResourceIdentifier(publicIpResourceId),
                            Priority = endpointPriority
                        };
                        var endpoint = (await endpointCollection.CreateOrUpdateAsync(WaitUntil.Completed, "Microsoft.network/TrafficManagerProfiles/ExternalEndpoints", endpointName, data)).Value;
                    }
                    else
                    {
                        profileWithCreate = profileWithCreate.DefineAzureTargetEndpoint(endpointName)
                                .ToResourceId(publicIpResourceId)
                                .WithRoutingPriority(endpointPriority)
                                .Attach();
                    }
                    endpointPriority++;
                }

                var trafficManagerProfile = profileWithCreate.Create();
                Utilities.Log("Created a traffic manager profile - " + trafficManagerProfile.Id);
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    azure.ResourceGroups.DeleteByName(rgName);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (Exception)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
            }
        }

        public static void Main(string[] args)
        {

            try
            {
                //=================================================================
                // Authenticate
                var credentials = SdkContext.AzureCredentialsFactory.FromFile(Environment.GetEnvironmentVariable("AZURE_AUTH_LOCATION"));

                var azure = Azure
                    .Configure()
                    .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                    .Authenticate(credentials)
                    .WithDefaultSubscription();

                // Print selected subscription
                Utilities.Log("Selected subscription: " + azure.SubscriptionId);

                RunSample(azure);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}
