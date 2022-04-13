using A2A.Option;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using WaitHelpers = SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using A2A.MC.Kernel.Entities;
using A2A.Logger;
using System.Drawing;
using System.Text.RegularExpressions;
using A2A.MC.Kernel;

namespace A2A.Automation.Base {
    public abstract class AutomationDriverBase : IDisposable, ILogProvider {

        public AutomationDriverBase(Options opt) {
            AuthenticationUrl = opt.AuthenticationUrl;
            UserName = opt.UserName;
            Password = opt.Password;
            ScenarioFilePath = opt.ScenarioFilePath;
            DownloadFolderPath = opt.DownloadFolderPath;
            LogsFolder = opt.LogsFolder;
            ActionType = opt.Action.ToString();
            ScenarioFileHasHeader = (bool)opt.ScenarioFileHasHeader;
            ReportsFolderPath = opt.ReportsFolderPath;
            ScreenshotsFolderPath = opt.ScreenshotsFolderPath;
            IncludeLegalHoldMessages = opt.IncludeLegalHoldMessages == true;
            ExportFormat = opt.ExportFormat ?? ExportFormatCode.EML;
            IncludeBccRecipients = opt.IncludeBccRecipients ?? false;
            SmtpScenario = opt.SmtpScenario ?? true;
            switch (ExportFormat) {
                case ExportFormatCode.EML:
                    FileExtension = ".zip";
                    break;
                case ExportFormatCode.PST:
                    FileExtension = ".pst";
                    break;
                case ExportFormatCode.ExchangeJournal:
                    FileExtension = ".zip"; //TODO: to be determined!
                    break;
                default:
                    break;
            }
        }

        public ExportFormatCode ExportFormat { get; private set; }
        public bool IncludeBccRecipients { get; }
        public bool IncludeLegalHoldMessages { get; private set; }
        protected RemoteWebDriver Driver { get; private set; }
        private DriverService DriverService { get; set; }
        protected string FileExtension { get; private set; }

