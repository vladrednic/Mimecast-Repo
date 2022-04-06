using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Notifications.Email {
    public class SmtpEmailNotifyConfig: INotifyConfig {
        public string From { get; set; } = "a360com@outlook.com";
        public string To { get; set; }

        public int Port { get; set; } = 25;
        public string Host { get; set; } = "smtp.live.com";
        public string Subject { get; set; } = "A360 new notification";
        public string Body { get; set; } = "Hello, this is a new notification from A360 team";

        public string UserName { get; set; } = "a360com@outlook.com";
        public string Password { get; set; } = "WelcomeA360!";
        public bool EnableSsl { get; set; } = true;
    }
}
