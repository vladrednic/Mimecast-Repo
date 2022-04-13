using A2A.Automation.Base;
using A2A.Automation.Base.Exceptions;
using A2A.ExcelReporter;
using A2A.MC.Data;
using A2A.MC.Kernel.Entities;
using A2A.MC.Kernel.Exceptions;
using A2A.Notifications.Email;
using A2A.Option;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
//using System.IO.Compression;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using WaitHelpers = SeleniumExtras.WaitHelpers;

namespace A2A.MC.Automation {
    public class MCAutomationDriver : AutomationDriverBase, ICloneable {

        public MCAutomationDriver(Options opt) : base(opt) {
            Options = opt;
        }

        protected string LastCreatedSearch { get; private set; }
        protected DateTime? LastExportLaunched { get; private set; } = DateTime.Now;

        public static List<string> LaunchedSearches { get; set; }

        private static FileStream _fsLaunched;
        private static StreamWriter _swLaunched;

        private DateTime? _lastNotificationSent;
        private readonly object _syncNotification = new object();

        readonly DbManager _db = new DbManager();

        public MCActions MCAction { get; private set; }

        private const int ExportIndexSource = 1;
        private const int ExportIndexSearchName = ExportIndexSource + 1;
        private const int ExportIndexDiscoveryCase = ExportIndexSource + 2;
        private const int ExportIndexCreatedBy = ExportIndexSource + 3;
        private const int ExportIndexFormat = ExportIndexSource + 4;
        private const int ExportIndexCreated = ExportIndexSource + 5;
        private const int ExportIndexPrepared = ExportIndexSource + 6;
        private const int ExportIndexTotalMessages = ExportIndexSource + 7;
        private const int ExportIndexCompleted = ExportIndexSource + 8;
        private const int ExportIndexRemaining = ExportIndexSource + 9;
        private const int ExportIndexStatus = ExportIndexSource + 10;

        private bool CanLaunchNewExport() {
            var dtLast = LastExportLaunched.HasValue ? LastExportLaunched.Value : DateTime.Now.Subtract(new TimeSpan(0, 10, 0));
            var time = DateTime.Now.Subtract(dtLast);
            var breakInterval = new TimeSpan(0, 1, 0);
            var ok = time > breakInterval;
            if (!ok) {
                Info($"Cannot launch new export.{Environment.NewLine}Minimum break before launches or application start: {breakInterval}{Environment.NewLine}Time since last launch: {time}");
                return ok;
            }
            var anyPending = Exports.Any(e => e.ExportStatus == ExportStatus.PreparationPending);
            ok = !anyPending;
            if (!ok) {
                Info($"Cannot launch new export. There is at least one export Preparation Pending status, we should not start new exports while pending");
            }
            return ok;
        }

        protected List<ExportMC> _exports = new List<ExportMC>();

        protected List<ExportMC> Exports {
            get { return _exports; }
        }

        protected List<SearchMC> Searches = new List<SearchMC>();
        private bool _connected;
        private DateTime _lastDownloadStart;
        private static readonly TimeSpan _downloadStartTimeout = new TimeSpan(0, 0, 30);

        private int LastSlots { get; set; }
        public Options Options { get; }

        protected override void AddArguments(List<string> arguments) {
            base.AddArguments(arguments);
            arguments.Add($"HomeSearchFolder     = {Options.HomeSearchFolder}");
            arguments.Add($"OffTimeSlots         = {Options.OffHoursTimeSlots}");
            arguments.Add($"WorkTimeSlots        = {Options.WorkHoursTimeSlots}");
            arguments.Add($"LastCreatedSearch    = {LastCreatedSearch}");
            arguments.Add($"MaxResultsPerExport  = {Options.MaxResultsPerExport}");
            arguments.Add($"ExportFormat         = {Options.ExportFormat}");
            arguments.Add($"IncludeBccRecipients = {Options.IncludeBccRecipients}");
            arguments.Add($"SmtpScenario         = {Options.SmtpScenario}");
        }

        private string GetLaunchedSearchesFilePath() {
            var fileName = Path.Combine(ReportsFolderPath, "_launched.txt");
            return fileName;
        }

        public override void Dispose() {
            base.Dispose();
            //if (_swLaunched != null) {
            //    _swLaunched.Dispose();
            //    _swLaunched = null;
            //}
            //if (_fsLaunched != null) {
            //    _fsLaunched.Dispose();
            //    _fsLaunched = null;
            //}
        }

        public void ConfirmSearches() {
            MCAction = MCActions.Reconcile;
            Info($"Reconciling searches from home folder: {Options.HomeSearchFolder}");
            using (var driver = CreateWebDriver(WebDriverType.Firefox)) {
                Authenticate();
                GotoHomeFolderWithRetry();
                Driver.SwitchTo().Frame(1);

                var affected = _db.ResetSubSearchReconciledFlag();
                Info($"Reconciled flag reset for {affected} subsearches");
                Info("Reconciling individual subsearches");

                //try {
                //    var SysDepth = WaitElementById("SysDepth", ElementCoditionTypes.ElementExists, 5);
                //    SelectElement select = new SelectElement(SysDepth);
                //    select.SelectByValue("1000");
                //}
                //catch { }

                int searchesFound = 0;
                int searchesReconciled = 0;
                Info($"Enumerating page: 1");
                var unreconciledList = new List<SubSearch>();
                while (true) {
                    var pageUnreconciled = ConfirmSearchesPage(out searchesFound, out searchesReconciled);
                    unreconciledList.AddRange(pageUnreconciled);
                    int? totalPages;
                    var newPageIndex = GotToNextSearchesPage(out totalPages);
                    if (!newPageIndex.HasValue) {
                        Info("Enumeration finished");
                        break;
                    }
                    Info($"Enumerating page: {newPageIndex + 1} of {totalPages}");
                }
                Info("===== Unreconciled Report =====");
                Info($"Total unreconciled subsearches: {unreconciledList.Count}");

                var subList = unreconciledList.Where(ss => ss.Id == 0).ToList();
                Info($"==> Subsearches in Mimecast but not in database: {subList.Count}");
                foreach (var subSearch in subList) {
                    Info($"Name: {subSearch.Name}; Begin: {subSearch.BeginDate}; End: {subSearch.EndDate}");
                }

                subList = unreconciledList.Where(ss => ss.Id != 0).ToList();
                Info($"==> Subsearches missing in Mimecast but in database: {subList.Count}");
                foreach (var subSearch in subList) {
                    Info($"Id: {subSearch.Id}; Name: {subSearch.Name}; Begin: {subSearch.BeginDate}; End: {subSearch.EndDate}");
                }
            }
        }

        private int? GotToNextSearchesPage(out int? totalPages) {
            totalPages = null;
            var SysPage = WaitElementById("SysPage", ElementCoditionTypes.ElementExists);
            SelectElement select = new SelectElement(SysPage);
            var index = select.Options.IndexOf(select.SelectedOption);
            totalPages = select.Options.Count;
            if (index + 1 < select.Options.Count) {
                var newIndex = index + 1;
                select.SelectByIndex(newIndex);
                return newIndex;
            }
            return null;
        }

        private IEnumerable<SubSearch> EnumerateSearchesPage() {
            var row = 0;
            var trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

            do {
                row++; //skip header
                if (trs == null || trs.Count < 2) {
                    break;
                }
                if (row >= trs.Count) {
                    Info($"Page enumeration completed");
                    break;
                }
                var tr = trs[row];

                Info($"Reconciling row {row}: {tr.Text}");
                var subSearch = ParseSearchDetails(tr);
                yield return subSearch;
            } while (true);
        }

        /// <summary>
        /// returns unreconciled subsearches:
        /// 1. Subsearches that appear in Mimecast but not in database (SubSearch.Id = 0)
        /// 2. Subsearches that appear in database but not in Mimecas (Subsearch.Id != 0)
        /// </summary>
        /// <param name="searchesFound"></param>
        /// <param name="searchesReconciled"></param>
        /// <returns></returns>
        public List<SubSearch> ConfirmSearchesPage(out int searchesFound, out int searchesReconciled) {
            var list = new List<SubSearch>();
            searchesFound = 0;
            searchesReconciled = 0;

            foreach (var subSearch in EnumerateSearchesPage()) {
                var reconciled = _db.MarkSubSearchReconciled(subSearch);
                if (reconciled == null) {
                    Info($"[Not reconciled]");
                    list.Add(subSearch);
                }
                else {
                    Info("[Reconciled]");
                    list.Add(reconciled);
                }
            }
            return list;
        }

        private SubSearch ParseSearchDetails(IWebElement tr) {
            var subSearch = new SubSearch();
            var tds = tr.FindElements(By.TagName("td"));
            var index = 0;
            foreach (var td in tds) {
                string text = null;
                try {
                    var span = td.FindElement(By.TagName("span"));
                    if (span != null)
                        text = span.GetAttribute("title");
                }
                catch { }

                if (string.IsNullOrEmpty(text))
                    text = td.Text;

                switch (index) {
                    case 1:
                        subSearch.Name = text;
                        break;
                    case 5:
                        //Start
                        subSearch.BeginDate = TryParseDateTime(text);
                        break;
                    case 6:
                        //End
                        subSearch.EndDate = TryParseDateTime(text);
                        break;
                }
                index++;
            }
            return subSearch;
        }

        private void CreateLaunchedSearches(string fileName) {
            var folderPath = Path.GetDirectoryName(fileName);
            Directory.CreateDirectory(folderPath);
            var files = Directory.GetFiles(folderPath, "*.txt", SearchOption.AllDirectories);
            var list = new List<string>();
            using (var sw = File.CreateText(fileName)) {
                sw.AutoFlush = true;
                foreach (var file in files) {
                    using (var sr = File.OpenText(file)) {
                        string line;
                        var first = true;
                        while ((line = sr.ReadLine()) != null) {
                            if (first) {
                                first = false;
                                continue;
                            }
                            var parts = line.Split('\t');
                            if (parts.Length < 2)
                                continue;
                            var name = parts[0];
                            var status = parts[1];
                            if (list.Contains(name))
                                continue;
                            if (status.ToLower() == "none")
                                continue;

                            list.Add(name);
                            sw.WriteLine(name);
                        }
                    }
                }
                sw.Flush();
            }
        }

        private void LoadExportsFromDb() {
            Exports.Clear();
            Exports.AddRange(_db.GetExports());
            lock (Exports) {
                Info($"Loaded {Exports.Count} exports");
            }
        }

        private void LoadLaunchedSearchesFromDb() {
            Info($"Loading launched searches");
            LaunchedSearches = _db.GetLaunchedSearches();
        }

