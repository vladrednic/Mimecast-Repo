using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Kernel.Entities {
    public class SearchMC : EntityBase {
        public ExportStatus ExportStatus { get; set; }
        public string Search { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Start { get; set; }
        public string End { get; set; }

        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long? ItemsCount { get; set; }
        private static readonly Random _random = new Random();

        public bool JournalExtraction { get; set; }

        public object Clone() {
            return MemberwiseClone();
        }

        public override void ParseLine(string[] fields) {
            base.ParseLine(fields);
            if (fields.Length < 2) {
                return;
            }

            ExportStatus = ExportStatus.SearchPending;
            if (Enum.TryParse<ExportStatus>(fields[fields.Length - 1], out ExportStatus status))
                ExportStatus = status;
        }
        public override string ToString() {
            var s = $"{Name}\t{ExportStatus}";
            return s;
        }

        public bool SetName() {
            if (JournalExtraction)
                return true;
            if (SetSimpleName())
                return true;
            Name = GetRandomName();
            return true;
        }

        private bool SetSimpleName() {
            if (string.IsNullOrEmpty(Email))
                return false;
            if (string.IsNullOrEmpty(Name))
                Name = Email;

            if (string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Email))
                return false;

            if (!BeginDate.HasValue || !EndDate.HasValue) {
                return false;
            }

            Name = $"{Email}_{BeginDate.Value:yyyy-MM-dd-HH:mm}_{EndDate.Value:yyyy-MM-dd-HH:mm}";
            //Mimecast only supports export names up to 60 characters
            if (Name.Length > 60) {
                Name = $"{Email}_{BeginDate.Value:yyyyMMddHHmm}_{EndDate.Value:yyyyMMddHHmm}";
                if (Name.Length > 60) {
                    Name = $"{Email}_{BeginDate.Value:yyyyMMddHHmm}";
                    if (Name.Length >= 60) {
                        return false;
                    }
                }
            }
            return true;
        }

        public static string RandomString(int length) {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[_random.Next(s.Length)]).ToArray());
        }

        private string GetRandomName() {
            var desiredName = string.Empty;
            if (!string.IsNullOrEmpty(Email)) {
                desiredName = $"{Email}_{BeginDate.Value:yyyy-MM-dd-HH:mm}_{EndDate.Value:yyyy-MM-dd-HH:mm}";
            }
            else {
                desiredName = $"{BeginDate.Value:yyyy-MM-dd-HH:mm}_{EndDate.Value:yyyy-MM-dd-HH:mm}";
            }
            var randomLength = 60 - desiredName.Length;
            if (randomLength > 0) {
                return desiredName;
            }
            randomLength = 60 - Email.Length;
            if (randomLength <= 0)
                throw new ApplicationException($"Cannot create subsearch for email {Email}. Too long: {Email.Length}");
            var strRandom = RandomString(randomLength);
            desiredName = $"{Email}{strRandom}";
            return desiredName;
        }

        public void Fill(SubSearch subSearch) {
            subSearch.BeginDate = this.BeginDate;
            subSearch.Email = this.Email;
            subSearch.EndDate = this.EndDate;
            subSearch.Name = this.Name;
            subSearch.ItemsCount = this.ItemsCount;
            if (subSearch.Search != null) {
                JournalExtraction = subSearch.Search.JournalExtraction;
            }
        }

        public void Fill(Search search, ExportFormatCode exportFormat) {
            search.BeginDate = this.BeginDate;
            search.Email = this.Email;
            search.EndDate = this.EndDate;
            search.Name = this.Name;
            search.ExportFormatId = exportFormat;
            search.ItemsCount = this.ItemsCount;
        }
        public void Fill(SubSearch subSearch, ExportFormat exportFormat, int? parentSearchId) {
            subSearch.BeginDate = this.BeginDate;
            //search.eMain = this.Email;
            subSearch.EndDate = this.EndDate;
            subSearch.Name = this.Name;
            subSearch.Email = this.Email;
            subSearch.ItemsCount = this.ItemsCount;
            if (parentSearchId.HasValue)
                subSearch.SearchId = parentSearchId.Value;
            //search.ExportFormat = exportFormat;
        }
    }
}
