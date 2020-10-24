using System;
using System.Collections.Generic;
using System.Text;

namespace AzureVM.Models
{
    class VMDetails
    {

        public string VirtualMachineName { get; set; }
        public string SubscriptionGuid { get; set; }
        public string ResourceGroup { get; set; }
        public string RGLocation { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public int SecondaryIPConfigurationCount { get; set; }

    }
}
