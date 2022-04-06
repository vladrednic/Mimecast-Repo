using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace A2A.ExcelReporter {
    public class ExcelReporterHelper {
        private const string MainTableName = "[Report$]";
        private const string CustodiansTableName = "[Custodians$]";
        private const string DailySummaryTableName = "[DailySummary$]";
        private OdbcConnection GetExcelDataConnection() {
            //var connStrBuilder = new OdbcConnectionStringBuilder();
            //connStrBuilder["Provider"] = "Microsoft.ACE.OLEDB.12.0";
            //connStrBuilder["Data Source"] = $"{ExcelReportPath}";
            //connStrBuilder["Extended Properties"] = "\"Excel 12.0;HDR=YES;IMEX=1;\"";
            //connStrBuilder.Dsn = "MessageOneReport";
            //var connStr = connStrBuilder.ToString();
            //connStr = $@"Driver={{Microsoft Excel Driver (*.xls)}};DriverId=790;Dbq={ExcelReportPath};";
            //connStr = $"Excel File={ExcelReportPath};";
            var connStr = "Dsn=Extraction_worksheet";
            var conn = new OdbcConnection(connStr);
            return conn;
        }

        private OdbcConnection _connection = null;

        public OdbcConnection Connection {
            get {
                if (_connection == null)
                    _connection = GetExcelDataConnection();
                return _connection;
            }
        }

        private string[] GetSheets() {
            //dt = objConn.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
            using (var conn = GetExcelDataConnection()) {
                conn.Open();
                var dt = conn.GetSchema("Tables");
                if (dt == null) {
                    return null;
                }

                String[] excelSheets = new String[dt.Rows.Count];
                int i = 0;

                // Add the sheet name to the string array.
                foreach (DataRow row in dt.Rows) {
                    excelSheets[i] = row["TABLE_NAME"].ToString();
                    i++;
                }
                return excelSheets;
            }
        }

        private OdbcCommand GetCommand(string sql, bool openConnection = false) {
            var cmd = Connection.CreateCommand();
            cmd.CommandText = sql;
            if (openConnection)
                Connection.Open();
            return cmd;
        }

        void CreateTable(DateTime date) {
            var sheets = GetSheets();
            var tableName = $"{date.ToString("yyyyMMdd")}";
            if (sheets.Contains(tableName)) {
                return;
            }
            var sql = $@"CREATE TABLE {tableName} (
[Date] DATETIME,
[Custodian] TEXT,
[Mimecast search emails count] NUMBER,
[Extracted emails] NUMBER,
[Data amount] NUMBER)";
            using (var cmd = GetCommand(sql, true)) {
                try {
                    cmd.ExecuteNonQuery();
                }
                finally {
                    Connection.Close();
                }
            }
        }

        private int? GetCustodianSearchItems(string custodianName, bool openConnection = false) {
            var sql = $@"SELECT [SearchItems] FROM {CustodiansTableName} WHERE [Name]={ToSql(custodianName)}";
            using (var cmd = GetCommand(sql, openConnection)) {
                var result = cmd.ExecuteScalar();
                if (result != null && result != DBNull.Value) {
                    return int.Parse(result.ToString());
                }
                return null;
            }
        }

        public int AddRecord(RecordData data) {
            //var sheets = GetSheets();
            var n = 0;
            var date = data.Date.Date;

            var sql = $@"SELECT TOP 1 COUNT(*) as cnt FROM {MainTableName} WHERE [Date]={ToSql(date)} AND [Custodian]={ToSql(data.Custodian)}";
            try {
                Connection.Open();
                using (var cmd = GetCommand(sql)) {
                    //cmd.Parameters.AddWithValue("Date", date);
                    //cmd.Parameters.AddWithValue("Custodian", data.Custodian);
                    n = (int)cmd.ExecuteScalar();
                }

                data.SearchItems = GetCustodianSearchItems(data.Custodian);

                if (n != 1) {
                    //insert
                    sql = $@"
INSERT INTO {MainTableName}
    ([Date], [Custodian], [Mimecast search emails count], [Extracted emails], [Data amount]) VALUES
    ({ToSql(date)}, {ToSql(data.Custodian)},{ToSql(data.SearchItems)},{ToSql(data.ExtractedItems)},{ToSql(data.DataSize)})";
                }
                else {
                    //update
                    sql = $@"
UPDATE {MainTableName} SET
    [Extracted emails] = [Extracted emails] + {ToSql(data.ExtractedItems ?? 0)},
    [Data amount] = [Data amount] + {ToSql(data.DataSize ?? 0)},
    [Mimecast search emails count] = {ToSql(data.SearchItems)}
WHERE
    [Date]={ToSql(date)} AND [Custodian]={ToSql(data.Custodian)}";
                }
                using (var cmd = GetCommand(sql)) {
                    n = cmd.ExecuteNonQuery();
                }
            }
            finally { Connection.Close(); }

            return n;
        }

        #region Converters
        public static string ToSql(DateTime? dt) {
            if (!dt.HasValue)
                return "NULL";
            var s = $"'{dt.Value.ToString("yyyy-MM-dd")}'";
            return s;
        }

        public static string ToSql(string value) {
            if (string.IsNullOrEmpty(value))
                return "NULL";
            value = value.Replace("'", "''");
            return $"'{value}'";
        }

        public static string ToSql(long? value) {
            if (!value.HasValue)
                return "NULL";
            var s = value.ToString();
            return s;
        }

        public static string ToSql(double? value) {
            if (!value.HasValue)
                return "NULL";
            var s = value.ToString();
            return s;
        }
        #endregion Converters

        #region Tests
        public void TestCreateTable() {
            CreateTable(DateTime.Now);
        }
        public void TestNonQuery() {
            var sheets = GetSheets();
            using (var conn = GetExcelDataConnection()) {
                conn.Open();
                var sql = "update [Sheet1$] set [Date]=1";
                using (var cmd = conn.CreateCommand()) {
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void TestQuery() {
            var sheets = GetSheets();
            using (var conn = GetExcelDataConnection()) {
                conn.Open();
                var sql = "select [Mimecast search emails count] from [Sheet1$]";
                using (var cmd = conn.CreateCommand()) {
                    cmd.CommandText = sql;
                    cmd.Connection = conn;
                    using (var dr = cmd.ExecuteReader()) {
                        while (dr.Read()) {

                        }
                    }
                }
            }
        }
        public void TestAddRecord() {
            var data = new RecordData() {
                Custodian = "John.Brauchle@kiewit.com",
                DataSize = 101,
                Date = DateTime.Now,
                ExtractedItems = 66,
                FileSize = 10,
                SearchItems = 999
            };
            var n = AddRecord(data);
            //UpdateDailySummary(data.Date);
        }

        public void UpdateDailySummary(DateTime date) {
            try {
                var sql = $@"SELECT TOP 1 COUNT(*) as cnt FROM {DailySummaryTableName} WHERE [Date] = {ToSql(date)}";
                var n = 0;
                Connection.Open();

                using (var cmd = GetCommand(sql)) {
                    n = (int)cmd.ExecuteScalar();
                }

                sql = $@"SELECT SUM([Extracted emails]) as [Items], SUM([Data amount]) as [Size] FROM {MainTableName} WHERE [Date] = {ToSql(date)}";

                var items = 0L;
                var size = 0d;

                using (var cmd = GetCommand(sql)) {
                    using (var dr = cmd.ExecuteReader()) {
                        if (dr.Read()) {
                            items = (long)((double)dr["Items"]);
                            size = (long)((double)dr["Items"]);
                            size /= (1024 * 1024 * 1024);
                            size = Math.Round(size, 3);
                        }
                    }
                }

                if (n != 0) {
                    //update
                    sql = $@"UPDATE {DailySummaryTableName} SET
[Extracted emails] = [Extracted emails] + {items},
[Data amount] = [Data amount] + {size}
WHERE [Date]={ToSql(date)}";
                }
                else {
                    //insert
                    sql = $@"INSERT INTO {DailySummaryTableName}
([Date], [Extracted emails], [Data amount (GB)])
VALUES
({ToSql(date)}, {ToSql(items)}, {ToSql(size)})";
                }

                using (var cmd = GetCommand(sql)) {
                    cmd.ExecuteNonQuery();
                }
            }
            finally {
                Connection.Close();
            }
        }
        #endregion Tests
    }
}
