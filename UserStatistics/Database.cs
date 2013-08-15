using System;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI.DB;
using System.Data;
using TShockAPI;
using System.IO;
using Mono.Data.Sqlite;

namespace UserStatistics
{
    static class DB
    {
        public static List<DBInfo> Infos = new List<DBInfo>();
        public static DBInfo GetPlayerInfo(int ID)
        {
            var turn = Infos.FirstOrDefault(i => i.AccountID == ID);

            if (turn == null)
                return new DBInfo(ID);

            return turn;
        }

        #region SQL setup

        private static IDbConnection Sequel;

        public static void SetupDB()
        {
            #region Get db type switch case
            switch (TShockAPI.TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    Sequel = new MySqlConnection()
                    {
                        ConnectionString = "Server={0}; Port={1}; Database={2}; Uid={3}, Pwd={4};"
                            .SFormat(host[0], host.Length == 1 ? "3306" : host[1],
                            TShock.Config.MySqlDbName, TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)
                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "User Statistics.sqlite");
                    Sequel = new SqliteConnection("uri=file://{0},Version=3".SFormat(sql));
                    break;
            }
            #endregion

            var creator = new SqlTableCreator(Sequel, Sequel.GetSqlType() == SqlType.Sqlite ? 
                (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            creator.EnsureExists(new SqlTable("Statistics",
                new SqlColumn("UserID", MySqlDbType.Int16) { Primary = true },
                new SqlColumn("RegisterTime", MySqlDbType.Text),
                new SqlColumn("LastLogin", MySqlDbType.Text),
                new SqlColumn("TotalTime", MySqlDbType.Text)));
        }
        private static void ReadDB()
        {
            var deletedUsers = new List<int>(); Infos.Clear();

            using (var reader = Sequel.QueryReader("Select * from Statistics"))
            {
                while (reader.Read())
                {
                    var ID = reader.Get<int>("UserID");

                    // Check to see if account has been deleted.
                    if (TShock.Users.GetUserByID(ID) == null) deletedUsers.Add(ID);

                    else // The account is valid.
                    {
                        Infos.Add(new DBInfo()
                        {
                            AccountID = ID,
                            RegisterTime = Utils.DTFromSQLString(reader.Get<string>("RegisterTime")),
                            LastLogin = Utils.DTFromSQLString(reader.Get<string>("LastLogin")),
                            TotalTime = Utils.TSFromSQLString(reader.Get<string>("TotalTime"))
                        });
                    }
                }
            }
            foreach (var id in deletedUsers) // get rid of old ID's
            {
                Sequel.Query("DELTE FROM Statistics WHERE UserID = @0", id);
            }
        }

        #endregion
    }

    /// <summary>
    /// Object of the database's tables.
    /// </summary>
    class DBInfo
    {
        public int AccountID { get; set; }
        public DateTime RegisterTime { get; set; }
        public DateTime LastLogin { get; set; }
        public TimeSpan TotalTime { get; set; }

        public bool LongTimeUser { get { return TotalTime >= Config.LongtimeUserLength; } }

        public DBInfo(int ID)
        {
            AccountID = ID;
            RegisterTime = DateTime.UtcNow;
        }
        public DBInfo() { } // Constructor for DB
    }
}
