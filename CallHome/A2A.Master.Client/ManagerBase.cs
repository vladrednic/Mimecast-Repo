using A2A.Master.Client.Common;
using A2A.Master.Entity.Models.Response;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Client {
    public abstract class ManagerBase {
        public HubConnection Connection { get; private set; }
        public IHubProxy Proxy { get; private set; }

        public virtual bool Init(string url = "http://a2amaster.azurewebsites.net", string hubName = "ChatHub") {
            var t = typeof(Newtonsoft.Json.MemberSerialization); //resolves Newtonsoft dependency issue
            Connection = new HubConnection(url);
            Proxy = Connection.CreateHubProxy(hubName);

            var connected = false;

            Connection.Start().ContinueWith(task => {
                if (task.IsFaulted) {
                    Console.WriteLine("There was an error opening the connection:{0}",
                                      task.Exception.GetBaseException());
                }
                else {
                    Console.WriteLine("Connected");
                    connected = true;
                }
            }).Wait();

            if (connected) {
                Proxy.On<ProjectResponse>("projectData", (projResponse) => {
                    Context.ProjectResponse = projResponse;
                });
            }

            return connected;
        }
    }
}
