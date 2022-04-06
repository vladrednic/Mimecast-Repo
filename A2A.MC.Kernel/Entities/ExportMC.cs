using A2A.MC.Kernel.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class ExportMC : EntityBase, ICloneable {
        public ExportStatus ExportStatus { get; set; }
        public string CreatedBy { get; set; }
        public string Type { get; set; }
        public string Created { get; set; }
        public string Prepared { get; set; }
        public string Messages { get; set; }
        public string Completed { get; set; }
        public string Remaining { get; set; }
        public int Index { get; set; }
        public int? SubSearchId { get; set; }

        public ExportFormatCode ExportFormat { get; set; }

        public int EstimatedExportsCount {
            get {
                var msg = string.IsNullOrEmpty(Messages) ? "0" : Messages;
                var messages = (decimal)int.Parse(msg);
                var exports = messages / MaxMessagesPerExport;
                var n = (int)Math.Ceiling(exports);
                //if (n < 1) n = 1;
                return n;
            }
        }

        public static int MaxMessagesPerExport { get { return 100000; } }

        public long? ItemsCount { get; set; }

        public static readonly ExportStatus[] ExportsRunningStatuses = new ExportStatus[] {
                ExportStatus.ActiveExport,
                ExportStatus.Downloadable,
                ExportStatus.Preparing,
                ExportStatus.PreparationPending,
                ExportStatus.ExportCreated
            };

        public override void ParseLine(string[] fields) {
            base.ParseLine(fields);
            if (fields.Length > 1) {
                ExportStatus = ExportStatus.SearchPending;
                ExportStatus status;
                if (Enum.TryParse<ExportStatus>(fields[1], out status))
                    ExportStatus = status;
            }
            if (fields.Length > 2) {
                this.Messages = fields[2];
            }
        }

        public override string ToString() {
            var s = $"{Name}\t{ExportStatus}\t{Messages}";
            return s;
        }

        public bool IsRunning() {
            return ExportMC.ExportsRunningStatuses.Contains(this.ExportStatus);
        }

        public bool IsCompleted() {
            return this.ExportStatus == ExportStatus.ExportCompleted;
        }

        public bool Equals(ExportMC obj) {
            if (obj == null) return false;
            //var equal = this.Index == obj.Index
            //    && this.Name == obj.Name;

            var equal = (this.Name ?? "").ToLower() == (obj.Name ?? "").ToLower()
                && this.ExportStatus == obj.ExportStatus
                && this.Messages == obj.Messages
                && this.Created == obj.Created
                && this.CreatedBy == obj.CreatedBy
                && this.Prepared == obj.Prepared
                && this.Remaining == obj.Remaining;
            return equal;
        }

        public object Clone() {
            return MemberwiseClone();
        }

        public ExportMC CloneExport() {
            return Clone() as ExportMC;
        }

        public void LoadFrom(Search search) {
            this.ExportFormat = search.ExportFormatId;
            ExportStatus? status = null;
            switch (search.SearchStatusId) {
                case SearchStatusEnum.ExportCreated:
                    status = ExportStatus.ExportCreated;
                    break;
                case SearchStatusEnum.Pending:
                    status = ExportStatus.SearchPending;
                    break;
                case SearchStatusEnum.SearchCreated:
                    status = ExportStatus.SearchCreated;
                    break;
                case SearchStatusEnum.Success:
                    status = ExportStatus.ExportCompleted;
                    break;
                default:
                    break;
            }
            if (status.HasValue)
                this.ExportStatus = status.Value;
            this.ItemsCount = search.ItemsCount;
            this.Name = search.Name;
            this.Email = search.Email;
        }

        public void LoadFrom(SubSearch subSearch) {
            this.ExportFormat = subSearch.Search.ExportFormatId;
            ExportStatus? status = null;
            switch (subSearch.Search.SearchStatusId) {
                case SearchStatusEnum.ExportCreated:
                    status = ExportStatus.ExportCreated;
                    break;
                case SearchStatusEnum.Pending:
                    status = ExportStatus.SearchPending;
                    break;
                case SearchStatusEnum.SearchCreated:
                    status = ExportStatus.SearchCreated;
                    break;
                case SearchStatusEnum.Success:
                    status = ExportStatus.ExportCompleted;
                    break;
                default:
                    break;
            }
            if (status.HasValue)
                this.ExportStatus = status.Value;
            this.ItemsCount = subSearch.ItemsCount;
            this.Name = subSearch.Name;
            this.Email = subSearch.Email;
            this.SubSearchId = subSearch.Id;
        }

        public int CompareTo(ExportMC exportDetails) {
            var r = this.Name.ToLower().CompareTo(exportDetails.Name.ToLower());
            if (r != 0)
                return r;

            if (this.ExportFormat != exportDetails.ExportFormat)
                return 1;

            return (int)(this.ItemsCount.Value - exportDetails.ItemsCount.Value);
        }
    }
}
