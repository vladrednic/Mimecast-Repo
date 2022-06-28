using System.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using A2A.MC.Kernel.Entities;
using A2A.Logger;
using A2A.MC.Kernel;

namespace A2A.MC.Data {
    public class DbManager {
        public int TotalProcessed { get; private set; }
        public int TotalInserted { get; private set; }
        public int TotalDuplicates { get; private set; }
        public int TotalSearches { get; private set; }
        public event EventHandler LogNeeded;

        private static readonly SearchStatusEnum[] _searchStatusToExport = new SearchStatusEnum[] {
            SearchStatusEnum.SearchCreated
        };

        public DbManager() {
            SearchAddProgress += DbManager_SearchAdded;
            LogNeeded += DbManager_LogNeeded;
        }

        private void DbManager_LogNeeded(object sender, EventArgs e) { }

        public ILogProvider LogProvider { get; set; }
        public int BatchDuplicates { get; private set; }
        public int BatchInserted { get; private set; }

        private void DbManager_SearchAdded(object sender, EventArgs e) { }

        private McDbContext GetDbContext() {
            return DbFactory.GetDbContext();
        }

        public event EventHandler SearchAddProgress;

        public void AddSearch(Search dbSearch) {
            using (var db = GetDbContext()) {
                var exists = db.Search.Any(s => s.Email == dbSearch.Email);
                if (exists)
                    return;
                dbSearch.CreatedDate = DateTime.Now;
                db.Search.Add(dbSearch);
                try {
                    db.SaveChanges();
                    db.SaveChanges();
                }
                catch { }
            }
        }

        public void UpdateSearch(Search search) {
            using (var db = GetDbContext()) {
                var src = db.Search
                    .Include(s => s.SubSearches)
                    .Where(s => s.Email == search.Email)
                    .FirstOrDefault();
                if (src == null)
                    return;

                src.SearchStatusId = search.SearchStatusId;
                src.Name = search.Name;
                src.Email = search.Email;
                src.SubSearches.ForEach(
                    sub => {
                        sub.LoadFrom(src);
                    });
                db.SaveChanges();
            }
        }

        public Search GetSearchByEmail(string email) {
            using (var db = GetDbContext()) {
                var search = db.Search.FirstOrDefault(s => s.Email == email);
                return search;
            }
        }

        public SubSearch GetSubSearchByEmail(string email, DateTime? beginDate, DateTime? endDate, McDbContext db = null) {
            bool release = false;
            if (db == null) {
                db = GetDbContext();
                release = true;
            }
            try {
                var subSearch = db.SubSearch
                    .AsNoTracking()
                    .FirstOrDefault(e => e.Email == email && e.BeginDate == beginDate && e.EndDate == endDate);
                return subSearch;
            }
            finally {
                if (release)
                    db.Dispose();
            }
        }

        private DateTime? EliminateSeconds(DateTime? dt) {
            if (!dt.HasValue)
                return dt;
            dt = new DateTime(dt.Value.Year, dt.Value.Month, dt.Value.Day, dt.Value.Hour, dt.Value.Minute, 0);
            return dt;
        }

        public SubSearch GetSubSearchByName(string name, DateTime? beginDate, DateTime? endDate, McDbContext db = null) {
            bool release = false;
            if (db == null) {
                db = GetDbContext();
                release = true;
            }
            try {
                var q = db.SubSearch
                    .Where(e => e.Name == name);
                if (beginDate.HasValue) {
                    var endIntervalDate = beginDate.Value.AddSeconds(59);
                    q = q.Where(ss => ss.BeginDate >= beginDate.Value).Where(ss => ss.BeginDate <= endIntervalDate);
                }
                if (endDate.HasValue) {
                    var endIntervalDate = endDate.Value.AddSeconds(59);
                    q = q.Where(ss => ss.EndDate >= endDate.Value).Where(ss => ss.EndDate <= endIntervalDate);
                }

                var subSearch = q.FirstOrDefault();
                return subSearch;
            }
            finally {
                if (release)
                    db.Dispose();
            }
        }

        public SubSearch SubSearchExists(SearchMC subSearch, McDbContext db = null) {
            return GetSubSearchByEmail(subSearch.Email, subSearch.BeginDate, subSearch.EndDate, db);
        }

