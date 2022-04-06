using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity {
    public abstract class AuditedEntityBase : EntityBase{
        public DateTime CreatedDateUtc { get; set; }
        public DateTime? LastModifiedUtc { get; set; }
    }
}
