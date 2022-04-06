using A2A.Master.Client.Common;
using A2A.Master.Entity;
using A2A.Master.Entity.Models;
using A2A.Master.Entity.Models.Requests;
using A2A.Master.Entity.Models.Response;
using Microsoft.AspNet.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Client {
    public class ProjectManager : ManagerBase {

        public void SendKeepAlive(float? lastCompletedPercent) {
            if (Context.ProjectResponse == null) {
                RegisterProject();
            }
            if (Context.ProjectResponse == null) {
                return;
            }
            var req = new KeepAliveRquest() {
                ProjectId = Context.ProjectResponse.ProjectId,
                RequestType = RequestType.KeepAlive,
                LastCompletedPercent = lastCompletedPercent
            };
            Proxy.Invoke<KeepAliveRquest>("KeepAlive", req);
        }

        public void RegisterProject(string customerName = null, string projectName = null, string projectTypeName = null) {
            if (string.IsNullOrEmpty(customerName))
                customerName = Context.CustomerName;
            if (string.IsNullOrEmpty(projectName))
                projectName = Context.ProjectName;
            if (string.IsNullOrEmpty(projectTypeName))
                projectTypeName = Context.ProjectTypeName;

            var req = new RegisterProjectRequest() {
                CustomerName = customerName,
                ProjectName = projectName,
                ProjectTypeName = projectTypeName
            };
            Proxy.Invoke<RegisterProjectRequest>("RegisterProject", req).ContinueWith(task => {
                if (task.IsFaulted) {
                    Console.WriteLine("There was an error calling send: {0}",
                                      task.Exception.GetBaseException());
                }
                else {
                    Console.WriteLine(task.Result);
                }
            }).Wait();
        }

        public void SendPackage(Package package) {
            var req = new RegisterProjectRequest();
            req.Package = package;
            req.CustomerName = "Kiewit";
            req.ProjectName = "Mimecast Extraction";
            req.ProjectTypeName = "MimecastExtraction";
            Proxy.Invoke<RegisterProjectRequest>("RegisterProject", req).ContinueWith(task => {
                if (task.IsFaulted) {
                    Console.WriteLine("There was an error calling send: {0}",
                                      task.Exception.GetBaseException());
                }
                else {
                    Console.WriteLine(task.Result);
                }
            }).Wait();
        }
    }
}