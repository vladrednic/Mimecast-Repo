using A2A.MC.Kernel.Entities;
using A2A.MC.Kernel.Enums;
using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Option {
    public class Options {
        [JsonConverter(typeof(StringEnumConverter))]
        [Option('a', "action", Required = false, HelpText = "Action to perform: ImportData, CheckExport, CreateSearch, GetCount, DeleteSearch, Reconcile")]
        public MCActions? Action { get; set; }

        [Option('r', "url", Required = false, HelpText = "Authentication URL")]
        public string AuthenticationUrl { get; set; }

        [Option('u', "user", Required = false, HelpText = "User name")]
        public string UserName { get; set; }

        [Option('f', "report", Required = false, HelpText = "Report folder path")]
        public string ReportsFolderPath { get; set; }

        [Option('s', "scenario", Required = false, HelpText = "Scenario folder path")]
        public string ScenarioFilePath { get; set; }

        [Option('d', "download", Required = false, HelpText = "Download folder path")]
        public string DownloadFolderPath { get; set; }

        [Option('l', "logs", Required = false, HelpText = "Logs folder path")]
        public string LogsFolder { get; set; }

        [Option('i', "header", Required = false, HelpText = "Scenario file has header")]
        public bool? ScenarioFileHasHeader { get; set; }

        [Option('n', "launchnew", Required = false, HelpText = "Allow launch new export")]
        public bool? AllowLaunchNewExports { get; set; }

        [Option('t', "screenshot", Required = false, HelpText = "Screenshot folder path")]
        public string ScreenshotsFolderPath { get; set; }

        [Option('m', "minslots", Required = false, HelpText = "Minimum free slots to launch new exports")]
        public int? MinSlotsForNewExports { get; set; }

        [Option('o', "offhourslots", Required = false, HelpText = "Maximum number of slots to use during off hours")]
        public int? OffHoursTimeSlots { get; set; }

        [Option('w', "workhourslots", Required = false, HelpText = "Maximum number of slots to use during work hours")]
        public int? WorkHoursTimeSlots { get; set; }

        [Option('e', "exporturl", Required = false, HelpText = "Exports page URL")]
        public string ExportsPageUrl { get; set; }

        [Option('h', "home", Required = false, HelpText = "Home search folder name")]
        public string HomeSearchFolder { get; set; }

        [Option('c', "searchurl", Required = false, HelpText = "Search page URL")]
        public string SavedSearchesUrl { get; set; }

        [Option('p', "password", Required = false, Hidden = true, HelpText = "Password")]
        public string Password { get; set; }

        [Option('x', "maxresults", Required = false, HelpText = "Max results per export")]
        public int? MaxResultsPerExport { get; set; }

        [Option('y', "customername", Required = false, HelpText = "Customer name")]
        public string CustomerName { get; set; }
        [Option('g', "includelh", Required = false, HelpText = "Include Legal Hold Messages")]
        public bool? IncludeLegalHoldMessages { get; set; }
        [Option('b', "exportformat", Required = false, HelpText = "Export Format: EML / PST / ExchangeJournal")]
        public ExportFormatCode? ExportFormat { get; set; }

        [Option('z', "includebcc", Required = false, HelpText = "Include BCC Recipients")]
        public bool? IncludeBccRecipients { get; set; }

        [Option('j', "smtpscenario", Required = false, HelpText = "Is SMTP Scenario")]
        public bool? SmtpScenario { get; set; }

        [Option("datestrategy", Required = false, HelpText = "Date range scenario strategy")]
        public DateRangeStrategyEnum? DateRangeStrategy { get; set; }

        [Option("datestrategyval", Required = false, HelpText = "Date range scenario strategy value")]
        public int? DateRangeStrategyValue { get; set; }

        [Option("sqlserver", Required = false, HelpText = "Sql server instance")]
        public string SqlServerInstance { get; set; }

        [Option("sqldatabase", Required = false, HelpText = "Sql server database")]
        public string SqlDatabaseName { get; set; }

        [Option("sqlpassword", Required = false, HelpText = "Sql server password")]
        public string SqlDatabasePassword { get; set; }

        [Option("sqlintegratedsecurity", Required = false, HelpText = "Logon with integrated security")]
        public bool? SqlIntegratedSecurity { get; set; }

        [Option("sqluser", Required = false, HelpText = "Sql server user name")]
        public string SqlDatabaseUserName {get;set;}
    }
}
