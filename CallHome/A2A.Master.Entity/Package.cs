using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity {
    public class Package : EntityBase {
        public DateTime CreatedDate { get; set; }
        public string JsonData { get; set; }
        public int ProjectId { get; set; }
        public Project Project { get; set; }
    }
}
