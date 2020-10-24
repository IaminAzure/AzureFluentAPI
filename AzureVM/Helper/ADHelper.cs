using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AzureVM.Helper
{
    class ADHelper
    {
        private static string clientId = Environment.GetEnvironmentVariable("ClientID");
        private static string clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
        private static string tenantId = Environment.GetEnvironmentVariable("TenantID");


        internal static AzureCredentials GetCredentials()
        { 
            return SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
        }

    }


   
}
