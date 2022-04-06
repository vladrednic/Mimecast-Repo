using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Automation {
    public class RowResult {
        public string From { get; set; }
        public string To { get; set; }
        public string Subject { get; set; }
        public float? Size { get; set; }
        public DateTime? Date { get; set; }
        public string Context { get; set; }
    }
}
