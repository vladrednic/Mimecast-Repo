using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace A2A.BasicCore.Service {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args) {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new AutomationService()
            };

#if DEBUG
            if (Environment.UserInteractive) {
                const BindingFlags bindingFlags =
                    BindingFlags.Instance | BindingFlags.NonPublic;

                foreach (var serviceBase in ServicesToRun) {
                    var serviceType = serviceBase.GetType();
                    var methodInfo = serviceType.GetMethod("OnStart", bindingFlags);

                    new Thread(service => methodInfo.Invoke(service, new object[] { args })).Start(serviceBase);
                }

                return;
            }
#endif
            ServiceBase.Run(ServicesToRun);
        }
    }
}
