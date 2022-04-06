using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class EntityBase : IExportEntity {
        public string Name { get; set; }
        public string Email { get; set; }

        public virtual void ParseLine(string[] fields) {
            Email = fields[0].Trim();
            if (fields.Length > 2)
                Name = fields[1];
            else
                Name = Email;
        }

        public static string GetHeader() {
            var s = $"=== Email Address ===\tStatus\tMessages";
            return s;
        }
    }
}
