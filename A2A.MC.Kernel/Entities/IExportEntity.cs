using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public interface IExportEntity {
        string Name { get; set; }
        void ParseLine(string[] fields);
    }
}
