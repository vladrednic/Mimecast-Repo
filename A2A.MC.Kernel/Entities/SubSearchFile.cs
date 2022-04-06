using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class SubSearchFile : ICloneable {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public SubSearch SubSearch { get; set; }
        public int? SubSearchId { get; set; }
        public DateTime DiscoveredDate { get; set; }
        public string DownloadPath { get; set; }
        public DateTime? DownloadDate { get; set; }
        public string McOriginalFileName { get; set; }
        public int? McBatchNumber { get; set; }
        public DateTime? McCreateTime { get; set; }
        public DateTime? McExpiryDate { get; set; }
        public int? McNumberOfMessages { get; set; }
        public int? McFailedMessages { get; set; }
        //approximation because Mc shows the size in different units: KB, MB, GB...
        public long? McFileSizeBytesApprox { get; set; }

        public bool DownloadError { get; set; }

        [NotMapped]
        public object Tag { get; set; }

        public object Clone() {
            return this.MemberwiseClone();
        }
    }
}