        public string AuthenticationUrl { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string ScenarioFilePath { get; set; }
        public string DownloadFolderPath { get; set; }
        public string LogsFolder { get; set; }
        public string ActionType { get; set; }
        public bool ScenarioFileHasHeader { get; set; } = false;
        public string ReportsFolderPath { get; set; }
        public string ScreenshotsFolderPath { get; set; }
        public bool SmtpScenario { get; set; }

        private static StreamWriter _swLog;
        private static object _syncLog = new object();
        private static Point _defaultLocation = new Point(0, 0);
        private static Size _defaultSize = new Size(1300, 900); //{Width = 1293 Height = 953}

        protected virtual EntityBase ParseSearch(string line) {
            if (string.IsNullOrEmpty(line)) return null;
            var entity = CreateSearch();
            var fields = line.Split('\t');
            entity.ParseLine(fields);
            return entity;
        }

        public static double ComputeTransferRateMBps(DateTime from, DateTime to, long bytes) {
            var ts = to.Subtract(from);
            return ComputeTransferRateMBps(ts, bytes);
        }

        public static double ComputeTransferRateMBps(TimeSpan ts, long bytes) {
            double seconds = ts.TotalSeconds;
            if (seconds == 0)
                return 0;
            double mbps = bytes / seconds;
            mbps = TransformtoMB((long)mbps);
            return mbps;
        }

        public static double TransformtoMB(long bytes, int decimals = 2) {
            double mb = bytes / (1024 * 1024d);
            if (decimals < 0)
                return mb;
            mb = Math.Round(mb, decimals);
            return mb;
        }

        protected virtual EntityBase ParseExport(string line) {
            if (string.IsNullOrEmpty(line)) return null;
            var entity = CreateExport();
            var fields = line.Split('\t');
            entity.ParseLine(fields);
            return entity;
        }

        protected abstract EntityBase CreateSearch();
        protected abstract EntityBase CreateExport();

        protected virtual void OnScenarioLine(string line) { }

        protected static Mutex _mScenario = new Mutex(false, "A360_LoadScenarioFile");

        public void LoadScenarioFile() {
            if (_mScenario != null)
                throw new NotImplementedException();
            if (!_mScenario.WaitOne(60 * 1000)) {
                throw new ApplicationException("Could not read the scenario files in due time");
            }
            try {
                OnInitData();
                Info($"Loading scenario");
                bool hasHeader = ScenarioFileHasHeader;
                var fileName = GetLastReport(ReportsFolderPath);

                fileName = null; //force loading data from the scenario file instead of last report file
                if (string.IsNullOrEmpty(fileName))
                    fileName = ScenarioFilePath;
                Info($"From {fileName}, header {hasHeader}");
                var n = 0;
                using (StreamReader sr = File.OpenText(fileName)) {
                    string line;
                    bool header = hasHeader;
                    while ((line = sr.ReadLine()) != null) {
                        if (header) {
                            header = false;
                            continue;
                        }

                        if (string.IsNullOrEmpty(line)) continue;
                        OnScenarioLine(line.Trim());
                        n++;
                        if ((n % 100) == 0) {
                            Info($"Loaded {n} items");
                        }
                    }
                }
                Info($"Total loaded {n} items");
            }
            finally {
                _mScenario.ReleaseMutex();
            }
        }

        protected abstract void OnInitData();

        protected object SetValue(IWebElement elem, string value) {
            string id = elem.GetAttribute("id");
            var obj = SetValue(id, value, elem);
            return obj;
        }

        protected IWebElement WaitElementById(string id, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            By by = By.Id(id);
            return WaitElement(by, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElementByClassName(string className, ElementCoditionTypes conditionType, int timeoutSeconds = 60) {
            By by = By.ClassName(className);
            return WaitElement(by, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElementByValue(string value, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            var xpath = $"//*[contains(value(), '{value}')]";
            return WaitElementByXPath(xpath, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElementByName(string name, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            By by = By.Name(name);
            return WaitElement(by, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElementByText(string text, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            var xpath = $"//*[contains(text(), '{text}')]";
            return WaitElementByXPath(xpath, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElementByXPath(string xpath, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            By by = By.XPath(xpath);
            return WaitElement(by, conditionType, timeoutSeconds);
        }

        protected IWebElement WaitElement(By by, ElementCoditionTypes conditionType, int timeoutSeconds = 30) {
            EnsureWindowSize();
            var ts = new TimeSpan(0, 0, timeoutSeconds);
            WebDriverWait w = new WebDriverWait(Driver, ts);
            Func<IWebDriver, IWebElement> condition = null;
            switch (conditionType) {
                case ElementCoditionTypes.ElementExists:
                    condition = WaitHelpers.ExpectedConditions.ElementExists(by);
                    break;
                case ElementCoditionTypes.ElementIsVisible:
                    condition = WaitHelpers.ExpectedConditions.ElementIsVisible(by);
                    break;
                case ElementCoditionTypes.ElementToBeClickable:
                    condition = WaitHelpers.ExpectedConditions.ElementToBeClickable(by);
                    break;
            }
            if (condition == null) return null;
            IWebElement elem = null;
            elem = w.Until(condition);
            //try {
            //    elem = w.Until(condition);
            //}
            //catch {
            //    //an alert might be present
            //    //if (DismissAlert()) {
            //    //    Info("Alert dismissed, retrying");
            //    //    elem = w.Until(condition);
            //    //    return elem;
            //    //}
            //    throw;
            //}
            return elem;
        }

        protected void EnsureWindowSize() {
            Driver.Manage().Window.Position = _defaultLocation;
            //{Width = 1293 Height = 953}
            var size = Driver.Manage().Window.Size;
            if (size != _defaultSize) {
                Driver.SwitchTo().Window(Driver.CurrentWindowHandle);
                Driver.Manage().Window.Size = _defaultSize;
            }
        }

        protected IWebDriver SwitchToFrame(int index) {
            var wd = Driver.SwitchTo().Frame(index);
            return wd;
        }

        private string GetScreenshotFileName(string fileNameParticle = null) {
            string fileName = fileNameParticle;
            var screenshotsRootPath = ScreenshotsFolderPath;
            if (string.IsNullOrEmpty(screenshotsRootPath))
                screenshotsRootPath = GetAppPath();
            //var path = Path.Combine(screenshotsRootPath, "screenshots");
            var path = screenshotsRootPath;
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmsss");
            if (string.IsNullOrEmpty(fileName)) {
                fileName = timeStamp + ".jpg";
                fileName = $"{ActionType}_{fileName}";
            }
            else {
                fileName = $"{ActionType}_{fileNameParticle}_{timeStamp}.jpg";
            }
            fileName = Path.Combine(path, fileName);
            return fileName;
        }

        private Screenshot SaveScreenshot(string fileName, Screenshot ss) {
            try {
                if (ss == null) return null;
                fileName = GetScreenshotFileName(fileName);
                Info($"Saving screenshot {fileName}");

                ss.SaveAsFile(fileName, ScreenshotImageFormat.Jpeg);
                //Sleep();
            }
            catch (Exception ex) {
                Error(ex);
            }
            return ss;

        }

        protected void Sleep(int timeoutMs = 1000) {
            Thread.Sleep(timeoutMs);
        }

        /// <summary>
        /// Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        protected Screenshot SaveScreenshot(string fileName = null, bool dontSave = false) {
            Screenshot ss = null;
            try {
                ss = Driver.GetScreenshot();
                if (dontSave)
                    return ss;
                fileName = Utility.SanitizeFileName(fileName);
                SaveScreenshot(fileName, ss);
                Sleep();
            }
            catch { }
            return ss;
        }


        protected IWebDriver SwitchToParentFrame() {
            var wd = Driver.SwitchTo().ParentFrame();
            return wd;
        }

        protected object SetValue(string id, string value, IWebElement elem = null) {
            string script = $"{id}.value = '{value}'; return {id}.value;";
            var obj = Driver.ExecuteScript(script);
            //make sure the value was set
            if (elem != null) {
                //TODO: implement verification
            }
            return obj;
        }

        public static void DisposeLog() {
            lock (_syncLog) {
                if (_swLog != null) {
                    _swLog.Dispose();
                    _swLog = null;
                }
            }
        }

        public virtual void Dispose() {
            if (Driver != null) {
                try {
                    Driver.Quit();
                }
                catch (Exception ex) { Trace.WriteLine(ex); }
                try {
                    Driver.Dispose();
                }
                catch { }
                Driver = null;
            }
            if (DriverService != null) {
                DriverService.Dispose();
                DriverService = null;
            }
        }

        #region Events
        protected abstract void OnCredentialsNeeded();
        #endregion Events

        public virtual void Authenticate() {
            Info("Authenticating");
            Driver.Url = AuthenticationUrl;
            Driver.Navigate();
            OnCredentialsNeeded();
            Sleep(2 * 1000);
        }

        protected void WaitAlert(TimeSpan? timeout) {
            var dtStart = DateTime.Now;
            var alertPresent = false;
            IAlert alert;
            while ((alert = GetAlert()) != null) {
                if (!alertPresent) {
                    Info($"Waiting for alert to close:{Environment.NewLine}{alert.Text}");
                    alertPresent = true;
                }
                if (timeout.HasValue) {
                    var tsDuration = DateTime.Now.Subtract(dtStart);
                    if (tsDuration > timeout.Value) {
                        throw new ApplicationException($"Alert wait timed out after {tsDuration}");
                    }
                }
                SwitchToParentFrame();
            }
        }

        protected bool DismissAlert() {
            var alert = GetAlert();
            if (alert == null) return false;
            Info($"Accepting alert:{Environment.NewLine}{alert.Text}");
            try {
                alert.Accept();
                Info("Alert accepted");
            }
            catch { }

            //check if alert still persists
            alert = GetAlert();
            if (alert == null) {
                Info("Alert closed");
                return true;
            }
            Info($"Dismissing alert{Environment.NewLine}{alert.Text}");
            alert.Dismiss();

            return true;
        }

        protected IAlert GetAlert() {
            try {
                var alert = Driver.SwitchTo().Alert();
                return alert;
            } // try
            catch (Exception ex) {
                Info(ex.Message);
                return null;
            } // catch
        }

        public static void InstallAccessDatabaseEngine(AutomationDriverBase driver) {
            driver.Info("Quietly installing Excel support");
            var path = Path.Combine(AutomationDriverBase.GetAppPath(), "AccessDatabaseEngine_X64.exe");
            var p = Process.Start(path, "/quiet");
            if (!p.WaitForExit(2 * 60 * 1000)) {
                driver.Error("Excel support failed");
            }
        }

        public static string GetAppPath() {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return path;
        }

        protected virtual IWebDriver CreateWebDriver(WebDriverType webDriverType, DriverOptions options = null) {
            Info($"Launching connector driver {webDriverType}");
            var appPath = GetAppPath();
            switch (webDriverType) {
                case WebDriverType.Firefox: //minimal supported version: 47.0.2
                    //DriverService = FirefoxDriverService.CreateDefaultService(appPath);

                    FirefoxOptions fop = (FirefoxOptions)options;
                    if (fop == null) {
                        fop = new FirefoxOptions();
                    }
                    FirefoxProfile profile = new FirefoxProfile();
                    if (!string.IsNullOrEmpty(DownloadFolderPath)) {
                        String path = DownloadFolderPath;
                        //profile.SetPreference("browser.download.manager.showWhenStarting", false);
                        profile.SetPreference("browser.download.folderList", 2);
                        profile.SetPreference("browser.download.dir", path);
                        profile.SetPreference("browser.download.manager.alertOnEXEOpen", false);

                        //to set up automatic download, see https://stackoverflow.com/a/36356422/274589
                        profile.SetPreference("browser.helperApps.neverAsk.saveToDisk", "application/msword, application/csv, application/ris, text/csv, image/png, application/pdf, text/html, text/plain, application/zip, application/x-zip, application/x-zip-compressed, application/download, application/octet-stream, application/x-zip-compressed;charset=utf-8");
                        profile.SetPreference("browser.download.panel.shown", false);

                        profile.SetPreference("browser.download.manager.focusWhenStarting", false);
                        profile.SetPreference("browser.download.useDownloadDir", true);
                        profile.SetPreference("browser.helperApps.alwaysAsk.force", false);
                        profile.SetPreference("browser.download.manager.alertOnEXEOpen", false);
                        profile.SetPreference("browser.download.manager.closeWhenDone", true);
                        profile.SetPreference("browser.download.manager.showAlertOnComplete", false);
                        profile.SetPreference("browser.download.manager.useWindow", false);
                        profile.SetPreference("services.sync.prefs.sync.browser.download.manager.showWhenStarting", false);
                        profile.SetPreference("pdfjs.disabled", true);
                        profile.SetPreference("browser.download.animateNotifications", false);
                        profile.SetPreference("browser.download.manager.showAlertOnComplete", false);
                    }

                    fop.Profile = profile;
                    //fop.AddArgument("--width=2560");
                    //fop.AddArgument("--height=1440");

                    FirefoxDriver fd = new FirefoxDriver(fop);
                    Driver = fd;
                    EnsureWindowSize();
                    break;
            }
            return Driver;
        }

        public void Info(string text) {
            lock (_syncLog) {
                //if (Debug) SaveScreenshot();
                _swLog = CreateLog();
                if (!string.IsNullOrEmpty(text)) {
                    text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {text}";
                }
                Console.WriteLine(text);
                _swLog.WriteLine(text);
            }
        }

        private void WriteArgs() {
            List<string> arguments = new List<string>();
            AddArguments(arguments);
            var msg = $"Start arguments:{Environment.NewLine}{string.Join(Environment.NewLine, arguments)}";
            Info(msg);
        }

        protected virtual void AddArguments(List<string> arguments) {
            arguments.Add($"ActionType         = {ActionType}");
            arguments.Add($"AuthenticationUrl  = {AuthenticationUrl}");
            arguments.Add($"UserName           = {UserName}");
            arguments.Add($"Password           = {(string.IsNullOrEmpty(Password) ? "Not specified!" : "********")}");
            arguments.Add($"ScenarioFilePath   = {ScenarioFilePath}");
            arguments.Add($"DownloadFolderPath = {DownloadFolderPath}");
            arguments.Add($"LogsFolder         = {LogsFolder}");
            arguments.Add($"ScreenshotsFolder  = {ScreenshotsFolderPath}");
            arguments.Add($"IncludeLitigationHold = {IncludeLegalHoldMessages}");
        }

        protected IWebElement ScrollToElement(string id, TimeSpan? tsWait = null) {
            WebDriverWait w = new WebDriverWait(Driver, new TimeSpan(0, 1, 0));
            IWebElement element = w.Until(WaitHelpers.ExpectedConditions.ElementExists(By.Id(id)));

            if (!tsWait.HasValue)
                tsWait = new TimeSpan(0, 0, 10);
            w = new WebDriverWait(Driver, tsWait.Value);

            Driver.ExecuteScript("arguments[0].scrollIntoView(true);", element);
            element = w.Until(WaitHelpers.ExpectedConditions.ElementIsVisible(By.Id(id)));
            return element;
        }

        public void Error(Exception ex, bool addStackTrace = false) {
            string text = ex.Message;
            if (ex.InnerException != null) {
                text = $"{text}{Environment.NewLine}{ex.InnerException.Message}";
            }
            if (addStackTrace) {
                text = $"{text}{Environment.NewLine}{ex.StackTrace}";
            }
            Error(text);
        }

        public void Error(Exception ex) {
            Error(ex, true);
        }

        public void Error(string text) {
            text = $"ERROR: {text}";
            Info(text);
        }

        private const long MaxLogSize = Int32.MaxValue - 1024 * 10;

        private StreamWriter CreateLog() {
            if (_swLog != null) {
                if (_swLog.BaseStream != null && _swLog.BaseStream.Length < MaxLogSize) {
                    return _swLog;
                }
                else {
                    _swLog.Dispose();
                    _swLog = null;
                }
            }
            string folder;
            if (!string.IsNullOrEmpty(LogsFolder)) {
                if (!Path.IsPathRooted(LogsFolder)) {
                    folder = GetAppPath();
                    folder = Path.Combine(folder, LogsFolder);
                }
                else
                    folder = LogsFolder;
            }
            else {
                folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                folder = Path.Combine(folder, "logs");
            }

            Directory.CreateDirectory(folder);

            string prefix = string.IsNullOrEmpty(ActionType) ? "" : $"{ActionType}_";

            string fileName = $"{prefix}{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.log";
            fileName = Path.Combine(folder, fileName);
            Console.WriteLine($"Creating log file {fileName}");
            _swLog = File.CreateText(fileName);
            _swLog.AutoFlush = true;

            WriteArgs();

            return _swLog;
        }

        private string GetLastReport(string reportsFolderPath) {
            if (!Directory.Exists(reportsFolderPath))
                return null;
            var di = new DirectoryInfo(reportsFolderPath);
            //var files = Directory.GetFiles(reportsFolderPath, "Report_????????_??????.txt")
            //    .OrderByDescending(x => x)
            //    .Take(1)
            //    .ToList();
            //if (files.Count < 1)
            //    return null;
            //return files[0];
            //var lastFile = di.GetFiles("Report_????????_??????.txt")
            var lastFile = di.GetFiles("Report_????????.txt")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            if (lastFile == null) return null;
            return lastFile.FullName;
        }

        protected void PerformActivity() {
            //scenario:
            //move mouse, press ALT key
            Info("Performing activity scenario");
            try {
                Actions action = new Actions(Driver);
                action.MoveToElement(Driver.FindElementByTagName("body")).Perform();
                Thread.Sleep(1000);
                //action.MoveByOffset(-50, -50).Perform();
                //Thread.Sleep(1000);
                action.ContextClick();
            }
            catch (Exception ex) {
                Error($"Error performing activity: {ex.Message}");
            }
        }

    }
}