        public SubSearch InsertSubSearchIfNotExists(SearchMC subSearch, int parentId) {
            using (var db = GetDbContext()) {
                var s = SubSearchExists(subSearch, db);
                if (s == null) {
                    s = new SubSearch() {
                        CreatedDate = DateTime.Now,
                        SearchId = parentId
                    };
                    s.LoadFrom(subSearch);
                }
                db.SaveChanges();
                return s;
            }
        }

        public SubSearch InsertIfNotExists(SubSearch subSearch) {
            using (var db = GetDbContext()) {
                var sub = db.SubSearch
                    .FirstOrDefault(
                        s => s.Id == subSearch.Id ||
                        (s.Search.Email == subSearch.Email && s.BeginDate == subSearch.BeginDate && s.EndDate == subSearch.EndDate)
                    );
                if (sub == null) {
                    subSearch.CreatedDate = DateTime.Now;
                    _ = db.SubSearch.Add(subSearch);
                    db.SaveChanges();
                    sub = subSearch;
                }
                db.SaveChanges();
                return sub;
            }
        }

        public SubSearch InsertOrUpdate(SubSearch subSearch, int? parentSearchId) {
            if (parentSearchId.HasValue)
                subSearch.SearchId = parentSearchId.Value;

            using (var db = GetDbContext()) {
                var existingSubsearch = db
                    .SubSearch
                    .FirstOrDefault(s =>
                        s.Email == subSearch.Email && s.BeginDate == subSearch.BeginDate && s.EndDate == subSearch.EndDate
                    );
                if (existingSubsearch == null) {
                    existingSubsearch = db.SubSearch.Add(subSearch);
                }
                else {
                    existingSubsearch.LoadFrom(subSearch);
                }
                if (!subSearch.CreatedDate.HasValue)
                    subSearch.CreatedDate = DateTime.Now;
                db.SaveChanges();
                return existingSubsearch;
            }
        }

        public int ResetSubSearchReconciledFlag() {
            using (var db = GetDbContext()) {
                db.SubSearch.ToList().ForEach(ss => ss.Reconciled = null);
                return db.SaveChanges();
            }
        }

        public int AddSearches(List<Search> searches, int progressStep = 100) {
            BatchInserted = 0;
            BatchDuplicates = 0;
            TotalProcessed = 0;
            TotalInserted = 0;
            TotalDuplicates = 0;

            TotalSearches = searches.Count;
            if (searches == null)
                return 0;

            using (var db = GetDbContext()) {
                foreach (var search in searches) {
                    if (search.EndDate.HasValue) {
                        //adding time to include the current day
                        var dt = search.EndDate.Value;
                        if (dt == dt.Date && !search.Email.StartsWith("(ALL)")) {
                            dt = dt.Date.AddDays(1).AddSeconds(-1);
                        }
                        search.EndDate = dt;
                    }
                    if (SearchExists(search, db) == null) {
                        if (!search.CreatedDate.HasValue)
                            search.CreatedDate = DateTime.Now;
                        db.Search.Add(search);
                        var affected = db.SaveChanges();
                        BatchInserted += affected;
                        TotalInserted += affected;
                    }
                    else {
                        BatchDuplicates++;
                        TotalDuplicates++;
                    }
                    TotalProcessed++;
                    if ((TotalProcessed % progressStep) == 0) {
                        SearchAddProgress(this, EventArgs.Empty);
                        BatchInserted = 0;
                        BatchDuplicates = 0;
                    }
                }
                return db.SaveChanges();
            }
        }

        public SubSearch MarkSubSearchReconciled(SubSearch subSearch) {
            using (var db = GetDbContext()) {
                var ss = GetSubSearchByName(subSearch, db);
                if (ss == null)
                    return null;
                ss.Reconciled = DateTime.Now;
                db.SaveChanges();
                return ss;
            }
        }

        private SubSearch GetSubSearchByName(SubSearch subSearch, McDbContext db = null) {
            return GetSubSearchByName(subSearch.Name, subSearch.BeginDate, subSearch.EndDate, db);
        }

        public IEnumerable<ExportMC> GetExports() {
            using (var db = GetDbContext()) {
                var q = db.SubSearch
                    .Where(s => s.Search.SearchStatusId == SearchStatusEnum.SearchCreated)
                    //.Where(s => s.StatusId == SearchStatusEnum.SearchCreated);
                    ;
                var list = q
                    .Select(s =>
                        new ExportMC {
                            Email = s.Email,
                            Name = s.Name,
                            ExportFormat = s.Search.ExportFormatId
                        })
                    .ToList();
                return list;
            }
        }

