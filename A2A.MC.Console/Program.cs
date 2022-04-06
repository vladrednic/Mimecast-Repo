using A2A.Automation.Base;
using A2A.ExcelReporter;
using A2A.MC.Automation;
using A2A.MC.Common;
using A2A.MC.Data;
using A2A.MC.Kernel.Exceptions;
using A2A.Option;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Console {
    class Program {
        private static MCAutomationDriver _driver;

        static void Main(string[] args) {
            _args = args;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            try {
                var parser = Parser.Default;
                parser = new Parser(
                    s => {
                        s.CaseSensitive = false;
                        s.AutoHelp = true;
                        s.AutoVersion = true;
                        s.CaseInsensitiveEnumValues = true;
                        s.IgnoreUnknownArguments = false;
                    });
                var result = parser.ParseArguments<Options>(args)
                    .WithParsed<Options>(RunOptions)
                    .WithNotParsed(HandleParseError);
                switch (result.Tag) {
                    case ParserResultType.NotParsed:
                        var usage = new UsageMessage();
                        usage.Execute();
                        break;
                }
            }
            catch (Exception) {
                //Logger.Error(ex.Message, ex);
            }
            //Console.ReadLine();
        }

        private static void HandleParseError(IEnumerable<Error> obj) {
            throw new NotImplementedException();
        }

        private static string[] _args = null;
        private static void ParseStandardArgs(string[] args, Options opt) {
            if (args == null || args.Length < 1)
                return;

            foreach (var arg in args) {
                string name = null;
                string value = null;
                var parts = arg.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1)
                    continue;
                name = parts[0];
                if (!string.IsNullOrEmpty(name))
                    name = name.Trim().ToLower();
                if (parts.Length > 1) {
                    value = parts[1];
                    if (!string.IsNullOrEmpty(value))
                        value = value.Trim().ToLower();
                }

                switch (name) {
                    case "a":
                    case "action":
                        opt.Action = ParseCmdLineAction(name, value);
                        break;
                }
            }
        }

        private static MCActions ParseCmdLineAction(string name, string value) {
            switch (value) {
                case "id":
                    value = nameof(MCActions.ImportData);
                    break;
                case "cs":
                    value = nameof(MCActions.CreateSearch);
                    break;
                case "ce":
                    value = nameof(MCActions.CheckExport);
                    break;
                case "r":
                    value = nameof(MCActions.Reconcile);
                    break;
                case "ds":
                    value = nameof(MCActions.DeleteSearch);
                    break;
            }
            if (!Enum.TryParse<MCActions>(value, true, out MCActions action))
                throw new ApplicationException($"Invalid parameters: {name}={value}");
            return action;
        }

        private static void RunOptions(Options opt) {
            ParseStandardArgs(_args, opt);
            opt = opt.Combine(Properties.Settings.Default);
            //Context.CustomerName = opt.CustomerName;

            var mgr = new PasswordManager();
            opt.Password = mgr.GetPasswordPlainText();
            var sqlInstance = Properties.Settings.Default.SqlServerInstance;
            var sqlDatabaseName = Properties.Settings.Default.SqlDatabaseName;
            var sqlUserPassword = Properties.Settings.Default.SqlDatabasePassword;
            var sqlIntegratedSecurity = Properties.Settings.Default.SqlIntegratedSecurity;
            var sqlUserName = Properties.Settings.Default.SqlDatabaseUserName;

            var action = opt.Action;
            using (_driver = new MCAutomationDriver(opt)) {
                _driver.Info($"Intializing database in sql instance: {sqlInstance}, database name: {sqlDatabaseName}, integrated security: {sqlIntegratedSecurity}");
                DbFactory.LogNeeded += DbFactory_LogNeeded;
                try {
                    _driver.Info($"Initializing database...");
                    DbFactory.InitDatabase(sqlInstance, sqlDatabaseName, sqlIntegratedSecurity, sqlUserName, sqlUserPassword);
                    _driver.Info($"Database initialized");
                }
                catch (Exception ex) {
                    _driver.Error(ex);
                    return;
                }

                //AutomationDriverBase.InstallAccessDatabaseEngine(driver);
                //Context.CustomerName = opt.CustomerName;
                try {
                    switch (action) {
                        case MCActions.ImportData:
                            //Context.ProjectName = "Mimecast Import";
                            //Context.ProjectTypeName = "MimecastImport";
                            DoImportData(_driver, opt);
                            break;
                        case MCActions.CheckExport:
                            //driver.DoGetSearchTotalMessages();
                            //Context.ProjectName = "Mimecast Extraction";
                            //Context.ProjectTypeName = "MimecastExtraction";
                            DoCheckExports(_driver);
                            break;
                        case MCActions.CreateSearch:
                            DoCreateSearches(_driver);
                            break;
                        case MCActions.GetCount:
                            _driver.DoGetSearchTotalMessages();
                            break;
                        case MCActions.DeleteSearch:
                            DoDeleteSearches(_driver);
                            break;
                        case MCActions.Reconcile:
                            DoReconcile(_driver);
                            break;
                        default:
                            PrintUsage();
                            return;
                    }
                    //driver.CheckExports();
                    //driver.CreateSearches();
                }
                catch (Exception ex) {
                    _driver.Error(ex);
                }
                _driver.Info("Exit");
            }
        }

        private static void DoReconcile(MCAutomationDriver driver) {
            driver.ConfirmSearches();
        }

        private static void DbFactory_LogNeeded(object sender, EventArgs e) {
            if (_driver != null)
                _driver.Info(DbFactory.LogText);
        }

        private static void DoCreateSearches(MCAutomationDriver driver) {
            //Context.ProjectName = "Mimecast Search Create";
            //Context.ProjectTypeName = "MimecastSearchCreate";
            driver.DoCreateSearches();
        }

        /// <summary>
        /// Deletes all searches from the folder
        /// </summary>
        private static void DoDeleteSearches(MCAutomationDriver driver) {
            while (true) {
                try {
                    driver.DeleteSearches();
                    break;
                }
                catch { }
            }
        }

        private static void DoImportData(MCAutomationDriver driver, Options options) {
            var helper = new ScenarioHelper() {
                ScenarioFilePath = options.ScenarioFilePath
            };
            var searches = helper.GetSearches();
            var db = new DbManager {
                LogProvider = driver
            };
            db.SearchAddProgress += Db_SearchAddProgress;
            driver.Info($"Adding {searches.Count} searches");
            var n = db.AddSearches(searches);
            driver.Info($"Total processed: {db.TotalProcessed}. Inserted: {db.BatchInserted}. Duplicates: {db.BatchDuplicates}");
        }

        private static void Db_SearchAddProgress(object sender, EventArgs e) {
            var db = sender as DbManager;
            if (db == null || db.LogProvider == null)
                return;
            db.LogProvider.Info($"Processed: {db.TotalProcessed} / {db.TotalSearches}. Inserted: {db.BatchInserted}. Duplicates: {db.BatchDuplicates}");
        }

        private static void DoCheckExports(MCAutomationDriver driver) {
            while (true) {
                try {
                    driver.CheckExportsWithRetry();
                }
                catch (Exception ex) {
                    driver.Error(ex);
                }
                finally {
                    driver.Info("Restarting");
                }
            }
        }

        private static void PrintUsage() {
            var values = Enum.GetValues(typeof(MCActions)).OfType<object>().Select(o => o.ToString()).ToArray();

            var msg = $@"Usage:
Accepted parameter values: {string.Join(", ", values)}";
            System.Console.WriteLine(msg);
        }
    }
}
