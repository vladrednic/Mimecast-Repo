using A2A.Master.Entity.Models.Response;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Client.Common {
    public static class Context {
        static Context() {
            var t = typeof(HubConnection);
        }
        public static ProjectResponse ProjectResponse { get; set; }
        public static string CustomerName { get; set; }
        public static string ProjectName { get; set; }
        public static string ProjectTypeName { get; set; }
    }
}
