using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.ExcelReporter {
    public class RecordData {
        public DateTime Date { get; set; }
        public string Custodian { get; set; }
        public int? SearchItems { get; set; }
        public int? ExtractedItems { get; set; }
        public long FileSize { get; set; }
        public long? DataSize { get; set; }
    }
}
