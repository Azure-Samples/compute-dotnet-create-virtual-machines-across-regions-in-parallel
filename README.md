---
services: Compute
platforms: .Net
author: selvasingh
---

#Getting Started with Compute - Create Virtual Machines In Parallel - in .Net #

      Azure compute sample for creating multiple virtual machines in parallel.
       - Define 1 virtual network per region
       - Define 1 storage account per region
       - Create 5 virtual machines in 2 regions using defined virtual network and storage account
       - Create a traffic manager to route traffic across the virtual machines


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-sdk-for-net/blob/Fluent/AUTH.md).

    git clone https://github.com/Azure-Samples/compute-dotnet-create-virtual-machines-across-regions-in-parallel.git

    cd compute-dotnet-create-virtual-machines-across-regions-in-parallel

    dotnet restore

    dotnet run

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.