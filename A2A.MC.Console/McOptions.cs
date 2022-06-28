using A2A.MC.Console.Properties;
using A2A.Option;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Option {
    public static class McOptions {
        internal static Options Combine(this Options opt, Settings settings) {
            if (opt is null) {
                throw new ArgumentNullException(nameof(opt));
            }
            if (settings is null) {
                throw new ArgumentNullException(nameof(settings));
            }

            var newOpt = new Options {
                Action = opt.Action ?? settings.Action,
                AllowLaunchNewExports = opt.AllowLaunchNewExports ?? settings.AllowLaunchNewExports,
                AuthenticationUrl = opt.AuthenticationUrl ?? settings.AuthenticationUrl,
                DownloadFolderPath = opt.DownloadFolderPath ?? settings.DownloadFolderPath,
                ExportsPageUrl = opt.ExportsPageUrl ?? settings.ExportsPageUrl,
                HomeSearchFolder = opt.HomeSearchFolder ?? settings.HomeSearchFolder,
                LogsFolder = opt.LogsFolder ?? settings.LogsFolder,
                MinSlotsForNewExports = opt.MinSlotsForNewExports ?? settings.MinSlotsForNewExports,
                OffHoursTimeSlots = opt.OffHoursTimeSlots ?? settings.OffHoursTimeSlots,
                ReportsFolderPath = opt.ReportsFolderPath ?? settings.ReportsFolderPath,
                SavedSearchesUrl = opt.SavedSearchesUrl ?? settings.SavedSearchesUrl,
                ScenarioFileHasHeader = opt.ScenarioFileHasHeader ?? settings.ScenarioFileHasHeader,
                ScenarioFilePath = opt.ScenarioFilePath ?? settings.ScenarioFilePath,
                ScreenshotsFolderPath = opt.ScreenshotsFolderPath ?? settings.ScreenshotsFolderPath,
                UserName = opt.UserName ?? settings.UserName,
                Password = opt.Password ?? settings.Password,
                WorkHoursTimeSlots = opt.WorkHoursTimeSlots ?? settings.WorkHoursTimeSlots,
                MaxResultsPerExport = opt.MaxResultsPerExport ?? settings.MaxResultsPerExport,
                CustomerName = opt.CustomerName ?? settings.CustomerName,
                IncludeLegalHoldMessages = opt.IncludeLegalHoldMessages ?? settings.IncludeLitigationHoldMessages,
                ExportFormat = opt.ExportFormat ?? settings.ExportFormat,
                IncludeBccRecipients = opt.IncludeBccRecipients ?? settings.IncludeBccRecipients,
                SmtpScenario = opt.SmtpScenario ?? settings.SmtpScenario,
                DateRangeStrategy = opt.DateRangeStrategy ?? settings.DateRangeStrategy,
                DateRangeStrategyValue = opt.DateRangeStrategyValue ?? settings.DateRangeStrategyValue,

                SqlServerInstance = opt.SqlServerInstance ?? settings.SqlServerInstance,
                SqlDatabaseName =  opt.SqlDatabaseName ?? settings.SqlDatabaseName,
                SqlDatabasePassword = opt.SqlDatabasePassword ?? settings.SqlDatabasePassword,
                SqlIntegratedSecurity = opt.SqlIntegratedSecurity ?? settings.SqlIntegratedSecurity,
                SqlDatabaseUserName = opt.SqlDatabaseUserName ?? settings.SqlDatabaseUserName,
            };

            if (!newOpt.MaxResultsPerExport.HasValue)
                newOpt.MaxResultsPerExport = 50000;
            if (string.IsNullOrEmpty(newOpt.CustomerName)) {
                newOpt.CustomerName = $"Customer_Default";
            }

            return newOpt;
        }
    }
}