        public void SetSubSearchStatus(string searchName, SearchStatusEnum? status, out Search parentSearch) {
            SubSearch subSearch = null;
            using (var db = GetDbContext()) {
                if (!status.HasValue) {
                    var succes = db.SubSearchFile
                        .Where(ssf => ssf.SubSearch.Name == searchName)
                        .All(ssf => !ssf.DownloadError && ssf.DownloadDate.HasValue);
                    if (succes)
                        status = SearchStatusEnum.Success;
                    else {
                        var exportFailed = db.SubSearchFile
                            .Where(ssf => ssf.SubSearch.Name == searchName)
                            .Any(ssf => ssf.DownloadError);
                        status = SearchStatusEnum.DownloadFailed;
                    }
                }
                subSearch = db.SubSearch.FirstOrDefault(s => s.Name == searchName);
                if (status.HasValue) {
                    if (subSearch != null) {
                        subSearch.StatusId = status.Value;
                        db.SaveChanges();
                    }
                }
            }
            parentSearch = null;
            if (subSearch != null) {
                parentSearch = SetSearchStatusSuccess(subSearch.SearchId);
            }
        }

        /// <summary>
        /// check if the parent search is completely downloaded or not and set it accordingly
        /// </summary>
        /// <param name="searchId"></param>
        private Search SetSearchStatusSuccess(int searchId) {
            using (var db = GetDbContext()) {
                var search = db.Search
                    .Include(s => s.SubSearches)
                    .Where(s => s.Id == searchId)
                    .FirstOrDefault();

                if (search == null)
                    return null;

                var success = true;
                foreach (var subSearch in search.SubSearches) {
                    if (subSearch.StatusId != SearchStatusEnum.Success) {
                        success = false;
                        break;
                    }
                }
                if (success) {
                    search.SearchStatusId = SearchStatusEnum.Success;
                    db.SaveChanges();
                }
                else {
                    var allSubsearchesLaunched = search.SubSearches.All(s => s.StatusId == SearchStatusEnum.ExportCreated);
                    if (allSubsearchesLaunched) {
                        search.SearchStatusId = SearchStatusEnum.ExportCreated;
                        db.SaveChanges();
                    }
                }
                return search;
            }
        }

        public List<string> GetLaunchedSearches() {
            using (var db = GetDbContext()) {
                var q = db.SubSearch
                    .Where(s => s.StatusId == SearchStatusEnum.SearchCreated);
                var list = q
                    .Select(s => s.Name)
                    .ToList();
                return list;
            }
        }

        private Search SearchExists(Search search, McDbContext db = null) {
            return SearchExists(search.Email, search.BeginDate, search.EndDate, db);
        }

        public void UpdateSubSearch(SubSearch subSrc, int? parentSearchId) {
            SubSearch subSearch;

            using (var db = GetDbContext()) {
                subSearch = db.SubSearch.FirstOrDefault(s => s.Id == subSrc.Id);
                if (subSearch == null)
                    return;
                if (parentSearchId.HasValue)
                    subSearch.SearchId = parentSearchId.Value;
                subSearch.LoadFrom(subSrc);
                db.SaveChanges();
            }
        }

        public Search InsertSearchIfNotExists(SearchMC search, ExportFormatCode exportFormatCode) {
            using (var db = GetDbContext()) {
                var s = SearchExists(search, db);
                if (s == null) {
                    s = new Search() {
                        CreatedDate = DateTime.Now
                    };
                    s.LoadFrom(search, exportFormatCode);
                }
                return s;
            }
        }

        public Search SearchExists(string email, DateTime? beginDate, DateTime? endDate, McDbContext db = null) {
            bool release = false;
            if (db == null) {
                db = GetDbContext();
                release = true;
            }
            try {
                var search = db.Search
                    //.AsNoTracking()
                    .FirstOrDefault(e => e.Email == email && e.BeginDate == beginDate && e.EndDate == endDate);
                return search;
            }
            finally {
                if (release)
                    db.Dispose();
            }
        }

        private Search SearchExists(SearchMC subSearch, McDbContext db = null) {
            return SearchExists(subSearch.Email, subSearch.BeginDate, subSearch.EndDate, db);
        }

        public bool UpdateSearchItemsCount(int? parentSearchId, long? itemsCount) {
            if (!parentSearchId.HasValue)
                return false;
            using (var db = GetDbContext()) {
                var search = db.Search.FirstOrDefault(s => s.Id == parentSearchId.Value);
                if (search == null)
                    return false;
                if (!search.ItemsCount.HasValue) {
                    search.ItemsCount = itemsCount;
                    db.SaveChanges();
                }
            }
            return true;
        }

