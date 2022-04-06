using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity.Models.Requests {
    public abstract class RequestBase {
        public RequestBase(RequestType requestType) {
            RequestDateUtc = DateTime.UtcNow;
            RequestType = requestType;
        }
        public RequestType RequestType { get; set; }
        public DateTime RequestDateUtc { get; set; }
        public Package Package { get; set; }
    }
}
