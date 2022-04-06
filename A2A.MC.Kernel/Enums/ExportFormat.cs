using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class ExportFormat {
        [Key]
        public ExportFormatCode Code { get; set; }

        [MaxLength(50)]
        public string Description { get; set; }
    }
}
