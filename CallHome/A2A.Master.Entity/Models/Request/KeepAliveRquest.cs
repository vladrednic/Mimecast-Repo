using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity.Models.Requests {
    public class KeepAliveRquest : RequestBase {
        public KeepAliveRquest() : base(RequestType.KeepAlive) {
        }

        public int ProjectId { get; set; }
        public float? LastCompletedPercent { get; set; }
    }
}
