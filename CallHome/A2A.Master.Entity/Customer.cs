using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity {
    public class Customer : AuditedEntityBase {
        public string Name { get; set; }
    }
}
