using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class Search {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Index(IsUnique = true)]
        [Index("unique", IsUnique = true, Order = 0)]
        [MaxLength(512)]
        public string Email { get; set; }

        [MaxLength(512), Index]
        public string Name { get; set; }

        public DateTime? CreatedDate { get; set; }

        [Index("unique", IsUnique = true, Order = 1)]
        public DateTime? BeginDate { get; set; }
        [Index("unique", IsUnique = true, Order = 2)]
        public DateTime? EndDate { get; set; }
        [ForeignKey(nameof(ExportFormat))]
        public ExportFormatCode ExportFormatId { get; set; }
        public ExportFormat ExportFormat { get; set; }
        public SearchStatusEnum SearchStatusId { get; set; }
        public List<SubSearch> SubSearches { get; set; }
        public long? ItemsCount { get; set; }

        public int? Priority { get; set; }
        public string Tag { get; set; }
        public bool JournalExtraction { get; set; }

        public void ParseLine(string[] fields) {
            Name = Email = fields[0];
            if (fields.Length > 1)
                BeginDate = DateTime.Parse(fields[1]);
            if (fields.Length > 2)
                EndDate = DateTime.Parse(fields[2]);
            if (fields.Length > 3) {
                //export format
                if (Enum.TryParse<ExportFormatCode>(fields[3], true, out ExportFormatCode code))
                    ExportFormatId = code;
            }
        }

        public void LoadFrom(SearchMC search, ExportFormatCode exportFormatCode) {
            this.BeginDate = search.BeginDate;
            this.Email = search.Email;
            this.EndDate = search.EndDate;
            this.ExportFormatId = exportFormatCode;
            this.Name = search.Name;
        }
    }
}
