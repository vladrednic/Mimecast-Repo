using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity {
    public class Project : AuditedEntityBase {
        public int CustomerId { get; set; }
        public Customer Customer { get; set; }
        public string Name { get; set; }

        public int ProjectType { get; set; }

        public DateTime? LastClientOnlineUtc { get; set; }
        public DateTime? LastServerOnlineUtc { get; set; }
        public float? LastCompletedPercent { get; set; }
    }
}
