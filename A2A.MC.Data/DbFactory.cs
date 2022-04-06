using System;
using System.Collections.Generic;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.MC.Data {
    public static class DbFactory {
        static DbFactory() {
            LogNeeded += DbFactory_LogNeeded;
        }

        public static string DatabaseFolderPath { get; set; }
        public static string SqlInstance { get; set; }
        public static string LogText { get; private set; }
        public static string SqlUserPassword { get; set; }
        public static bool SqlIntegratedSecurity { get; set; }
        public static string SqlUserName { get; set; }
        private static void DbFactory_LogNeeded(object sender, EventArgs e) { }

        public static event EventHandler LogNeeded;
        public static McDbContext GetDbContext() {
            return GetDbContext(SqlInstance, SqlDatabaseName, SqlIntegratedSecurity, SqlUserName, SqlUserPassword);
        }
        public static McDbContext GetDbContext(string sqlInstance, string sqlDatabaseName, bool sqlIntegratedSecurity, string sqlUserName, string sqlUserPassword) {
            SqlInstance = sqlInstance;
            SqlDatabaseName = sqlDatabaseName;
            SqlIntegratedSecurity = sqlIntegratedSecurity;
            SqlUserName = sqlUserName;
            SqlUserPassword = sqlUserPassword;

            if (!string.IsNullOrEmpty(sqlInstance))
                SqlInstance = sqlInstance;
            if (!string.IsNullOrEmpty(sqlDatabaseName))
                SqlDatabaseName = sqlDatabaseName;

            SqlConnectionStringBuilder connBuilder;
            if (sqlIntegratedSecurity) {
                connBuilder = new SqlConnectionStringBuilder() {
                    DataSource = SqlInstance,
                    InitialCatalog = SqlDatabaseName,
                    ApplicationName = "a360mimecast",
                    IntegratedSecurity = sqlIntegratedSecurity,
                    PersistSecurityInfo = true
                };
            }
            else {
                connBuilder = new SqlConnectionStringBuilder() {
                    DataSource = SqlInstance,
                    InitialCatalog = SqlDatabaseName,
                    //AttachDBFilename = $@"{DatabaseFolderPath}\{DatabaseName}.mdf",
                    ApplicationName = "a360mimecast",
                    IntegratedSecurity = sqlIntegratedSecurity,
                    PersistSecurityInfo = true,
                    UserID = sqlUserName,
                    Password = sqlUserPassword
                };
            }

            //var connString = $@"Data Source=(LocalDb)\MSSQLLocalDB;Initial Catalog=mimecast;Integrated Security=SSPI;AttachDBFilename={DatabaseFolderPath}\mimecast.mdf;User Instance=true";
            var connString = $"{connBuilder.ConnectionString}";
            //Info($"ConnectionString: {connBuilder.ConnectionString}");
            var db = new McDbContext(connString);
            return db;
        }

        public static void Info(string text) {
            LogText = text;
            LogNeeded(null, EventArgs.Empty);
        }

        public static void InitDatabase(string sqlInstance, string databaseName, bool integratedSecurity, string userName, string dbPassword) {
            using (var db = GetDbContext(sqlInstance, databaseName, integratedSecurity, userName, dbPassword)) {
                db.Database.Initialize(false);
            }
        }

        public static string SqlDatabaseName = "mimecast";
    }
}
