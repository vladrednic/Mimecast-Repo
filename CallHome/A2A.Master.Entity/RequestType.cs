using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Master.Entity {
    public enum RequestType {
        KeepAlive = 0,
        SingleData = 1,
        RangeData = 2,
        RegisterProject = 3
    }
}
