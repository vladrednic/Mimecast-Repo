using A2A.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Notifications.Email {
    public class SmtpEmailNotify : NotifyBase<SmtpEmailNotifyConfig> {
        public SmtpEmailNotify(SmtpEmailNotifyConfig config) : base(config) {
        }

        protected override void SendInternal() {
            SmtpClient client = new SmtpClient();
            client.Port = Config.Port;
            client.Port = 587;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Host = Config.Host;
            client.Credentials = new NetworkCredential(Config.UserName, Config.Password);
            client.EnableSsl = Config.EnableSsl;

            MailMessage mail = new MailMessage();
            mail.From = new MailAddress(Config.From);
            foreach (var email in Config.To.Split(',', ';')) {
                mail.To.Add(new MailAddress(email.Trim()));
            }
            mail.Subject = Config.Subject;
            mail.Body = Config.Body;
            client.Send(mail);
        }
    }
}
