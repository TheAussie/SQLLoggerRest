using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TShockAPI.ServerSideCharacters;
using TShockAPI;
using TShockAPI.DB;
using Rests;
using TerrariaApi.Server;
using Terraria;
namespace SQLLoggerRest
{
    [ApiVersion(1, 17)]
    public class SQLLogger : TerrariaPlugin
    {

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        public override string Author
        {
            get { return "Grandpa-G"; }
        }
        public override string Name
        {
            get { return "SQLLoggerRest"; }
        }

        public override string Description
        {
            get { return "Interface for MySQL Log"; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
            base.Dispose(disposing);
        }

        public SQLLogger(Main game)
            : base(game)
        {
            Order = 1;
        }

        public override void Initialize()
        {
            TShock.RestApi.Register(new SecureRestCommand("/SQLLogger/getRows", getRows, "SQLLogger.allow"));
            TShock.RestApi.Register(new SecureRestCommand("/SQLLogger/getStats", getStats, "SQLLogger.allow"));
            TShock.RestApi.Register(new SecureRestCommand("/SQLLogger/deleteRows", deleteRows, "SQLLogger.allow"));

        }

        public static RestObject getStats(RestRequestArgs args)
        {
            LogStat rec;
            string sql = "";
            if (!TShock.Config.UseSqlLogs)
                return new RestObject("400") { Response = "UseSqlLogs not set to true.", };

            DatabaseStat dbStat = new DatabaseStat();

            if (TShock.DB.GetSqlType() == SqlType.Mysql)
            {
                dbStat.DBType = "MySQL";
                sql = "SELECT SUM( data_length + index_length) / 1024 / 1024 as Size FROM information_schema.TABLES where table_schema = \"tshock\"";
                try
                {
                    using (var reader = TShock.DB.QueryReader(sql))
                    {
                        if (reader.Read())
                            dbStat.DBSize = reader.Get<double>("Size");
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    Console.WriteLine(ex.StackTrace);
                }
                sql = "SELECT table_rows as Rows, data_length, index_length, round(((data_length + index_length) / 1024 / 1024),2) as Size FROM information_schema.TABLES WHERE table_schema = \"tshock\" and table_name = \"logs\"";
                try
                {
                    using (var reader = TShock.DB.QueryReader(sql))
                    {
                        if (reader.Read())
                        {
                            dbStat.TableRows = reader.Get<int>("Rows");
                            dbStat.TableSize = reader.Get<double>("Size");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    Console.WriteLine(ex.StackTrace);
                }
                sql = "select count(id) as Count, year(timestamp) as Year, month(TimeStamp) as Month from logs group by year(timestamp), month(TimeStamp)";

                List<LogStat> Loglist = new List<LogStat>();
                try
                {
                    using (var reader = TShock.DB.QueryReader(sql))
                    {
                        if (reader.Read())
                        {
                            rec = new LogStat(reader.Get<Int32>("Count"), reader.Get<Int32>("Year"), reader.Get<Int32>("Month"));
                            Loglist.Add(rec);
                        }
                    }
                    return new RestObject() { { "Rows", Loglist }, { "Stats", dbStat }, { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString() } };
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    Console.WriteLine(ex.StackTrace);
                }
            }
            else
            {
                dbStat.DBType = "SQLite";
                dbStat.DBSize = 0;
                dbStat.TableRows = 0;
                dbStat.TableSize = 0;
                sql = "SELECT count(*) as Rows FROM Logs";
                try
                {
                    using (var reader = TShock.DB.QueryReader(sql))
                    {
                        if (reader.Read())
                        {
                            dbStat.TableRows = reader.Get<int>("Rows");
                        }
                    }
                }
                catch (Exception ex)
                {
                    TShock.Log.Error(ex.ToString());
                    Console.WriteLine(ex.StackTrace);
                }

                sql = "select count(id) as Count, strftime('%Y',timestamp) as Year, strftime('%Y',TimeStamp) as Month from logs group by strftime('%Y',timestamp), strftime('%Y',TimeStamp)";

                List<LogStat> Loglist = new List<LogStat>();
            try
            {
                using (var reader = TShock.DB.QueryReader(sql))
                {
                    if (reader.Read())
                    {
                        rec = new LogStat(reader.Get<Int32>("Count"), Convert.ToInt32(reader.Get<string>("Year")), Convert.ToInt32(reader.Get<string>("Month")));
                        Loglist.Add(rec);
                    }
                }
                return new RestObject() { { "Rows", Loglist }, { "Stats", dbStat }, { "version", Assembly.GetExecutingAssembly().GetName().Version.ToString() } };
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }
            }
            return null;
        }

        public static RestObject getRows(RestRequestArgs args)
        {
            if (!TShock.Config.UseSqlLogs)
                return new RestObject("400") { Response = "UseSqlLogs not set to true.", };

            string searchString = Convert.ToString(args.Parameters["search"]);
            if (searchString == null)
                searchString = "";

            List<Log> logList = GetRows(searchString);

            return new RestObject() { { "Rows", logList } };

        }
        /// <summary>
        /// Gets a list of Log entries. 
        /// </summary>
        public static List<Log> GetRows(string search)
        {
            Log rec;
            string sql = "SELECT * FROM logs " + search;

            List<Log> Loglist = new List<Log>();
            try
            {
                using (var reader = TShock.DB.QueryReader(sql))
                {
                    while (reader.Read())
                    {
                        rec = new Log(reader.Get<Int32>("ID"), reader.Get<string>("TimeStamp"), reader.Get<Int32>("LogLevel"), reader.Get<string>("Caller"), reader.Get<string>("Message"));
                        Loglist.Add(rec);
                    }
                }
                return Loglist;

            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }
            return null;
        }
        public static RestObject deleteRows(RestRequestArgs args)
        {
            if (!TShock.Config.UseSqlLogs)
                return new RestObject("400") { Response = "UseSqlLogs not set to true.", };

            string sqlString = Convert.ToString(args.Parameters["delete"]);
            if (sqlString == null)
                sqlString = "";

            string sql = "DELETE FROM logs WHERE id in " + sqlString;
            try
            {
                TShock.DB.Query(sql);
                return RestResponse("Entries deleted.");
            }
            catch (Exception ex)
            {
                TShock.Log.Error(ex.ToString());
                Console.WriteLine(ex.StackTrace);
            }

            return new RestObject("400") { Response = "Error in deleting entries", };

        }
        #region Utility Methods

        private static RestObject RestError(string message, string status = "400")
        {
            return new RestObject(status) { Error = message };
        }

        private static RestObject RestResponse(string message, string status = "200")
        {
            return new RestObject(status) { Response = message };
        }

        private static RestObject RestMissingParam(string var)
        {
            return RestError("Missing or empty " + var + " parameter");
        }

        private static RestObject RestMissingParam(params string[] vars)
        {
            return RestMissingParam(string.Join(", ", vars));
        }

        private RestObject RestInvalidParam(string var)
        {
            return RestError("Missing or invalid " + var + " parameter");
        }

        #endregion
    }

    public class Log
    {
        public int ID { get; set; }
        public string TimeStamp { get; set; }
        public int LogLevel { get; set; }
        public string Caller { get; set; }
        public string Message { get; set; }

        public Log(int id, string timeStamp, int loglevel, string caller, string message)
        {
            ID = id;
            TimeStamp = timeStamp;
            LogLevel = loglevel;
            Caller = caller;
            Message = message;
        }

        public Log()
        {
            ID = 0;
            TimeStamp = "";
            LogLevel = 0;
            Caller = "";
            Message = "";
        }
    }
    public class DatabaseStat
    {
        public double DBSize { get; set; }
        public int TableRows { get; set; }
        public double TableSize { get; set; }
        public string DBType { get; set; }

        public DatabaseStat(double dbSize, int tableRows, double tableSize, string dbType)
        {
            DBSize = dbSize;
            TableRows = tableRows;
            TableSize = tableSize;
            DBType = dbType;
         }

        public DatabaseStat()
        {
            DBSize = 0;
            TableRows = 0;
            TableSize = 0;
            DBType = "";
        }
    }
    public class LogStat
    {
        public int Count { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
 
        public LogStat(int count, int year, int month)
        {
            Count = count;
            Year = year;
            Month = month;
        }

        public LogStat()
        {
            Count = 0;
            Year = 0;
            Month = 0;
        }
    }
}