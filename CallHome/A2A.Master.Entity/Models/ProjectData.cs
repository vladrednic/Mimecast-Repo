using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity.Models {
    public class ProjectData {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public string CustomerName { get; set; }
        public DateTime? LastOnlineUtc { get; set; }

        public float? LastCompletedPercent { get; set; }
    }
}
