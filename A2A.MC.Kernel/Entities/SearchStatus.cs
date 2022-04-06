using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class SearchStatus {
        [Key]
        public SearchStatusEnum Id { get; set; }

        [MaxLength(20)]
        public string StatusCode { get; set; }
    }
}
