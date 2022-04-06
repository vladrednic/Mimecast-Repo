using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity.Models.Requests {
    public class RegisterProjectRequest : RequestBase {
        public RegisterProjectRequest() : base(RequestType.RegisterProject) { }
        public string CustomerName { get; set; }
        public string ProjectName { get; set; }
        public string ProjectTypeName { get; set; }
    }
}