        public Search SetStatus(int searchId, SearchStatusEnum searchStatus) {
            using (var db = GetDbContext()) {
                var search = db.Search.FirstOrDefault(s => s.Id == searchId);
                if (search == null) return null;
                search.SearchStatusId = searchStatus;
                db.SaveChanges();
                return search;
            }
        }

        public List<SearchMC> GetSearchesToCreate(string filterByTag = null) {
            var statuses = new SearchStatusEnum[] {
                SearchStatusEnum.Pending,
                SearchStatusEnum.CreateSearchFailed
            };
            using (var db = GetDbContext()) {
                var q = db.Search
                    .Where(s => statuses.Contains(s.SearchStatusId));
                if (!string.IsNullOrEmpty(filterByTag)) {
                    q = q.Where(s => s.Tag == filterByTag);
                }
                var searches = q
                    .OrderBy(s => s.Priority ?? 1000)
                    .Select(s => new SearchMC() {
                        BeginDate = s.BeginDate,
                        Email = s.Email,
                        EndDate = s.EndDate,
                        ItemsCount = s.ItemsCount,
                        Name = s.Name
                    })
                    .ToList();
                return searches;
            }
        }

        public void ResetSearch(int searchId, bool deleteSubSearches) {
            using (var db = GetDbContext()) {
                var search = db.Search
                    .Include(s => s.SubSearches)
                    .FirstOrDefault(s => s.Id == searchId);
                if (search == null)
                    throw new ApplicationException($"Search not found for id: {searchId}");

                var subSearches = search.SubSearches.ToList();

                foreach (var subSearch in subSearches) {
                    ResetSubSearch(subSearch.Id, true);
                    if (deleteSubSearches) {
                        db.SubSearch.Remove(subSearch);
                    }
                    else {
                        subSearch.StatusId = SearchStatusEnum.ExportCreated;
                    }
                    db.SaveChanges();
                }
                search.SearchStatusId = SearchStatusEnum.Pending;

                db.SaveChanges();
            }
        }

        private void ResetSubSearch(int subSearchId, bool deleteSubSearchFiles, McDbContext db = null) {
            bool disposeDb = false;
            if (db == null) {
                db = GetDbContext();
                disposeDb = true;
            }
            try {
                var subSearch = db.SubSearch
                    .Include(ss => ss.SubSearchFiles)
                    .FirstOrDefault(ss => ss.Id == subSearchId);
                if (subSearch == null)
                    throw new ApplicationException($"SubSearch not found for id: {subSearchId}");

                if (deleteSubSearchFiles) {
                    subSearch.SubSearchFiles.Clear();
                }
                else {
                    subSearch.SubSearchFiles.ForEach(f => {
                        f.DownloadDate = null;
                        f.DownloadError = false;
                        f.DownloadPath = null;
                    });
                }
                db.SaveChanges();
            }
            finally {
                if (disposeDb)
                    db.Dispose();
            }
        }

        public void ResetFailedSearches() {
            SearchStatusEnum[] failedStatuses = new SearchStatusEnum[] {
                SearchStatusEnum.CreateExportError, SearchStatusEnum.CreateSearchFailed, SearchStatusEnum.ExportCreateFail
            };
            using (var db = GetDbContext()) {
                var q = db.SubSearchFile
                    //.AsNoTracking()
                    .Include(s => s.SubSearch.Search)
                    .Where(ssf =>
                        failedStatuses.Contains(ssf.SubSearch.StatusId) ||
                        failedStatuses.Contains(ssf.SubSearch.Search.SearchStatusId));
                var failedFiles = q.ToArray();
                foreach (var failed in failedFiles) {
                    failed.SubSearch.StatusId = SearchStatusEnum.SearchCreated;
                    failed.SubSearch.Search.SearchStatusId = SearchStatusEnum.Pending;
                }
            }
        }

        private void RemoveSubsearchFiles() {

        }

        public SubSearch GetDownloadableExportByName(string name) {
            using (var db = GetDbContext()) {
                var q = db.SubSearch
                    .AsNoTracking()
                    .Where(s => s.Name == name)
                    .Where(s => (s.StatusId != SearchStatusEnum.Success && s.StatusId != SearchStatusEnum.DownloadFailed) || s.SubSearchFiles.Any(ssf => !ssf.DownloadError && !ssf.DownloadDate.HasValue));
                var subSearch = q.FirstOrDefault();
                return subSearch;
            }
        }