        private void LoadLaunchedSearches() {
            if (_fsLaunched != null)
                return;
            string fileName = null;
            Info($"Loading launched searches");
            try {
                LaunchedSearches = new List<string>();
                fileName = GetLaunchedSearchesFilePath();
                Info($"From {fileName}");
                if (!File.Exists(fileName)) {
                    CreateLaunchedSearches(fileName);
                }
                using (var sr = File.OpenText(fileName)) {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        LaunchedSearches.Add(line);
                    }
                }
                Info($"Found {LaunchedSearches.Count} launched exports");
            }
            catch (Exception ex) {
                Error(ex);
            }
            _fsLaunched = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);
            _swLaunched = new StreamWriter(_fsLaunched);
            _swLaunched.AutoFlush = true;
        }

        private void AddLaunchedSearch(string searchName) {
            _db.SetSubSearchStatus(searchName, SearchStatusEnum.ExportCreated, out _);
            //_swLaunched.WriteLine(searchName);
            //_swLaunched.Flush();
            //LaunchedSearches.Add(searchName);
        }

        private Search ParseDbSearch(string line) {
            var exp = new Search();
            var fields = line.Split('\t');
            exp.ParseLine(fields);
            return exp;
        }

        protected override void OnScenarioLine(string line) {
            var search = ParseSearch(line);
            var dbSearch = ParseDbSearch(line);
            _db.AddSearch(dbSearch);

            if (search != null)
                Searches.Add((SearchMC)search);
            var export = ParseExport(line);
            lock (Exports) {
                if (export != null)
                    Exports.Add((ExportMC)export);
            }
        }

        //public void Process() {
        //    LoadScenarioFile();
        //    Info($"Loaded {Exports.Count} exports");
        //    using (var driver = CreateWebDriver(WebDriverType.Firefox)) {
        //        Authenticate();
        //        do {
        //            GotoHomeFolder();
        //            var changes = UpdateSearches();
        //            if (changes > 0) {
        //                //TODO: save new report
        //            }

        //            //CreateExports();

        //            Sleep(60 * 1000);
        //        } while (true);
        //    }
        //}

        protected override void OnInitData() {
            lock (_exports) {
                _exports = new List<ExportMC>();
            }
            lock (Searches) {
                Searches = new List<SearchMC>();
            }
        }

        private int GetMaxConcurrentSlots(int workTimeSlots, int offTimeSlots) {
            var dtNow = DateTime.Now;
            int hours;
            switch (dtNow.DayOfWeek) {
                case DayOfWeek.Monday:
                    hours = dtNow.TimeOfDay.Hours;
                    if (hours < 6)
                        return offTimeSlots;
                    return workTimeSlots;
                case DayOfWeek.Friday:
                    hours = dtNow.TimeOfDay.Hours;
                    if (hours > 19)
                        return offTimeSlots;
                    return workTimeSlots;
                case DayOfWeek.Saturday:
                case DayOfWeek.Sunday:
                    return offTimeSlots;
                default:
                    return workTimeSlots;
            }
        }

        private bool CanSendNotification() {
            lock (_syncNotification) {
                if (!_lastNotificationSent.HasValue)
                    return true;
                var lastNotification = _lastNotificationSent.Value;

                var timeout = new TimeSpan(0, 1, 0);

                if (DateTime.Now.Subtract(lastNotification) < timeout)
                    return false;

                return true;
            }
        }

        //protected void SendNotification(float? percentCompleted = null, string to = null, string body = null, string subject = null) {
        //    try {
        //        if (!CanSendNotification())
        //            return;
        //        if (!_connected || sender.Connection.State != ConnectionState.Connected) {
        //            _connected = false;
        //            Info("Connecting");
        //            _connected = sender.Init();
        //            Info($"Notifications connected: {_connected}");
        //        }
        //        if (_connected) {
        //            Info("KeepAlive");
        //            sender.SendKeepAlive(percentCompleted);
        //            _lastNotificationSent = DateTime.Now;
        //        }
        //    }
        //    catch (Exception ex) {
        //        Info($"WARN: cannot send notification: {ex.Message}");
        //    }
        //}

        public void CheckExportsWithRetry() {
            do {
                try {
                    CheckExports();
                }
                catch (Exception ex) {
                    Error(ex);
                    Info("[Retry] Download check");
                }
            } while (true);
        }

        public void CheckExports() {
            MCAction = MCActions.CheckExport;
            Info("Start check exports");
            LoadLaunchedSearchesFromDb();
            LoadExportsFromDb();
            int sleepTime = 5 * 60 * 1000;
            if (Debugger.IsAttached)
                sleepTime /= 2;
            //CountMessages();
            using (var driver = CreateWebDriver(WebDriverType.Firefox)) {
                Authenticate();
                do {
                    //Context.ProjectName = "Mimecast Extraction";
                    //Context.ProjectTypeName = "Mimecast Extraction";

                    //SendCheckExportNotification();
                    PerformActivity();
                    GotoExportsPage();

                    //test
                    //var n = GetSlotsInUse();
                    //if (LaunchExports(1))
                    //    return;

                    ExportMC exportToDownload;
                    //UpdateExports(out exportToDownload);

                    LoadExportsFromDb();
                    Info("Looking for an export available to download");
                    var ok = DownloadExportWithRetry(DownloadFolderPath, out exportToDownload);

                    if (exportToDownload == null) {
                        Info($"No downloads pending");

                        if ((bool)Options.AllowLaunchNewExports) {
                            Info($"Checking for available export slots");
                            var slotsInUse = GetSlotsInUse();
                            var maxSlots = GetMaxConcurrentSlots((int)Options.WorkHoursTimeSlots, (int)Options.OffHoursTimeSlots);
                            if (LastSlots != maxSlots) {
                                Info($"Allowed slots changed from {LastSlots} to {maxSlots}");
                                LastSlots = maxSlots;
                            }
                            var freeSlots = maxSlots - slotsInUse;
                            if (freeSlots < 0) freeSlots = 0;
                            Info($"Used slots {slotsInUse} of max {maxSlots} allowed. Free: {freeSlots} slots. Required free slots to launch exports: {Options.MinSlotsForNewExports}");
                            if (slotsInUse <= maxSlots - Options.MinSlotsForNewExports) {
                                //SaveScreenshot(); //too many screenshots
                                var count = maxSlots - slotsInUse;
                                ok = LaunchExportsAfterDownload(count);
                                if (ok) {
                                    LastExportLaunched = DateTime.Now;
                                }
                                WriteExportReport();
                                RefresshExportsPage();
                                Sleep(10 * 1000);
                                continue;
                            }
                        }
                        else {
                            Info("Launching new exports not allowed");
                        }

                        Info($"Waiting {sleepTime / 1000} seconds");
                        //WriteExportReport();
                        RefresshExportsPage();
                        Sleep(sleepTime);
                        continue;
                    }

                    if (ok) {
                        Search parentSearch;
                        UpdateExportStatus(exportToDownload, out parentSearch);
                        if (parentSearch != null) {
                            Info($"[{parentSearch.SearchStatusId}] Search: {parentSearch.Name}");
                        }
                        else {
                            Error($"Could not find parent of export {exportToDownload.Name}");
                        }
                        ReturnFromExport();
                        //ResumeExportAfterDownload(exportToDownload);
                        //WriteExportReport();
                        SwitchToParentFrame();
                    }
                    RefresshExportsPage();
                    Sleep(10 * 1000);
                } while (true);
            }
        }

        private void UpdateExportStatus(ExportMC exportToDownload, out Search parentSearch) {
            _db.SetSubSearchStatus(exportToDownload.Name, null, out parentSearch);
        }

        private void ReturnFromExport() {
            var adconAction = WaitElementByClassName("adconAction", ElementCoditionTypes.ElementIsVisible);
            adconAction.Click();
        }

        //private void SendCheckExportNotification() {
        //    //compute percentage
        //    var percentage = (float?)null;
        //    if (Exports == null || Exports.Count < 1)
        //        SendNotification();
        //    percentage = (100f * LaunchedSearches.Count) / Exports.Count;
        //    SendNotification(percentage);
        //}

        private void CountMessages() {
            lock (Exports) {
                var noCount = Exports.Count(e => string.IsNullOrEmpty(e.Messages));
                if (noCount > 0) {
                    Info($"Searches without message counts: {noCount}");
                    MCAutomationDriver d = (MCAutomationDriver)this.Clone();
                    Thread th = new Thread(new ThreadStart(d.DoGetSearchTotalMessages));
                    th.Name = "DoGetSearchTotalMessages";
                    th.Start();
                    //DoGetSearchTotalMessages();
                }
            }
        }

        private void RefresshExportsPage() {
            Driver.Navigate().Refresh();
            Driver.SwitchTo().DefaultContent();
        }

        private bool LaunchExportsAfterDownload(int count) {
            if (!CanLaunchNewExport()) {
                return false;
            }
            List<ExportMC> toLaunch = new List<ExportMC>();
            //Info($"Excluding {LaunchedSearches.Count} already launched searches");
            var searches = new List<SubSearch>();
            if (SmtpScenario) {
                searches = _db.GetSearchesToExport().OrderByDescending(s => s.EndDate.Value).ToList();
            }
            else {
                searches = _db.GetSearchesToExport();
            }

            foreach (var search in searches) {
                var exportmc = new ExportMC();
                exportmc.LoadFrom(search);
                exportmc.ExportFormat = ExportFormat;
                toLaunch.Add(exportmc);
            }

            //lock (Exports) {
            //    toLaunch = Exports
            //    .Where(s => s.ExportStatus == ExportStatus.None)
            //    //.Where(e => e.EstimatedExportsCount <= count)
            //    //.Where(e => e.EstimatedExportsCount > 0)
            //    ////.Where(e => !LaunchedSearches.Contains(e.Name))
            //    //.Take(count)
            //    //.OrderByDescending(e => e.EstimatedExportsCount)
            //    .ToList();
            //}
            if (toLaunch.Count < 1) {
                Info("==> No searches to export");
                return false;
            }
            Info($"==> Searches left to export: {toLaunch.Count}");
            //List<ExportMC> selected = new List<ExportMC>(toLaunch.Count);
            //toLaunch.ForEach(e => {
            //    var n = selected.Sum(s => s.EstimatedExportsCount);
            //    if (n >= count) return;
            //    if (n + e.EstimatedExportsCount <= count) {
            //        selected.Add(e);
            //    }
            //});
            //toLaunch = selected;
            //var exportNames = string.Join(Environment.NewLine, toLaunch.Select(e => e.Name));
            var msg = $"Export slots available: {count}, to launch {toLaunch.Count} with an estimated slots consumption of {toLaunch.Sum(e => e.EstimatedExportsCount)}";
            Info(msg);
            //toLaunch = toLaunch.Where(s => s.Name.Contains("chad.smith@east-thames.co.uk_201306010000_201503120559")).ToList();
            try {
                foreach (var src in toLaunch) {
                    if (src.ItemsCount < 1) {
                        Info($"[Success] Search with no items will not be exported by Mimecast: {src.Name}");
                        _db.SetSubSearchStatus(src.Name, SearchStatusEnum.Success, out _);
                        continue;
                    }
                    Info($"Launching the export for sub search: {src.Name}");
                    using (var d = (MCAutomationDriver)this.Clone()) {
                        d.ActionType = "LaunchExport";
                        if (d.DoStartExport(src)) {
                            src.ExportStatus = ExportStatus.ExportCreated;
                            AddLaunchedSearch(src.Name);
                            Info($"[Success] Export launched for search: {src.Name}");
                        }
                        else {
                            Error($"[Fail] Export could not be launched for search: {src.Name}. An explanation of the error or an alert may be found in this log above this line.");
                            _db.SetSubSearchStatus(src.Name, SearchStatusEnum.ExportCreateFail, out _);
                        }
                    }
                }
            }
            finally {
                WriteExportReport();
            }
            return true;
        }

        private bool DoStartExport(ExportMC src) {
            using (CreateWebDriver(WebDriverType.Firefox)) {
                Authenticate();
                do {
                    GotoHomeFolderWithRetry();
                    try {
                        return ApplySearchFilterAndLaunchExport(src.Name, src.ExportFormat);
                    }
                    catch (Exception ex) {
                        Error($"[Fail] Could not launch export for search {src.Name}");
                        Error(ex.Message);
                        Info($"[Retry] Launch export for search {src.Name}");
                    }
                } while (true);
            }
        }

        public void DoGetSearchTotalMessages() {
            MCAction = MCActions.GetCount;
            ActionType = nameof(MCActions.GetCount);
            lock (Exports) {
                if (Exports == null || Exports.Count < 1) {
                    LoadScenarioFile();
                }
            }
            while (true) {
                try {
                    using (CreateWebDriver(WebDriverType.Firefox)) {
                        Authenticate();
                        GotoHomeFolderWithRetry();
                        GetSearchTotalMessages();
                    }
                }
                catch (Exception ex) {
                    Error(ex);
                }
                finally {
                    WriteExportReport();
                }
            }
        }

        private void GetSearchTotalMessages() {
            var processed = new List<ExportMC>();
            ExportMC nextSearch;
            var dtStart = DateTime.Now;
            var launched = 0;
            LoadLaunchedSearchesFromDb();
            var qNext = Exports.Where(s => string.IsNullOrEmpty(s.Messages)) //&& !processed.Contains(s))
                 .Where(s => !LaunchedSearches.Contains(s.Name));
            lock (Exports) {
                nextSearch = qNext.FirstOrDefault();
            }
            SwitchToFrame(1);
            bool first = true;
            while (nextSearch != null) {
                processed.Add(nextSearch);
                GetSearchTotalMessages(nextSearch, first);
                first = false;
                WriteExportReport();
                var toGo = 0;
                launched++;
                lock (Exports) {
                    nextSearch = qNext.FirstOrDefault();
                    toGo = qNext.Count();
                }
                var counted = Exports.Count - toGo;
                var percentCompleted = 100f * counted / Exports.Count;
                var tsRuntime = DateTime.Now.Subtract(dtStart);
                tsRuntime = TimeSpan.FromSeconds((int)tsRuntime.TotalSeconds);
                var secondsTogo = tsRuntime.TotalSeconds * (toGo / launched);
                var tsDuration = TimeSpan.FromSeconds((int)secondsTogo);
                Info($"Total counts: {counted}, remaining: {toGo} of {Exports.Count} ({Math.Round(percentCompleted, 2)}%)");
                Info($"Run time: {tsRuntime}. Estimated finish duration: {tsDuration}");

                //Context.ProjectName = "Mimecast Results Count";
                //Context.ProjectTypeName = "Mimecast Results Count";

                //SendNotification(percentCompleted);
            }
        }

        private int? ParseTotalCount(string text) {
            if (string.IsNullOrEmpty(text))
                return null;
            if (text == "(0) Rows") {
                return 0;
            }
            text = new string(text.Where(c => char.IsDigit(c)).ToArray());
            if (int.TryParse(text, out int result))
                return result;
            return null;
        }

        private void GetSearchTotalMessages(ExportMC export, bool first) {
            string name = export.Name;
            if (first)
                WaitForLoading();
            else
                Sleep();
            if (DismissAlert()) {
                Driver.SwitchTo().DefaultContent();
                Driver.SwitchTo().Frame(1);
            }
            var searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            //clear text
            searchBox.Click();
            Sleep();
            searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            searchBox.Clear();
            Sleep();
            searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            searchBox.SendKeys(name);
            Sleep();
            searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            searchBox.SendKeys(Keys.Enter);
            Sleep(5 * 1000);
            var trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

            var index = 1;
            if (trs.Count != 2) {
                index = 0;
                foreach (var tr1 in trs) {
                    var tds = tr1.FindElements(By.TagName("td"));
                    if (tds == null || tds.Count < 2) {
                        index++;
                        continue;
                    }
                    var value = tds[1].Text.ToLower().Trim();
                    if (export.Name.ToLower().Trim() == value)
                        break;
                    index++;
                }
            }

            var initialCounter = WaitElementByClassName("adconRowCount", ElementCoditionTypes.ElementIsVisible, 300);
            var intialText = initialCounter.Text;

            var tr = trs[index];
            tr.Click();

            Sleep();
            string text;

            var dtStart = DateTime.Now;
            bool goBack = true;

            while (true) {
                try {

                    var counter = WaitElementByClassName("adconRowCount", ElementCoditionTypes.ElementIsVisible, 300);
                    text = counter.Text;
                    if (DateTime.Now.Subtract(dtStart).TotalSeconds > 60) { break; }
                    if (text == intialText) {
                        if (text == "(0) Rows") {
                            export.ItemsCount = 0;
                            goBack = false;
                            break;
                        }
                        Sleep();
                        continue;
                    }
                    break;
                }
                catch { }
            }

            text = new string(text.Where(c => char.IsDigit(c)).ToArray());

            export.Messages = text;
            Info("");
            Info("");
            Info($"Found {text} messages for {export.Name}");
            int.TryParse(text, out int itemsCount);
            export.ItemsCount = itemsCount;

            if (goBack) {
                //press button Go Back
                var button = WaitElementByText("Go Back", ElementCoditionTypes.ElementIsVisible);
                button.Click();
            }
            else {
                searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
                //clear text
                searchBox.Click();
                searchBox.Clear();
                searchBox.SendKeys(Keys.Enter);
                Sleep(5 * 1000);
            }
        }

        private void WriteSearchReport() {
            WriteSearchReport(Searches, ReportsFolderPath);
        }

        private void WriteExportReport() {
            WriteExportReport(Exports, ReportsFolderPath);
        }

        private void WriteExportReport(List<ExportMC> exports, string reportsFolderPath) {
            if (!_mScenario.WaitOne(60 * 1000)) {
                throw new ApplicationException("Could not read the scenario files in due time");
            }
            try {
                var hasHeader = ScenarioFileHasHeader;
                Directory.CreateDirectory(reportsFolderPath);
                //var fileName = $"Report_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt";
                var fileName = $"Report_{DateTime.Now.ToString("yyyyMMdd")}.txt";
                fileName = Path.Combine(reportsFolderPath, fileName);
                using (var sw = File.CreateText(fileName)) {
                    sw.AutoFlush = true;
                    if (hasHeader) {
                        var line = EntityBase.GetHeader();
                        sw.WriteLine(line);
                    }
                    lock (Exports) {
                        foreach (var exp in exports) {
                            var line = exp.ToString();
                            sw.WriteLine(line);
                        }
                    }
                }
            }
            finally {
                _mScenario.ReleaseMutex();
            }
        }

        private int GetSlotsInUse() {
            ExportMC export;
            lock (Exports) {
                Exports.ForEach(e => e.ExportStatus = ExportStatus.None);
            }
            UpdateExports(out export, out Dictionary<ExportMC, int> usedSlots, false);

            var slotsCount = usedSlots.Sum(s => s.Value);

            //var msg = $"Slots in use {slotsCount}:{Environment.NewLine}{(string.Join(Environment.NewLine, usedSlots.Select(e => e.Key + $"\t({e.Value})")))}";
            //Info(msg);

            return slotsCount;
        }

        private bool IdentifyAndDismissAlert() {
            try {
                var alertid = WaitElementByClassName("ui-dialog-buttonset", ElementCoditionTypes.ElementIsVisible, 1);
                if (alertid != null) {
                    var button = alertid.FindElement(By.XPath(".//*"));
                    button.Click();
                    Info("Alert dismissed");
                    return true;
                }
            }
            catch { }
            return false;
        }

        public void DeleteSearches() {
            MCAction = MCActions.DeleteSearch;
            Info($"Deleting searches from home folder: {Options.HomeSearchFolder}");
            var deleted = 0;
            var row = 1;

            //settings:
            //an alert will be displayed by Mimecast in case a search cannot be deleted because of an export being defined for that search
            //in this case we must dismiss the alert, but this takes slows a bit down the speed, so it make sense to be able to anable/disable the check
            var checkAlert = false;

            using (var driver = CreateWebDriver(WebDriverType.Firefox)) {
                Authenticate();
                GotoHomeFolderWithRetry();
                Driver.SwitchTo().Frame(1);

                var searchCount = "";
                ReadOnlyCollection<IWebElement> trs = null;
                do {
                    string count;
                    var dtStart = DateTime.UtcNow;

                    //try {
                    //    var SysDepth = WaitElementById("SysDepth", ElementCoditionTypes.ElementExists, 5);
                    //    SelectElement select = new SelectElement(SysDepth);
                    //    select.SelectByValue("1000");
                    //}
                    //catch { }

                    do {
                        count = "";
                        try {
                            var adconRowCount = Driver.FindElementByClassName("adconRowCount");

                            trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

                            //a dialog may appear due to export being already launched for this search: ui-dialog-buttonset
                            if (checkAlert) {
                                if (DateTime.UtcNow.Subtract(dtStart) > new TimeSpan(0, 0, 1)) {
                                    if (IdentifyAndDismissAlert()) {
                                        row++;
                                        Sleep();
                                        break;
                                    }
                                    row++;
                                    break;
                                }
                            }

                            count = adconRowCount.Text;
                        }
                        catch { }
                    } while (count == searchCount);

                    if (trs == null || trs.Count < 2) {
                        break;
                    }
                    if (row >= trs.Count) {
                        Info($"Searches deleted but some might still be deletable. Rerun the command to delete more.");
                        break;
                    }
                    var tr = trs[row];
                    searchCount = count;
                    Info($"Row {row}: {tr.Text}");
                    Info($"Left: {count}");

                    //get option icon
                    try {
                        var icon = tr.FindElements(By.TagName("td"))[0].FindElement(By.TagName("img"));
                        icon.Click();
                    }
                    catch {
                        if (IdentifyAndDismissAlert()) {
                            //row++;
                            Sleep();
                            continue;
                        }
                        break; //job done
                    }

                    Sleep(100);

                    var subMenus = Driver.FindElementsByClassName("SubMenu");

                    Info($"Deleting search");
                    subMenus[3].Click();
                    deleted++;
                    Sleep(200);
                } while (true);
            }
            Info($"Total deleted searches: {deleted}");
        }

        public void DoCreateSearches() {
            do {
                try {
                    CreateSearches();
                    ReconcileSearches();
                    Info("[Finish] Create Searches");
                    break;
                }
                catch (A2ASearchCountMismatch) {
                    //TODO implement a reconciliation procedure between the searches in Mimecast and the searches we have in database
                    //for now, we just consider this a breaking exception leaving this a manual task 
                    ReconcileSearches();
                    break;
                }
                catch (A2ABreakingException) {
                    break;
                }
                catch (Exception ex) {
                    Error(ex);
                    Info("[Retry] Create Search");
                }
            } while (true);
        }

        private void ReconcileSearches() {
            //TODO: implement the 1-0-1 reconciliation between Mimecast searches and the database
            //HINT: ConfirmSearches() method could be extended for that matter (?)
        }

        public void CreateSearches() {
            MCAction = MCActions.CreateSearch;
            _db.ResetFailedSearches();
            SubSearchFile[] filesToRemove = _db.RepairBrokenSearches();
            RemoveDownloadedFiles(filesToRemove, DownloadFolderPath);
            Searches = _db.GetSearchesToCreate();
            //Searches = Searches.Where(s => s.Name == "east livingspmanagerseast-living.co.uk@east-thames.co.uk").ToList();
            if (Searches == null || Searches.Count < 1) {
                Info("[Finish] No new searches to create");
                return;
            }
            //Info($"Loaded {Exports.Count} exports");
            using (var driver = CreateWebDriver(WebDriverType.Firefox)) {
                Authenticate();
                GotoHomeFolderWithRetry();
                int n = 0;
                LastCreatedSearch = "";
                Info($"Searches to be created: {Searches.Count}");
                try {
                    foreach (SearchMC search in Searches) {
                        n++;
                        var retry = false;
                        do {
                            retry = false;
                            try {
                                var prefix = $"{n} / {Searches.Count}.";

                                var percent = 100f * n / Searches.Count;
                                var search1 = _db.GetSearchByEmail(search.Email);
                                if (search1.SearchStatusId == SearchStatusEnum.SearchCreated)
                                    continue;
                                try {
                                    Info($@"{prefix} ==> Start creating new Mimecast search:
    [{search.Name}] [{FormatDate(search.BeginDate)}] - [{FormatDate(search.EndDate)}]");

                                    //Info($"{prefix} Creating new search: {search.Name}{Environment.NewLine}");
                                    if (CreateNewSearch(search, percent, true, search1.Id)) {
                                        _db.SetStatus(search1.Id, SearchStatusEnum.SearchCreated);
                                        Info($"[Success] New search created successfully: {search.Name}{Environment.NewLine}");
                                    }
                                    else {
                                        _db.SetStatus(search1.Id, SearchStatusEnum.CreateSearchFailed);
                                        Info($"[Fail] Could not create new search: {search.Name}{Environment.NewLine}");
                                    }
                                }
                                catch {
                                    _db.SetStatus(search1.Id, SearchStatusEnum.CreateSearchFailed);
                                    throw;
                                }
                                Sleep();
                            }
                            catch (McInvalidStateException) {
                                retry = true;
                                Info("Invalid application state detected, retrying");
                                GotoHomeFolderWithRetry();
                            }
                            catch (Exception ex) {
                                Trace.WriteLine(ex);
                                Error(ex);
                                throw;
                            }
                        } while (retry);
                    }
                }
                finally {
                    //WriteSearchReport(Searches, ReportsFolderPath);
                }
            }
        }

        protected void WriteSearchReport(List<SearchMC> searches, string reportsFolderPath) {
            if (!_mScenario.WaitOne(60 * 1000)) {
                throw new ApplicationException("Could not read the scenario files in due time");
            }
            try {
                var hasHeader = ScenarioFileHasHeader;
                Directory.CreateDirectory(reportsFolderPath);
                //var fileName = $"Report_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.txt";
                var fileName = $"Report_{DateTime.Now.ToString("yyyyMMdd")}.txt";
                fileName = Path.Combine(reportsFolderPath, fileName);
                using (var sw = File.CreateText(fileName)) {
                    sw.AutoFlush = true;
                    if (hasHeader) {
                        var line = EntityBase.GetHeader();
                        sw.WriteLine(line);
                    }
                    foreach (var src in searches) {
                        var line = src.ToString();
                        sw.WriteLine(line);
                    }
                }
            }
            finally {
                _mScenario.ReleaseMutex();
            }
        }

        private List<SearchMC> DetectSubSearchesWithRetry(SearchMC search, int maxResultsCount) {
            while (true) {
                try {
                    var result = DetectSubSearches(search, maxResultsCount);
                    return result;
                }
                catch (A2ABreakingException) {
                    throw;
                }
                catch (Exception ex) {
                    Error(ex);
                }
                GoBackToSearchDefinition();
                GetMainButton("Search", true);
                Info("[Retry]");
            }
        }

        private List<SearchMC> DetectSubSearches(SearchMC search, int maxResultsCount) {
            Info($"Determining sub searches of search {search.Name}");
            var subSearches = new List<SearchMC>();

            //var pages = new List<KeyValuePair<RowResult, long>>();
            //var resultsPerPage = WaitElementById("SysDepth", ElementCoditionTypes.ElementToBeClickable);

            long? totalRows = null;
            var retry = 5;
            do {
                do {
                    retry--;
                    if (retry < 0) {
                        //throw new ApplicationException("Cannot detect subsearches. See error details in the log above.");
                        throw new McInvalidStateException();
                    }
                    try {
                        WaitElementByClassName("adconRowCount", ElementCoditionTypes.ElementIsVisible);
                        break;
                    }
                    catch { }
                } while (true);
                var trs = Driver.FindElements(By.ClassName("adconRowCount"));
                foreach (var tr in trs) {
                    totalRows = GetRowsCount(tr.Text);
                    if (totalRows.HasValue) {
                        search.ItemsCount = totalRows;
                        Info($@"Search total results reported by Mimecast: [{totalRows ?? 0}]
    [{search.Name}] [{FormatDate(search.BeginDate)}] - [{FormatDate(search.EndDate)}]");
                        break;
                    }
                }

                if (totalRows.HasValue) {
                    search.ItemsCount = totalRows;
                    if (totalRows <= maxResultsCount || totalRows <= 1000) {
                        return subSearches; //no need to split searches
                    }
                    break;
                }
            } while (true);

            //var pages = GetPages(maxResultsCount); 
            Sleep();
            GetSearchInterval(out RowResult startRow, out RowResult endRow);

            DateTime startDate = search.BeginDate ?? startRow.Date.Value;
            DateTime endDate = search.EndDate ?? endRow.Date.Value;

            subSearches = SplitSearch(search, startDate, endDate);

            return subSearches;
        }

        private List<SearchMC> SplitSearch(SearchMC initialSearch, DateTime startDate, DateTime endDate) {
            var ts = endDate.Subtract(startDate);
            var seconds = Math.Abs((long)ts.TotalSeconds);
            var search1 = (SearchMC)initialSearch.Clone();
            var search2 = (SearchMC)initialSearch.Clone();

            search1.BeginDate = startDate;
            search1.EndDate = startDate.AddSeconds(seconds / 2);

            search2.BeginDate = search1.EndDate.Value.AddSeconds(1);
            search2.EndDate = endDate;

            search1.SetName();
            search2.SetName();

            if (!SmtpScenario) {
                search1.Email = string.Empty;
                search2.Email = string.Empty;
            }

            var list = new List<SearchMC> {
                search1,
                search2
            };

            return list;
        }

        private void ScrollIntoView(IWebElement element) {
            Actions actions = new Actions(Driver);
            actions.MoveToElement(element);
            actions.Perform();
        }

        /// <summary>
        /// returns the oldest and the newest message dates
        /// </summary>
        /// <param name="startRow"></param>
        /// <param name="endRow"></param>
        private void GetSearchInterval(out RowResult startRow, out RowResult endRow) {
            startRow = GetFirstRowResult(out _);
            var crtContext = startRow.Context;
            var retry = 3;
            do {
                IWebElement menu0 = null;
                try {
                    menu0 = WaitElementById("menu_0", ElementCoditionTypes.ElementIsVisible, 60);
                    menu0.Click();
                    Sleep(500);
                    var menu1 = WaitElementById("menu_1", ElementCoditionTypes.ElementIsVisible, 10);
                    ScrollIntoView(menu1);
                    menu1.Click();
                    break;
                }
                catch {
                    Actions builder = new Actions(Driver);
                    var element = menu0;
                    builder.MoveToElement(element).Build().Perform();
                }
                retry--;
                if (retry < 0)
                    throw new ApplicationException("Cannot get search interval");
            } while (true);
            //wait for page to reload
            do {
                endRow = GetFirstRowResult(out _);
                if (crtContext != endRow.Context) {
                    break;
                }
                Sleep();
            } while (true);
        }

        private List<KeyValuePair<RowResult, long>> GetPages(int maxResultsCount) {
            var pages = new List<KeyValuePair<RowResult, long>>();
            var resultsPerPage = WaitElementById("SysDepth", ElementCoditionTypes.ElementToBeClickable);

            long? totalRows = null;
            var trs = Driver.FindElements(By.ClassName("adconRowCount"));
            foreach (var tr in trs) {
                totalRows = GetRowsCount(tr.Text);
                if (totalRows.HasValue) {
                    Info($"Search total results reported by Mimecast: {totalRows ?? 0}");
                    break;
                }
            }

            if (totalRows.HasValue) {
                if (totalRows <= maxResultsCount || totalRows <= 1000) {
                    return pages;
                }
            }

            long totalItems = 0;

            int pageSize;
            var firstRowResult = GetFirstRowResult(out pageSize);
            if (firstRowResult == null)
                return pages;

            var selectElement = new SelectElement(resultsPerPage);
            selectElement.SelectByValue("1000");
            //wait until the 1000 items load...
            Info("Waiting for Mimecast to load 1000 items");

            do {
                Sleep(2000);
                GetFirstRowResult(out pageSize);
                if (pageSize >= 1000)
                    break;
            } while (true);
            //first 1000 items loaded

            //iterating through pages
            do {
                firstRowResult = GetFirstRowResult(out pageSize);
                totalItems += pageSize;
                pages.Add(new KeyValuePair<RowResult, long>(firstRowResult, totalItems));
                var crtContext = firstRowResult.Context;
                Info($"New page: {pages.Count}. Page Size: {pageSize}. Context: {crtContext}");
                var nextButton = GetNextPageButton();
                if (!nextButton.Enabled)
                    break;
                nextButton.Click();
                WaitNextResultsPage(crtContext);
            } while (true);

            return pages;
        }

        private void WaitNextResultsPage(string currentContext) {
            do {
                Sleep();
                var firstRow = GetFirstRowResult(out _);
                if (firstRow.Context != currentContext)
                    return;
            } while (true);
        }

        private IWebElement GetNextPageButton() {
            var retry = 5;
            do {
                retry--;
                if (retry < 0)
                    return null;
                try {
                    var buttons = Driver.FindElements(By.ClassName("StdReportIcons"));
                    foreach (var button in buttons) {
                        if (button.GetAttribute("title") == "Next Page")
                            return button;
                    }
                    Sleep();
                }
                catch { }
            } while (true);
        }

        private RowResult GetFirstRowResult(out int pageSize) {
            var rowResults = GetRowResults(1, out pageSize);
            if (rowResults == null || rowResults.Count < 1)
                return null;

            var firstRowResult = rowResults[0];
            return firstRowResult;
        }

        private List<RowResult> GetRowResults(int? top, out int pageSize) {
            do {
                try {
                    WaitElementByClassName("StdTableRow1", ElementCoditionTypes.ElementIsVisible);
                    var rows1 = Driver.FindElements(By.ClassName("StdTableRow1"));
                    var rows2 = Driver.FindElements(By.ClassName("StdTableRow2"));
                    var rows = new List<IWebElement>(rows1);
                    rows.AddRange(rows2);
                    pageSize = rows.Count;
                    Info($"Result rows: {rows.Count}");
                    var results = new List<RowResult>(1000);
                    foreach (var tr in rows) {
                        var rowResult = ParseRowResult(tr);
                        results.Add(rowResult);
                        if (results.Count >= top)
                            break;
                    }
                    return results;
                }
                catch (StaleElementReferenceException) { }
                catch (UnhandledAlertException) { }
            } while (true);
        }

        private RowResult ParseRowResult(IWebElement tr) {
            var tds = tr.FindElements(By.TagName("td"));
            var i = 0;
            var rowResult = new RowResult();
            foreach (var td in tds) {
                i++;
                switch (i) {
                    case 0:
                        break;
                    case 3:
                        rowResult.From = td.Text;
                        break;
                    case 4:
                        rowResult.To = td.Text;
                        break;
                    case 5:
                        rowResult.Subject = td.Text;
                        break;
                    case 8:
                        float f;
                        try {
                            if (float.TryParse(td.Text.Split(' ')[0], out f))
                                rowResult.Size = f;
                        }
                        catch { }
                        break;
                    case 9:
                        DateTime dt;
                        if (DateTime.TryParse(td.Text, out dt)) {
                            rowResult.Date = dt;
                        }
                        break;
                    default:
                        break;
                }
            }

            rowResult.Context = tr.GetAttribute("onClick");
            return rowResult;
        }

        private long? GetRowsCount(string text) {
            if (string.IsNullOrEmpty(text))
                return null;
            var textLower = text.ToLower();
            if (!textLower.Contains("row"))
                return null;

            var parts = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1)
                return null;

            text = parts[0].Trim('(', ')');

            long? rows = null;

            try {
                rows = long.Parse(text);
            }
            catch { }

            return rows;
        }

        private string FormatDate(DateTime? date) {
            if (!date.HasValue) {
                return "All";
            }
            var dt = date.Value;
            var text = $"{dt:yyyy-MM-dd HH:mm:ss}";
            return text;
        }

        private bool CreateNewSearch(SearchMC search, float? percent, bool detectSubSearches, int? parentSearchId = null) {
            //SendNotification(percent);
            //we are in the Home folder of the searches

            if (SearchExists(search, parentSearchId)) {
                Info($"Subsearch exists: {JsonConvert.SerializeObject(search)}");
                return true;
            }

            var mcExistingSubSearches = GetExistingMcSearchCount();
            if (!mcExistingSubSearches.HasValue) {
                throw new A2ABreakingException($"Could not read existing search count from Mimecast");
            }
            int dbExistingSubSearches = _db.GetSubSearchCount();
            Info($"Total searches: Mimecast {mcExistingSubSearches}; dabatase: {dbExistingSubSearches}");

            if (mcExistingSubSearches.Value != dbExistingSubSearches) {
                throw new A2ASearchCountMismatch($@"The number of searches in Mimecast folder is different than the number of SubSearches in database (see above).
Please investigate the latest generated search and if needed, manually delete the searches that do not appear in the database");
            }

            //        Info($@"==> Start creating new (sub)search:
            //{search.Name} [{FormatDate(search.BeginDate)} - {FormatDate(search.EndDate)}]");
            var srcName = search.Name;
            SwitchToFrame(1);

            Info($"Creating search {srcName}...");
            var newSearchButton = WaitElement(By.XPath("//*[contains(text(), 'New Search')]"), ElementCoditionTypes.ElementToBeClickable);

            newSearchButton.Click();
            Sleep();

            if (string.IsNullOrEmpty(srcName)) {
                //generate search name
                srcName = GenerateSearchName(search);
            }
            var txtSearchName = WaitElementById("description", ElementCoditionTypes.ElementToBeClickable);
            txtSearchName.SendKeys(srcName);
            Sleep(100);

            if (!string.IsNullOrEmpty(search.Email)) {
                var fromtext = ScrollToElement("fromtext");
                fromtext.SendKeys(search.Email);
                Sleep(100);
            }

            var op = ScrollToElement("bool");
            SelectElement opElem = new SelectElement(op);
            opElem.SelectByValue("OR");
            Sleep(100);

            if (!string.IsNullOrEmpty(search.Email)) {
                var totext = ScrollToElement("totext");
                totext.SendKeys(search.Email);
                Sleep(100);
            }

            if (!parentSearchId.HasValue) {
                _db.GetSearchByEmail(search.Email);
            }

            var allTime = true;
            if (search.BeginDate.HasValue) {
                allTime = false;
                //select begin date: calendarPickerLabel-startDate
                var s = search.BeginDate.Value.ToString("yyyy-MM-dd HH:mm");
                var beginDate = WaitElementByClassName("calendarPickerLabel-startDate", ElementCoditionTypes.ElementIsVisible);
                Driver.ExecuteScript($"arguments[0].innerHTML = '{s}';", beginDate);
                SetValue("ttFromDate", s);
            }
            if (search.EndDate.HasValue) {
                allTime = false;
                //select end date
                var dt = search.EndDate.Value;
                var s = dt.ToString("yyyy-MM-dd HH:mm");
                var endDate = WaitElementByClassName("calendarPickerLabel-endDate", ElementCoditionTypes.ElementIsVisible);
                Driver.ExecuteScript($"arguments[0].innerHTML = '{s}';", endDate);
                SetValue("ttToDate", s);
            }

            if (allTime) {
                var calPicker = WaitElementByClassName("calendarPickerLabel-to-label", ElementCoditionTypes.ElementExists);
                calPicker.Click();
                Sleep(100);
                var calSelect = WaitElementByClassName("gwt-ListBox", ElementCoditionTypes.ElementIsVisible);
                var calSelectElem = new SelectElement(calSelect);
                calSelectElem.SelectByValue("All Time");
                var applyButton = WaitElementByClassName("gwt-Button", ElementCoditionTypes.ElementExists);
                applyButton.Click();
            }
            Sleep(100);

            var orderBy = WaitElementById("orderby", ElementCoditionTypes.ElementExists);
            var orderBySelect = new SelectElement(orderBy);
            orderBySelect.SelectByValue("datea");
            Sleep(100);

            var legalhold = WaitElementById("legalhold", ElementCoditionTypes.ElementExists);
            ScrollIntoView(legalhold);

            if (IncludeLegalHoldMessages) {
                if (!legalhold.Selected)
                    legalhold.Click();
            }
            else {
                if (legalhold.Selected)
                    legalhold.Click();
            }
            Sleep();

            if (detectSubSearches) {
                var totalCount = WaitElementById("totalCount", ElementCoditionTypes.ElementExists);
                if (!totalCount.Selected) {
                    totalCount.Click();
                    Sleep();
                }
                GetMainButton("Search", true);
                Sleep(100);

                var subSearches = DetectSubSearchesWithRetry(search, Options.MaxResultsPerExport ?? 50000);
                _db.UpdateSearchItemsCount(parentSearchId, search.ItemsCount);
                if (subSearches != null && subSearches.Count > 0) {
                    Info($"Sub searches found for search {search.Name}: {subSearches.Count}");
                    foreach (var subSearchMc in subSearches) {
                        //Driver.Navigate().Refresh();
                        GotoHomeFolderWithRetry();
                        CreateNewSearch(subSearchMc, percent, true, parentSearchId);
                    }
                    Info($"All sub searches created for search {search.Name}");
                    return true;
                }
                else {
                    //var subSearch = new SubSearch();
                    //search.Fill(subSearch);
                    //subSearch.StatusId = SearchStatusEnum.SearchCreated;
                    //_db.InsertOrUpdate(subSearch, parentSearchId);
                }
                Sleep();
                GetMainButton("Go Back", true);
            }

            Sleep();
            WaitDocumentReady();

            var saveExit = GoBack(new TimeSpan(0, 1, 0));
            if (saveExit == null) {
                throw new McInvalidStateException();
            }

            var fileName = $"{search.Name}_";
            SaveScreenshot(fileName + "p1");
            Driver.ExecuteScript("window.scrollTo(0, document.body.scrollHeight)");
            SaveScreenshot(fileName + "p2");

            saveExit.Click();

            var subSearch = new SubSearch();
            search.Fill(subSearch);
            subSearch.StatusId = SearchStatusEnum.SearchCreated;
            _db.InsertOrUpdate(subSearch, parentSearchId);

            Sleep();

            var okDiv = WaitElementByClassName("ui-dialog-buttonset", ElementCoditionTypes.ElementIsVisible);
            var buttons = okDiv.FindElements(By.TagName("button"));
            //dismiss a dialog box:
            buttons[0].Click();
            Sleep();

            search.ExportStatus = ExportStatus.SearchCreated;

            SwitchToParentFrame();

            Info($"Created (sub)search {search.Name}");
            Sleep(3000);

            //src = new Search();
            //search.Fill(src, ExportFormat);
            //_db.UpdateSearch(src);

            return true;
        }

        private string GenerateSearchName(SearchMC search) {
            if (search == null)
                return null;
            if (!string.IsNullOrEmpty(search.Name))
                return search.Name;
            var name = string.Empty;
            if (search.BeginDate.HasValue) {
                name = $"{search.BeginDate.Value:yyyyyMMdd-HHmmss}";
            }
            if (search.EndDate.HasValue) {
                if (!string.IsNullOrEmpty(name))
                    name += "_";
                name = $"{name}{search.EndDate.Value:yyyyyMMdd-HHmmss}";
            }
            if (string.IsNullOrEmpty(name)) {
                name = "all";
            }
            return name;
        }

        private int? GetExistingMcSearchCount() {
            //just reads the count from the search page
            Driver.SwitchTo().Frame(1);
            var adconRowCount = WaitElementByClassName("adconRowCount", ElementCoditionTypes.ElementIsVisible);
            var text = adconRowCount.Text;
            var count = ParseTotalCount(text);
            Driver.SwitchTo().ParentFrame();
            return count;
        }

        private IWebElement GoBackToSearchDefinition() {
            return GetMainButton("Go Back", true);
        }

        private IWebElement GoBack(TimeSpan timeout) {
            Info($"Going back with a timeout of {timeout}");
            IWebElement saveExit;
            var dtStart = DateTime.UtcNow;
            do {
                try {
                    saveExit = WaitElement(By.XPath("//*[contains(text(), 'Save and Exit')]"), ElementCoditionTypes.ElementToBeClickable);
                    break;
                }
                catch {
                    Info("Allowing more time to Mimecast to respond");
                    GetMainButton("Go Back", true);
                    Sleep();
                }
                if (DateTime.UtcNow.Subtract(dtStart) > timeout) {
                    Info("Could not go back. Timeout elapsed");
                    return null;
                }
            } while (true);
            Info("Go back done");
            return saveExit;
        }

        private bool WaitDocumentReady() {
            var exception = false;
            do {
                exception = false;
                try {
                    var readyState = Driver.ExecuteScript("return document.readyState");
                    var state = readyState != null ? readyState.ToString() : "";
                    while (state != "complete") {
                        Info($"Waiting for document to be ready. Current state: {readyState}");
                        Sleep();
                        readyState = Driver.ExecuteScript("return document.readyState");
                        state = readyState != null ? readyState.ToString() : "";
                    }
                    break;
                }
                catch (UnhandledAlertException ex) {
                    exception = true;
                    Info($"WARN alert: {ex.Message}");
                    Sleep();
                }
            } while (true);
            return exception;
        }

        /// <summary>
        /// this method checks if a search exists and synchronizes it with the database
        /// </summary>
        /// <param name="subSearchMc"></param>
        /// <returns></returns>
        private bool SearchExists(SearchMC search, int? parentId) {
            //Driver.Navigate().Refresh();
            //GotoHomeFolder();

            //Info($"Verifying search: {search.Name}");
            //var srcName = search.Name;
            //SwitchToFrame(1);

            //var searchElem = WaitElementById("SysSearch", ElementCoditionTypes.ElementIsVisible);

            //SetValue(searchElem, srcName);

            //searchElem.Click();

            var searchExistsInMimecast = false; //TODO: check the search existence in Mimecast and set this flag!

            if (searchExistsInMimecast) {
                return true;
            }

            if (parentId.HasValue) {
                _db.InsertSubSearchIfNotExists(search, parentId.Value);
            }
            else {
                _db.InsertSearchIfNotExists(search, ExportFormat);
            }

            return false; //search does not exist
        }

        private IWebElement GetMainButton(string text, bool click) {
            var dtStart = DateTime.UtcNow;
            var tsDuration = new TimeSpan(0, 1, 0);
            do {
                try {
                    var mainButtons = Driver.FindElements(By.ClassName("adconAction"));
                    foreach (var mainButton in mainButtons) {
                        if (mainButton.Text == text) {
                            if (click)
                                mainButton.Click();
                            return mainButton;
                        }
                    }
                }
                catch { }
                if (DateTime.UtcNow.Subtract(dtStart) > tsDuration)
                    throw new ApplicationException($"Button not found: {text}");
            } while (true);
            throw new ApplicationException($"Button not found: {text}");
        }
        private void ResumeExportAfterDownload(ExportMC export) {
            Info($"Resuming export {export.Name}");
            ////*[contains(text(), 'My Button')]

            bool completed = false;
            IWebElement generateButton;
            try {
                generateButton = WaitElement(By.XPath("//*[contains(text(), 'Generate Next Export Block')]"), ElementCoditionTypes.ElementToBeClickable);
                Info($"Start next block {export.Name}");
            }
            catch {
                generateButton = WaitElement(By.XPath("//*[contains(text(), 'Mark Completed')]"), ElementCoditionTypes.ElementToBeClickable);
                completed = true;
                Info($"Export COMPLETED {export.Name}");
            }

            generateButton.Click();

            export.ExportStatus = completed ? ExportStatus.ExportCompleted : ExportStatus.Preparing;

            Sleep(5 * 1000);
            //WaitElementByClassName("fa fa-bars", ElementCoditionTypes.ElementExists);
        }

        private bool DownloadExportWithRetry(string downloadFolder, out ExportMC export) {
            var retry = 3;
            while (retry-- > 0) {
                try {
                    Driver.SwitchTo().Frame(1);
                    return DownloadExport(downloadFolder, out export);
                }
                catch (Exception ex) {
                    var dfe = ex as DownloadFailedException;
                    if (dfe != null) {
                        Error(dfe.Message);
                        Info($"Giving up to download this file. See error details above.");
                        _db.UpdateSubSearchFile(dfe.SubSearchFile.SubSearchId.Value, null, dfe.SubSearchFile.McOriginalFileName, null, true);
                    }
                    Info($"WARN: Cannot read from Mimecast. Details: {ex.Message}");
                    Info($"Refreshing the data. Retries left: {retry}");
                }
            }
            throw new ApplicationException("Could not perform the download. Read above for more details on the error");
        }

        private void RemoveDownloadedFiles(SubSearchFile[] files, string downloadFolder) {
            if (files == null)
                return;
            foreach (var file in files) {
                RemoveDownloadedFile(file, downloadFolder);
            }
        }
        private void RemoveDownloadedFile(SubSearchFile file, string downloadFolder) {
            //delete potential partially downloaded file
            var filePathToDelete = Path.Combine(downloadFolder, file.McOriginalFileName);
            filePathToDelete += FileExtension;
            if (File.Exists(filePathToDelete)) {
                Info($"Deleting file {filePathToDelete}");
                try {
                    File.Delete(filePathToDelete);
                }
                catch (Exception ex) {
                    Error($"Could not delete file {filePathToDelete}: {ex.Message}");
                }
            }
        }

        private bool DownloadExport(string downloadFolder, out ExportMC export) {
            //var tr = FindExport(out export);
            var tr = UpdateExports(out export, out _, true, 500);

            if (tr == null || export == null) {
                Info("No export part to download");
                return false;
            }

            bool downloadedOK = true;

            var index = 0;
            var subSearchId = export.SubSearchId.Value;
            var downloads = GetAllExportDownloads(tr, true);

            var chunkFiles = GetDownloadFiles(downloads, export.Name);
            var newChunkFiles = _db.AddSubSearchFiles(chunkFiles, subSearchId);
            foreach (var chunk in newChunkFiles) {
                //transfer tags
                var tChunk = chunkFiles.FirstOrDefault(c => c.McOriginalFileName == chunk.McOriginalFileName);
                if (tChunk != null)
                    chunk.Tag = tChunk.Tag;
            }

            //Info($"Found {downloads.Length} files for export {export.Name}");
            Info($"Detected {downloads.Length} files to download from Mimecast for export {export.Name}");

            while (index < downloads.Length) {
                downloads = GetAllExportDownloads(null, true);
                chunkFiles = GetDownloadFiles(downloads, export.Name);
                var f = newChunkFiles[index];
                if (f.DownloadDate.HasValue) {
                    Info($"File was already downloaded. Skipping: {f.McOriginalFileName}. Download date: {f.DownloadDate}. Path: {f.DownloadPath}");
                    _db.SetSubSearchStatus(f.SubSearch.Name, null, out _);
                    index++;
                    continue;
                }
                if (f.DownloadError) {
                    Info($"WARN: Previous file download failed. Skipping file: {f.McOriginalFileName}");
                    UpdateExportStatus(export, out _);
                    index++;
                    continue;
                }
                var download = downloads[index];
                var downloadText = download.Text;
                Info($"Downloading file {index + 1} of {downloads.Length}");

                //RemoveDownloadedFile(download);
                var filesBefore = GetNewDownloadedFiles(DownloadFolderPath, null);
                _lastDownloadStart = StartDownloadExport(export, download);

                SaveScreenshot("Download_" + export.Name);
                //WaitAlert(new TimeSpan(0, 1, 0));

                var filesAfter = GetNewDownloadedFiles(DownloadFolderPath, filesBefore);

                Info($"Waiting to download the file {index + 1} of {downloads.Length} for export {export.Name}");
                while (filesAfter.Length < 1) {
                    Sleep();
                    filesAfter = GetNewDownloadedFiles(DownloadFolderPath, filesBefore);
                }
                var files = string.Join(Environment.NewLine, filesAfter);
                Info($"File(s) downloading:{Environment.NewLine}{files}");
                Info(downloadText);

                //now we've got one file, need to test if it completed or not
                var fileName = filesAfter[0];
                var crtDownloadOK = WaitUntilDownloadCompletes(f, fileName, out long size);
                Info($"Download success: {crtDownloadOK}");

                DateTime? downloadDate = null;
                string downloadFilePath = null;

                EnsureWindowSize();

                if (crtDownloadOK) {
                    var dtNow = DateTime.Now;
                    var mbps = AutomationDriverBase.ComputeTransferRateMBps(_lastDownloadStart, dtNow, size);
                    Info($"[{fileName}] [{dtNow.Subtract(_lastDownloadStart)}] [{mbps}MB/sec]");
                    downloadDate = dtNow;
                    downloadFilePath = Path.Combine(DownloadFolderPath, fileName);

                    //Info("Processing downloaded file");
                    //try {
                    //    UpdateExcelReport(fileName, export);
                    //}
                    //catch (Exception ex) {
                    //    Info($"WARN: cannot update Excel report. Please make sure the ODBC (32 or 64) DNS Extraction_worksheet exists and is not Read Only. Details: {ex.Message}");
                    //}
                }
                else {
                    Info($"[Fail] Could not download file: {fileName}. The download will be retried");
                    downloadedOK = false;
                    break;
                }
                if (index < downloads.Length)
                    downloads = GetAllExportDownloads(tr, false);

                var fileNameNoExt = Path.GetFileName(downloadFilePath);
                fileNameNoExt = Path.GetFileNameWithoutExtension(fileNameNoExt);

                f = _db.UpdateSubSearchFile(subSearchId, downloadFilePath, fileNameNoExt, downloadDate, false);
                if (f != null) {
                    Info($"[Success] Downloaded file: {downloadFilePath}");
                }
                else {
                    Error($"[Fail] Coud not identify in the database a download file ({nameof(SubSearchFile)}) that corresponds to the file name {fileName}. Please investigate");
                }
                index++;
            }

            var strSuccess = downloadedOK ? "Success" : "Fail";
            Info($"[{strSuccess}] Export: [{export.Name}] Files: {downloads.Length}");

            return downloadedOK;
        }

        private void RemoveDownloadedFile(IWebElement download) {
            var files = GetDownloadFilesWithRetry(new IWebElement[] { download }, null);

            foreach (var file in files) {
                var filePathToDelete = Path.Combine(DownloadFolderPath, file.McOriginalFileName);
                filePathToDelete += FileExtension;
                if (File.Exists(filePathToDelete)) {
                    Info($"Deleting file: {filePathToDelete}");
                    try {
                        File.Delete(filePathToDelete);
                        Info($"Downloaded file deleted: {filePathToDelete}");
                    }
                    catch (Exception ex) {
                        Error($"Could not delete file {filePathToDelete}: {ex.Message}");
                    }
                }
            }
        }

        private SubSearchFile[] GetDownloadFilesWithRetry(IWebElement[] webElements, object p) {
            while (true) {
                try {
                    var files = GetDownloadFiles(webElements, null);
                    if (files.Length != webElements.Length) {
                        throw new ApplicationException($"Invalid number of files detected. Expected: {webElements.Length}, detected: {files.Length}");
                    }
                    return files;
                }
                catch (Exception ex) {
                    Error(ex.Message);
                    Info($"[Retry] Detect downloads");
                }
            }
        }

        private SubSearchFile[] GetDownloadFiles(IWebElement[] downloads, string searchName) {
            var list = new List<SubSearchFile>();
            if (!string.IsNullOrEmpty(searchName))
                Info($"Listing the files to download for search {searchName}");
            int i = 0;
            foreach (var elem in downloads) {
                SubSearchFile file = ParseSubSearchFile(elem);
                file.Tag = elem;
                list.Add(file);
                i++;
                Info($"File {i}: {file.McOriginalFileName}");
            }
            return list.OrderBy(d => d.McOriginalFileName).ToArray();
        }

        private SubSearchFile ParseSubSearchFile(IWebElement elem) {
            var errorMessage = $"Cannot find the file name in Mimecast. Please investigate.";
            var tds = elem.FindElements(By.TagName("td"));
            if (tds == null || tds.Count < 1)
                throw new ApplicationException(errorMessage);
            var td1 = tds[0];
            var file = new SubSearchFile();
            if (td1 == null)
                throw new ApplicationException(errorMessage);
            var span = td1.FindElement(By.TagName("span"));
            if (span == null)
                throw new ApplicationException(errorMessage);
            file.McOriginalFileName = span.Text;
            file.McBatchNumber = TryParseInt(tds[1].Text);
            file.McCreateTime = TryParseDateTime(tds[2].Text);
            file.McExpiryDate = TryParseDateTime(tds[3].Text);
            file.McNumberOfMessages = TryParseInt(tds[4].Text);
            file.McFailedMessages = TryParseInt(tds[5].Text);
            file.McFileSizeBytesApprox = TryParseSize(tds[6].Text);
            file.DiscoveredDate = DateTime.Now;

            return file;
        }

        private long? TryParseSize(string text) {
            if (string.IsNullOrEmpty(text))
                return null;
            var parts = text.Split(' ');
            if (parts.Length < 1)
                return TryParseLong(text);
            var size = TryParseFloat(parts[0]);
            if (!size.HasValue)
                return null;
            var unit = parts[1].Trim().ToLower();
            var factor = 0f;
            var units = new List<string>(new string[] { "b", "kb", "mb", "gb", "tb" });
            var index = units.IndexOf(unit);
            if (index < 0)
                return null;
            factor = (long)Math.Pow(1024, index);
            size = size.Value * factor;
            return (long)size;
        }

        private float? TryParseFloat(string text) {
            float? value = null;
            if (float.TryParse(text, out var i))
                return i;
            text = text.Replace('.', ',');
            if (float.TryParse(text, out i))
                return i;
            text = text.Replace(',', '.');
            if (float.TryParse(text, out i))
                return i;
            return value;
        }

        private static int? TryParseInt(string text) {
            int? value = null;
            if (int.TryParse(text, out var i))
                value = i;
            return value;
        }

        private static long? TryParseLong(string text) {
            long? value = null;
            if (long.TryParse(text, out var i))
                value = i;
            return value;
        }

        private static DateTime? TryParseDateTime(string text) {
            DateTime? value = null;
            if (DateTime.TryParse(text, out var i))
                value = i;
            return value;
        }

        private IWebElement[] GetAllExportDownloads(IWebElement trMain, bool clickTr) {
            if (clickTr && trMain != null) {
                var td = trMain.FindElements(By.TagName("td"))[ExportIndexSearchName];
                td.Click();
                _ = WaitElementById("v2Download", ElementCoditionTypes.ElementExists);
            }
            var stdTable = WaitElementByClassName("StdTable", ElementCoditionTypes.ElementIsVisible);
            var trs = stdTable.FindElements(By.TagName("tr"));
            var downloads = new List<IWebElement>();
            foreach (var tr in trs) {
                var className = tr.GetAttribute("class");
                if (string.IsNullOrEmpty(className))
                    continue;
                className = className.ToLower();
                switch (className) {
                    case "stdtablerow1":
                    case "stdtablerow2":
                    case "stdtablemouseover":
                        downloads.Add(tr);
                        break;
                }
            }
            //var downloads1 = Driver.FindElementsByClassName("StdTableRow1");
            //var downloads2 = Driver.FindElementsByClassName("StdTableRow2");
            //downloads.AddRange(downloads1);
            //downloads.AddRange(downloads2);
            return downloads.ToArray();
        }

        private DateTime StartDownloadExport(ExportMC export, IWebElement download) {
            EnsureWindowSize();
            var downloadButton = download.FindElement(By.Id("v2Download"));
            downloadButton.Click();
            var dt = DateTime.Now;
            Sleep();
            return dt;
        }

        public static int ZipFileCount(String zipFileName, out long size) {
            size = 0;
            var count = 0;
            using (var zip = new ZipFile(zipFileName)) {
                foreach (ZipEntry zipEntry in zip) {
                    size += zipEntry.Size;
                    count++;
                }
            }
            return count;
            //using (var archive = ZipFile.Open(zipFileName, ZipArchiveMode.Read)) {
            //    var count = archive.Entries.Count(x => !string.IsNullOrWhiteSpace(x.Name));
            //    size = archive.Entries.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Sum(x => x.Length);
            //    return count;
            //}
        }

        private void UpdateExcelReport(string fileName, ExportMC export) {
            if (string.IsNullOrEmpty(fileName) || export == null)
                return;
            if (!File.Exists(fileName))
                return;

            var size = 0L;
            int? items = null;
            switch (ExportFormat) {
                case ExportFormatCode.EML:
                    items = ZipFileCount(fileName, out size);
                    break;
                default:
                    break;
            }

            var data = new RecordData() {
                Custodian = export.Name,
                FileSize = (new FileInfo(fileName)).Length,
                Date = DateTime.Now,
                ExtractedItems = items,
                DataSize = size
            };
            var excel = new ExcelReporterHelper();
            excel.AddRecord(data);
            //excel.UpdateDailySummary(data.Date);
        }

        private bool WaitUntilDownloadCompletes(SubSearchFile file, string filePath, out long size) {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var partFile = string.Empty;
            if (SmtpScenario) {
                var fileNameParts = fileName.Split('.');
                partFile = Directory.GetFiles(DownloadFolderPath, "*.part*")
                    .SingleOrDefault(f => f.Contains(fileNameParts[0]) && f.Contains(fileNameParts[1]) && f.Contains(fileNameParts[2]));
            }
            else {
                partFile = Directory.GetFiles(DownloadFolderPath, "*.part*")
                    .SingleOrDefault(f => f.Contains(fileName));
            }
            size = 0;
            var tsWait = new TimeSpan(0, 30, 0);
            var dtStart = DateTime.Now;
            while (true) {
                Sleep();
                var tsDuration = DateTime.Now.Subtract(dtStart);
                if (tsDuration > tsWait) {
                    throw new DownloadFailedException(file, $"Download timed out after {tsDuration}");
                }
                try {
                    if (File.Exists(partFile)) continue;
                    using (var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None)) { fs.Close(); }
                    var fi = new FileInfo(filePath);
                    size = fi.Length;
                    if (fi.Length < 1) {
                        File.Delete(filePath);
                        return false;
                    }
                    return true;
                }
                catch { }
            }
        }

        private string[] GetNewDownloadedFiles(string downloadFolder, string[] existingFiles) {
            int nonZipFiles;
            Directory.CreateDirectory(downloadFolder);
            var allFiles = Directory.GetFiles(downloadFolder);
            var zipFiles = allFiles.Where(fileName => Path.GetExtension(fileName).ToLower() == FileExtension).ToArray();
            nonZipFiles = allFiles.Length - zipFiles.Length;

            if (existingFiles != null && nonZipFiles < 1) {
                //allow max 30 seconds for download to start
                var timeSinceDownloadStart = DateTime.Now.Subtract(_lastDownloadStart);
                if (timeSinceDownloadStart > _downloadStartTimeout)
                    throw new ApplicationException($"Download could not be started");
            }

            if (existingFiles == null)
                return zipFiles;
            zipFiles = zipFiles.Except(existingFiles).ToArray();
            return zipFiles;
        }

        private void CreateExport(SearchMC exp) {
            //var newSearch = WaitElementByClassName("", ElementCoditionTypes.ElementToBeClickable);
            var buttons = Driver.FindElementsByClassName("adconAction");
            var newSearch = buttons.FirstOrDefault(b => b.Text.ToLower() == "new search");
            newSearch.Click();
        }

        private IWebElement UpdateExports(out ExportMC export, out Dictionary<ExportMC, int> slotsInUse, bool forDownload = false, int itemsPerPage = 100) {
            slotsInUse = new Dictionary<ExportMC, int>();
            try {
                Driver.SwitchTo().DefaultContent();
                SwitchToFrame(1);
            }
            catch { }

            //WaitElementById("SysSearch", ElementCoditionTypes.ElementIsVisible);
            //var sysDepth = WaitElementById("SysDepth", ElementCoditionTypes.ElementExists);
            //if (sysDepth != null) {
            //    //display 1000 exports per page
            //    var optSysDepth = new SelectElement(sysDepth);
            //    optSysDepth.SelectByValue(itemsPerPage.ToString());
            //}
            //Sleep(2 * 1000);

            var trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

            var header = true;

            List<ExportMC> traversed = new List<ExportMC>(Exports.Count);

            var index = 0;
            var traversedRows = 0;
            var totalRows = trs.Count;
            if (header)
                totalRows -= 1;
            Info($"Analyzing a page of {totalRows} rows");
            EnsureWindowSize();
            foreach (var tr in trs) {
                if (header) {
                    header = false;
                    continue;
                }

                string name = null;

                var tds = tr.FindElements(By.TagName("td"));
                var tdName = tds[2];
                var nameElem = tdName.FindElement(By.TagName("span"));
                if (nameElem != null)
                    name = nameElem.GetAttribute("title");
                if (string.IsNullOrEmpty(name)) {
                    name = tdName.Text;
                }

                traversedRows++;
                if ((traversedRows % 100) == 0) {
                    Info($"Traversed rows: {traversedRows}/{totalRows}");
                }

                if (string.IsNullOrEmpty(name)) {
                    Info($"WARN! Empty name detected!");
                    continue;
                }

                //var existing = (ExportMC)Exports.FirstOrDefault(e => e.Name == name);
                var subSearch = _db.GetDownloadableExportByName(name);

                if (forDownload && subSearch == null)
                    continue;

                //debug ONLY
                //TODO: REMOVE/COMMENT LINE:
                //if (subSearch.Id != 21)
                //    continue;

                //verify that the export details match the export we found in database
                ExportMC exportDetails = ParseExportDetails(tr);
                ExportMC existing = null;

                if (forDownload) {
                    existing = subSearch.GetExportMC();
                    existing.ExportFormat = ExportFormat;

                    if (existing.CompareTo(exportDetails) != 0) {
                        continue;
                    }

                    existing.Index = ++index;

                    if (!traversed.Contains(existing)) {
                        existing.ExportStatus = ExportStatus.None;
                        traversed.Add(existing);
                    }
                }
                else {
                    existing = exportDetails;
                }

                //var existing = new ExportMC();
                var i = 0;
                ExportStatus? actualStatus = null;
                foreach (var td in tds) {
                    var value = td.Text;
                    switch (i++) {
                        case ExportIndexCreatedBy:
                            existing.CreatedBy = value;
                            break;
                        case ExportIndexFormat:
                            existing.Type = value;
                            break;
                        case ExportIndexCreated:
                            existing.Created = value;
                            break;
                        case ExportIndexPrepared:
                            existing.Prepared = value;
                            break;
                        case ExportIndexCompleted:
                            existing.Completed = value;
                            break;
                        case ExportIndexRemaining:
                            existing.Remaining = value;
                            break;
                        case ExportIndexStatus:
                            //status
                            var img = td.FindElement(By.TagName("img"));
                            if (img == null) break;
                            var src = img.GetAttribute("src").ToLower();
                            if (src.EndsWith("red.gif"))
                                existing.ExportStatus = ExportStatus.ActiveExport;
                            if (src.EndsWith("green.gif")) {
                                actualStatus = ExportStatus.ExportCompleted;
                                if (!ExportMC.ExportsRunningStatuses.Contains(existing.ExportStatus))
                                    existing.ExportStatus = ExportStatus.Downloadable;
                            }
                            if (src.EndsWith("download.gif"))
                                existing.ExportStatus = ExportStatus.Downloadable;
                            if (src.EndsWith("blue.gif"))
                                existing.ExportStatus = ExportStatus.Preparing;
                            if (src.EndsWith("orange.gif"))
                                existing.ExportStatus = ExportStatus.PreparationPending;
                            if (src.EndsWith("delete.gif")) {
                                actualStatus = ExportStatus.ExportCanceled;
                                if (!existing.IsRunning())
                                    existing.ExportStatus = ExportStatus.ExportCanceled;
                            }
                            break;
                    }
                }

                if (forDownload && existing.ExportStatus == ExportStatus.Downloadable) {
                    export = existing;
                    return tr;
                }

                if (ExportMC.ExportsRunningStatuses.Contains(actualStatus ?? existing.ExportStatus)) {
                    if (!slotsInUse.ContainsKey(existing)) {
                        slotsInUse.Add(existing, 0);
                    }
                    slotsInUse[existing]++;
                    //slotsInUse.Add(existing);
                }
            }
            SwitchToParentFrame();
            export = null;
            return null;
        }

        private ExportMC ParseExportDetails(IWebElement trMain) {
            var export = new ExportMC();
            var tds = trMain.FindElements(By.TagName("td"));
            var index = 0;
            foreach (var td in tds) {
                var text = td.Text;
                switch (index++) {
                    case 2:
                        string name = null;
                        try {
                            var span = td.FindElement(By.TagName("span"));
                            if (span != null)
                                name = span.GetAttribute("title");
                        }
                        catch { }
                        if (string.IsNullOrEmpty(name))
                            name = td.Text;
                        export.Name = name;
                        break;
                    case 4:
                        export.CreatedBy = td.Text;
                        break;
                    case 5:
                        if (Enum.TryParse<ExportFormatCode>(text.Replace(" ", string.Empty), out var format))
                            export.ExportFormat = format;
                        break;
                    case 6:
                        export.Created = text;
                        break;
                    case 8:
                        if (int.TryParse(text, out var count))
                            export.ItemsCount = count;
                        return export;
                    default:
                        break;
                }
            }

            return export;
        }

        //private int UpdateSearches() {
        //    var changes = 0;
        //    SwitchToFrame(1);

        //    WaitElementById("SysSearch", ElementCoditionTypes.ElementIsVisible);
        //    Sleep(2 * 1000);

        //    var trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

        //    var header = true;

        //    foreach (var tr in trs) {
        //        if (header) {
        //            header = false;
        //            continue;
        //        }
        //        var tds = tr.FindElements(By.TagName("td"));
        //        var name = tds[1].Text;
        //        var existing = (SearchMC)Searches.FirstOrDefault(e => e.Name == name);
        //        if (existing == null) continue;
        //        List<string> fields = new List<string>();

        //        int index = 0;
        //        foreach (var td in tds) {
        //            var value = td.Text;
        //            switch (index++) {
        //                case 2:
        //                    existing.Search = value;
        //                    break;
        //                case 3:
        //                    existing.From = value;
        //                    break;
        //                case 4:
        //                    existing.To = value;
        //                    break;
        //                case 5:
        //                    existing.Start = value;
        //                    break;
        //                case 6:
        //                    existing.End = value;
        //                    break;
        //                case 7:
        //                    //status
        //                    var img = td.FindElement(By.TagName("img"));
        //                    if (img == null) break;
        //                    var src = img.GetAttribute("src").ToLower();
        //                    if (src.EndsWith("red.gif"))
        //                        existing.ExportStatus = ExportStatus.ActiveExport;
        //                    if (src.EndsWith("green.gif"))
        //                        existing.ExportStatus = ExportStatus.ExportCompleted;
        //                    break;
        //            }
        //        }
        //    }

        //    SwitchToParentFrame();
        //    return changes;
        //}

        private void GotoExportsPage() {
            var methodByUrl = false;
            if (methodByUrl) {
                Info("Fetching exports");
                GoToExportsPageByUrl();
            }
            else {
                try {
                    GotoExportsPageByClick();
                }
                catch {
                    RefresshExportsPage();
                }
            }
            Sleep(10 * 1000);
            //else
            //    Driver.Navigate().Refresh();
            //Sleep(2 * 1000);
        }

        private void GoToExportsPageByUrl() {
            if (Driver.Url.ToLower() != Options.ExportsPageUrl.ToLower()) {
                Driver.Url = Options.ExportsPageUrl;
                Driver.Navigate();
            }
            else {
                RefresshExportsPage();
            }
        }

        private void GotoExportsPageByClick() {
            var menu = WaitElementByClassName("mc-navbar-item", ElementCoditionTypes.ElementToBeClickable);
            menu.Click();
            var subMenu = Driver.FindElementsByClassName("mc-menu-label")[1];
            subMenu.Click();
            var subMenus = Driver.FindElementsByClassName("ng-binding");
            foreach (var option in subMenus) {
                if (option.Text.ToLower().Contains("exports and")) {
                    option.Click();
                    break;
                }
            }
        }

        private void GotoHomeFolderWithRetry(bool switchFrame = true) {
            while (true) {
                try {
                    GotoHomeFolder(switchFrame);
                    return;
                }
                catch (Exception ex) {
                    Error("Could not access Mimecast home API. Retrying.");
                    Error($"Details: {ex.Message}");
                }
                TryLogout();
                Authenticate();
            }
        }

        private void TryLogout() {
            SwitchToParentFrame();
            Driver.Manage().Cookies.DeleteAllCookies();
        }

        private void GotoHomeFolder(bool switchFrame = true) {
            Info($"Accessing home folder {Options.HomeSearchFolder}");
            if (Driver.Url != Options.SavedSearchesUrl) {
                Driver.Url = Options.SavedSearchesUrl;
                Driver.Navigate();
            }
            else {
                Driver.Navigate().Refresh();
            }
            WaitForLoadingSearchHome();

            WaitElementById("SysSearch", ElementCoditionTypes.ElementExists); //wait for page to finish loading
            var folder = FindHomeNode();
            if (folder == null) {
                throw new ApplicationException($"Home folder not specified or not found {Options.HomeSearchFolder}");
            }
            Info("Home folder found, opening");
            Sleep();
            folder.Click();
            WaitElementById("foldernamechange", ElementCoditionTypes.ElementIsVisible); //wait for folder to finish loading
            WaitForLoadingHomeFolder();
            if (switchFrame)
                SwitchToParentFrame();
        }

        private void WaitForLoadingHomeFolder() {
            WaitElementByClassName("adconAction", ElementCoditionTypes.ElementExists);
        }

        private void WaitForLoadingSearchHome(int retyCount = 3) {
            if (retyCount < 1)
                retyCount = 3;
            do {
                try {

                    Driver.SwitchTo().Frame(1);
                }
                catch (NoSuchFrameException) {
                    if (retyCount <= 0)
                        throw;
                    Sleep();
                    continue;
                }
                WaitElementById("SysSearch", ElementCoditionTypes.ElementIsVisible);
                return;
            } while (retyCount-- > 0);
        }

        private bool ApplySearchFilterAndLaunchExport(string text, ExportFormatCode exportFormatCode) {
            //text = "Alyson.Carstens@kiewit.com";
            //Sleep(5 * 1000);
            WaitForLoading();
            SwitchToFrame(1);
            //if (DismissAlert()) {
            //    Driver.SwitchTo().DefaultContent();
            //    Driver.SwitchTo().Frame(1);
            //}
            var searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            searchBox.SendKeys(text);
            Sleep();
            searchBox = WaitElementById("SysSearch", ElementCoditionTypes.ElementToBeClickable);
            searchBox.SendKeys(Keys.Enter);
            Sleep(5 * 1000);
            var trs = Driver.FindElementByClassName("adconColumnList").FindElements(By.TagName("tr"));

            if (trs.Count != 2) {
                Error($"Search not found: {text}");
                return false;
            }
            var tr = trs[1];

            //get option icon
            try {
                var icon = tr.FindElements(By.TagName("td"))[0].FindElement(By.TagName("img"));
                icon.Click();
            }
            catch {
                var msg = $"Search not found/not defined: {text}";
                Error(msg);
                throw new ApplicationException(msg);
            }
            Sleep();

            var subMenus = Driver.FindElementsByClassName("SubMenu");

            subMenus[4].Click();
            Sleep();

            try {
                var expType = new SelectElement(WaitElementById("type", ElementCoditionTypes.ElementToBeClickable));
                var value = (int)exportFormatCode;
                expType.SelectByValue(value.ToString());
            }
            catch {
                string alertMessage = GetSoftAlertText();
                if (string.IsNullOrEmpty(alertMessage))
                    throw;
                Info($"[Alert] {alertMessage}");
                //var alert = GetAlert();
                //if (alert != null) {
                //    Info($"[Alert] {alert.Text}");
                //    return false;
                //}
                return false;
            }
            Sleep();

            if (ExportFormat != ExportFormatCode.ExchangeJournal) {
                var includebcc = WaitElementById("includebcc", ElementCoditionTypes.ElementExists);
                if (IncludeBccRecipients) {
                    if (!includebcc.Selected) {
                        includebcc.Click();
                    }
                }
            }

            var txtName = WaitElementById("name", ElementCoditionTypes.ElementToBeClickable);
            txtName.SendKeys(text);

            var saveBtn = WaitElement(By.XPath($"//*[contains(text(), 'Save')]"), ElementCoditionTypes.ElementToBeClickable);

            SaveScreenshot(text);

            Info($"Launching export {text}, format: {exportFormatCode}");
            saveBtn.Click();

            WaitForLoading();
            WaitForLoading();
            return true;
        }

        private string GetSoftAlertText() {
            try {
                var alertid = WaitElementById("alertid", ElementCoditionTypes.ElementIsVisible);
                if (alertid != null)
                    return alertid.Text;
            }
            catch {
            }
            return null;
        }

        private void WaitForLoading() {
            Sleep(10 * 1000);
        }

        private IWebElement FindHomeNode() {
            var myNode = WaitElement(By.XPath($"//*[contains(text(), '{Options.HomeSearchFolder}')]"), ElementCoditionTypes.ElementToBeClickable);
            if (myNode != null) return myNode;
            var nodes = Driver.FindElementsByClassName("adconFolderText");
            var homeFolderLower = Options.HomeSearchFolder.ToLower().Trim();
            foreach (var node in nodes) {
                var text = node.Text.ToLower().Trim();
                if (homeFolderLower == text) {
                    return node;
                }
            }
            return null;
        }

        protected override void OnCredentialsNeeded() {
            //input credentials and hit login
            //returns the wait element that 
            //username
            WebDriverWait w = new WebDriverWait(Driver, new TimeSpan(0, 1, 0));
            //var username = Driver.FindElement(By.Id("username"));
            var username = w.Until(WaitHelpers.ExpectedConditions.ElementToBeClickable(By.Id("username")));
            username.Click();

            username.SendKeysSlow(UserName);
            Thread.Sleep(1000);
            username.SendKeys(Keys.Enter);

            w = new WebDriverWait(Driver, new TimeSpan(0, 0, 10));
            //var username = Driver.FindElement(By.Id("username"));
            var attempts = 10;
            var standardLogin = false;
            var dtStart = DateTime.UtcNow;
            var tsDuration = new TimeSpan(0, 1, 0);
            do {
                attempts--;
                try {
                    do {
                        try {
                            Info("Trying by type login...");
                            username = w.Until(WaitHelpers.ExpectedConditions.ElementToBeClickable(By.Id("type")));
                            standardLogin = false;
                            Info("Login by type initiated");
                            break;
                        }
                        catch (StaleElementReferenceException) { }
                        if (DateTime.UtcNow.Subtract(dtStart) > tsDuration)
                            throw new ApplicationException("Unable to recover, must restart");
                    } while (true);
                    break;
                }
                catch (WebDriverTimeoutException) {
                    Info("Login by type unavailable");
                }

                try {
                    Info("Trying standard login...");
                    username = w.Until(WaitHelpers.ExpectedConditions.ElementToBeClickable(By.Id("i0116")));
                    username.Click();
                    username.SendKeysSlow(UserName);
                    username.SendKeys(Keys.Enter);
                    standardLogin = true;
                    Info("Standard login initiated");
                    break;
                }
                catch (WebDriverTimeoutException) {
                    if (attempts < 0) throw;
                    Info("Standard login unavailable");
                }
            } while (true);
            Info($"Standard login: {standardLogin}");
            Thread.Sleep(1000);

            var password = w.Until(WaitHelpers.ExpectedConditions.ElementToBeClickable(By.Id("password")));

            password.Click();
            password.SendKeysSlow(Password);
            Thread.Sleep(1000);
            password.SendKeys(Keys.Enter);

            w = new WebDriverWait(Driver, new TimeSpan(0, 0, 5));
            var retry = 3;
            do {
                try {
                    Sleep(5000);
                    var c = Driver.SwitchTo().DefaultContent();
                    if (c.Title == "My Apps")
                        return;
                }
                catch { }

                try {
                    var signedIn = w.Until(WaitHelpers.ExpectedConditions.ElementExists(By.Id("KmsiCheckboxField")));
                    if (signedIn == null) {
                        continue;
                    }
                    //signedIn.Click();
                    signedIn.SendKeys(Keys.Enter);
                }
                catch {
                    w.Until(WaitHelpers.ExpectedConditions.ElementExists(By.ClassName("message-body-panel")));
                    break;
                }
                retry--;
                if (retry < 0)
                    throw new ApplicationException($"Authentication failed");
            } while (true);
        }

        protected override EntityBase ParseSearch(string line) {
            var exp = new SearchMC();
            var fields = line.Split('\t');
            exp.ParseLine(fields);
            return exp;
        }

        protected override EntityBase CreateSearch() {
            return new SearchMC();
        }

        protected override EntityBase CreateExport() {
            return new ExportMC();
        }

        public object Clone() {
            var d = this.MemberwiseClone();
            return d;
        }
    }
}
