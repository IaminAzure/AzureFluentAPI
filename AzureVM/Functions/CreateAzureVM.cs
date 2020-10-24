using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using AzureVM.Helper;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.Management.KeyVault.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using System.Net.Http;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.Management.Network.Fluent.Models;
using AzureVM.Models;
using Renci.SshNet;
using System.Threading;

namespace AzureVM
{
    public static class CreateAzureVM
    {
        private static string clientId = Environment.GetEnvironmentVariable("ClientID");
        private static string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
        private static string tenantId = Environment.GetEnvironmentVariable("TenantID");
        


        [FunctionName("CreateAzureVM")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");


            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            VMDetails data = JsonConvert.DeserializeObject<VMDetails>(requestBody);

            string VMName = SdkContext.RandomResourceName(data.VirtualMachineName,2) ;
            string SubId = data.SubscriptionGuid;
            string RGName = data.ResourceGroup;
            string RGLocation = data.RGLocation;
            string VmUserName = data.UserName;
            string VmPassword = data.Password;
            int SecondaryIPCount = data.SecondaryIPConfigurationCount;            

            string VetName = SdkContext.RandomResourceName(VMName + "-vnet", 5);
            string publicIp = SdkContext.RandomResourceName(VMName + "-primary-", 5);
            string nsgName = SdkContext.RandomResourceName(VMName + "-nsg", 5);
            string nicName = SdkContext.RandomResourceName(VMName + "-nic", 5);
            string VMSize = VirtualMachineSizeTypes.StandardB1ms.ToString();

            try
            {
               
                var credentials = ADHelper.GetCredentials();
                IAzure azure = Azure.Configure()
                   .Authenticate(credentials).WithSubscription(SubId);

                log.LogInformation("Creating a new VNET...");
                INetwork network = CreateNetwork(RGName, RGLocation, VetName, azure);
                
                log.LogInformation("Creating a new PIP...");
                IPublicIPAddress pip = CreatePublicIp(RGName, RGLocation, publicIp, azure);

                log.LogInformation("Creating a new NSG...");
                INetworkSecurityGroup nsg = CreateNSG(RGName, RGLocation, nsgName, azure);

                log.LogInformation("Creating a new NIC...");
                INetworkInterface nic = CreateNIC(RGName, RGLocation, nicName, azure, network, pip, nsg);

                for (int i = 0; i <= SecondaryIPCount; i++)
                {
                    log.LogInformation("Updating a PIP...secondary-" + i);
                    CreateIPConfigurations(network, nic, i);

                }

                log.LogInformation("Creating a new VM...");
                var azureVM = CreateVM(VMName, RGName, VMSize, RGLocation, VmUserName, VmPassword, azure, nic);
                return new OkObjectResult(new { Message =string.Format("Successfully created VM: {0}!", azureVM.Id) });

            }
            catch (Exception ex)
            {

                log.LogInformation(ex.Message);
                return new BadRequestObjectResult(new { ErrorMessage=ex.Message });
            }            
        }


       


            private static void CreateIPConfigurations(INetwork network, INetworkInterface nic, int i)
        {
            nic.Update().DefineSecondaryIPConfiguration("secondary-" + i).WithExistingNetwork(network)
                                    .WithSubnet("default").WithPrivateIPAddressDynamic().WithNewPublicIPAddress().Attach().Apply();
        }

        private static IVirtualMachine CreateVM(string VMName, string RGName,string VMSize, string RGLocation, string VmUserName, string VmPassword, IAzure azure, INetworkInterface nic)
        {
            return azure.VirtualMachines.Define(VMName)
                                .WithRegion(RGLocation)
                                .WithNewResourceGroup(RGName).WithExistingPrimaryNetworkInterface(nic)
                                .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.CentOS7_2)
                                .WithRootUsername(VmUserName)
                                .WithRootPassword(VmPassword)
                                .WithSize(VMSize)
                                .Create();
        }

        private static INetworkInterface CreateNIC(string RGName, string RGLocation, string nicName, IAzure azure, INetwork network, IPublicIPAddress pip, INetworkSecurityGroup nsg)
        {
            return azure.NetworkInterfaces.Define(nicName)
                    .WithRegion(RGLocation)
                    .WithExistingResourceGroup(RGName)
                    .WithExistingPrimaryNetwork(network)
                    .WithSubnet("default")
                    .WithPrimaryPrivateIPAddressDynamic().WithExistingPrimaryPublicIPAddress(pip)
                    .WithExistingNetworkSecurityGroup(nsg)
                    .Create();
        }

        private static INetworkSecurityGroup CreateNSG(string RGName, string RGLocation, string nsgName, IAzure azure)
        {
            return azure.NetworkSecurityGroups.Define(nsgName)
                .WithRegion(RGLocation)
                .WithExistingResourceGroup(RGName)
                .DefineRule("Port_8080").AllowInbound().FromAnyAddress().FromAnyPort().ToAnyAddress().ToPort(9897).WithAnyProtocol()
                .WithPriority(350).Attach().DefineRule("HTTP").AllowInbound().FromAnyAddress().FromAnyPort().ToAnyAddress().ToPort(80).WithProtocol(SecurityRuleProtocol.Tcp)
                .WithPriority(320).Attach().DefineRule("HTTPS").AllowInbound().FromAnyAddress().FromAnyPort().ToAnyAddress().ToPort(443).WithProtocol(SecurityRuleProtocol.Tcp)
                .WithPriority(340).Attach().DefineRule("SSH").AllowInbound().FromAnyAddress().FromAnyPort().ToAnyAddress().ToPort(22).WithProtocol(SecurityRuleProtocol.Tcp)
                .WithPriority(300).Attach()
                .Create();
        }

        private static IPublicIPAddress CreatePublicIp(string RGName, string RGLocation, string publicIp, IAzure azure)
        {
            return azure.PublicIPAddresses
                .Define(publicIp)
                .WithRegion(RGLocation)
                .WithExistingResourceGroup(RGName)
                .WithDynamicIP()
                .Create();
        }

        private static INetwork CreateNetwork(string RGName, string RGLocation, string VetName, IAzure azure)
        {
            return azure.Networks.Define(VetName)
                   .WithRegion(RGLocation)
                   .WithNewResourceGroup(RGName)
                   .WithAddressSpace("10.0.1.0/24")
                   .DefineSubnet("default")
                   .WithAddressPrefix("10.0.1.0/24")
                   .Attach()
                   .Create();
        }
    }
}
