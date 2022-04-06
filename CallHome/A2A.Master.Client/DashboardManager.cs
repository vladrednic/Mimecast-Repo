using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using A2A.Master.Entity.Models;
using A2A.Master.Entity.Models.Notification;

namespace A2A.Master.Client {
    public class DashboardManager : ManagerBase {
        public DashboardManager() {
            DashboardDataAvailable += dummy_DashboardDataAvailable;
            ProjectNotificationReceived += dummy_ProjectNotificationReceived;
        }

        public DashboardData DashboardData { get; private set; }

        public event EventHandler DashboardDataAvailable;
        public event EventHandler ProjectNotificationReceived;

        public ProjectAliveNotification LastProjectNotification { get; set; }

        private void dummy_DashboardDataAvailable(object sender, EventArgs e) { }
        private void dummy_ProjectNotificationReceived(object sender, EventArgs e) { }

        public override bool Init(string url = "http://a2amaster.azurewebsites.net", string hubName = "ChatHub") {
            var connected = base.Init(url, hubName);

            if (connected) {
                Proxy.On<DashboardData>("dashboardDataReady", (data) => {
                    DashboardData = data;
                    DashboardDataAvailable(this, EventArgs.Empty);
                });

                Proxy.On<ProjectAliveNotification>("broadcastKeepAlive", (notif) => {
                    LastProjectNotification = notif;
                    ProjectNotificationReceived(this, EventArgs.Empty);
                });
            }

            return connected;
        }

        public void RequestDashboardData() {
            Proxy.Invoke("getDashboardData");
        }
    }
}
