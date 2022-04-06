using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class SubSearch {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int SearchId { get; set; }
        public Search Search { get; set; }

        public SearchStatusEnum StatusId { get; set; }

        [MaxLength(512), Index]
        public string Name { get; set; }
        [MaxLength(512)]
        public string Email { get; set; }

        public DateTime? CreatedDate { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long? ItemsCount { get; set; }
        public DateTime? Reconciled { get; set; }

        public List<SubSearchFile> SubSearchFiles { get; set; }

        public void LoadFrom(Search search) {
            this.BeginDate = search.BeginDate;
            this.EndDate = search.EndDate;
            this.Name = search.Name;
            this.SearchId = search.Id;
            if (search.SearchStatusId == SearchStatusEnum.Success)
                this.StatusId = search.SearchStatusId;
        }

        public void LoadFrom(SubSearch subSrc) {
            this.BeginDate = subSrc.BeginDate;
            this.EndDate = subSrc.EndDate;
            this.Name = subSrc.Name;
            this.StatusId = subSrc.StatusId;
        }

        public void LoadFrom(SearchMC subSearch) {
            this.BeginDate = subSearch.BeginDate;
            this.Email = subSearch.Email;
            this.EndDate = subSearch.EndDate;
            this.Name = subSearch.Name;
        }

        public ExportMC GetExportMC() {
            var export = new ExportMC() {
                Email = this.Email,
                ItemsCount = this.ItemsCount,
                Name = this.Name,
                SubSearchId = this.Id
            };
            return export;
        }
    }
}
