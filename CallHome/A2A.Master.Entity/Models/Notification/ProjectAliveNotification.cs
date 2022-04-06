using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity.Models.Notification {
    public class ProjectAliveNotification {
        public Project Project { get; set; }
        public Customer Customer { get; set; }
        public float? LastCompletedPercent { get; set; }
    }
}