        public List<SubSearch> GetSearchesToExport() {
            using (var db = GetDbContext()) {
                var searches = db.SubSearch
                    .Include(s => s.Search)
                    .AsNoTracking()
                    .Where(s => _searchStatusToExport.Contains(s.StatusId))
                    .ToList();
                return searches;
            }
        }

        public SubSearchFile[] AddSubSearchFiles(SubSearchFile[] files, int subSearchId) {
            using (var db = GetDbContext()) {
                var subSearch = db.SubSearch
                    .Include(s => s.SubSearchFiles)
                    .FirstOrDefault(s => s.Id == subSearchId);
                if (subSearch == null)
                    throw new ApplicationException($"Subsearch Id not found: {subSearchId}");
                foreach (var file in files) {
                    var exists = subSearch.SubSearchFiles.Any(sf => sf.McOriginalFileName == file.McOriginalFileName);
                    if (exists) {
                        continue;
                    }
                    file.SubSearchId = subSearch.Id;
                    subSearch.SubSearchFiles.Add(file);
                }
                db.SaveChanges();
                return db.SubSearchFile
                    .AsNoTracking()
                    .Include(s => s.SubSearch)
                    .Where(f => f.SubSearchId == subSearchId)
                    .ToArray();
            }
        }

        public SubSearchFile UpdateSubSearchFile(int subSearchId, string downloadFilePath, string fileName, DateTime? downloadDate, bool downloadError) {
            if (string.IsNullOrEmpty(fileName))
                return null;

            SubSearchFile subSearchFile = null;
            using (var db = GetDbContext()) {
                var q = db.SubSearchFile
                    .Include(s => s.SubSearch.Search)
                    .Where(s => s.SubSearchId == subSearchId);

                subSearchFile = q.Where(s => s.McOriginalFileName == fileName)
                    .FirstOrDefault();
                if (subSearchFile == null) {
                    //sometimes, the file name has to be sanitized before using 
                    fileName = fileName.ToLower();
                    foreach (var file in q) {
                        var mcFileName = file.McOriginalFileName;
                        mcFileName = Utility.SanitizeFileName(mcFileName);
                        if (mcFileName.ToLower() == fileName) {
                            //TODO: should update the file download path (file sanitized)
                            subSearchFile = file;
                            break;
                        }
                    }
                }
                if (subSearchFile == null)
                    return null;

                subSearchFile.DownloadDate = downloadDate;
                subSearchFile.DownloadPath = downloadFilePath;
                subSearchFile.DownloadError = downloadError;
                db.SaveChanges();
            }
            return subSearchFile;
        }

        private void ComputeSearchStatus(int searchId) {
            //should implement a complete status calculation
            //but for now, just call SetSearchStatusSuccess method:
            SetSearchStatusSuccess(searchId);
        }

        /// <summary>
        /// returns searches that did not compplete generating their subsearches OR have their status on fail
        /// these searches will have to be reset completely before retrying to generate them again
        /// </summary>
        /// <returns></returns>
        public Search[] GetSearchesToReset() {
            using (var db = GetDbContext()) {
                var searches = db.Search
                    //.Where(s => s.Id == 398)
                    .Where(s => (s.SearchStatusId == SearchStatusEnum.CreateSearchFailed) ||
                        (s.SearchStatusId == SearchStatusEnum.Pending && db.SubSearch.Any(ss => ss.SearchId == s.Id)))
                    .ToArray();
                return searches;
            }
        }

        /// <summary>
        /// detects and resets the searches that have either SearchFailed status of they did not complete to generate
        /// </summary>
        /// <returns></returns>
        public SubSearchFile[] RepairBrokenSearches() {
            List<SubSearchFile> list;
            var searchesReset = GetSearchesToReset();
            using (var db = GetDbContext()) {
                var searchIds = searchesReset.Select(s => s.Id).ToArray();
                list = db.SubSearchFile
                    .AsNoTracking()
                    .Where(ssf => searchIds.Contains(ssf.SubSearch.SearchId))
                    .ToList();
            }

            foreach (var search in searchesReset) {
                ResetSearch(search.Id, true);
            }
            return list.ToArray();
        }

        public int GetSubSearchCount() {
            using (var db = GetDbContext()) {
                var c = db.SubSearch.Count();
                return c;
            }
        }
    }
}
