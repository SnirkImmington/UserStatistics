using System;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using TShockAPI.DB;
using System.Linq;
using System.Text;
using System.Data;
using TShockAPI;
using System.IO;

namespace UserStatistics
{
    /// <summary>
    /// Contains Infos wrapper and setup/save methods.
    /// </summary>
    static class Database
    {
        /// <summary>
        /// The in-memory objects for unique user accounts. 
        /// May in the future be replaced with SQL queries if 30,000 * 3 datetimes is a lot for RAM.
        /// </summary>
        public static List<DBInfo> Infos = new List<DBInfo>();
        /// <summary>
        /// Searches for an info by user ID, or constructs a new one.
        /// </summary>
        /// <param name="ID">The TShock User ID of the player.</param>
        public static DBInfo GetPlayerInfo(int ID)
        {
            var turn = Infos.FirstOrDefault(i => i.UserID == ID);

            if (turn == null)
            {
                turn = new DBInfo(ID);
                AddSQL(turn);
            }

            return turn;
        }

        #region SQL setup/interface

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
                    Sequel = new SqliteConnection("uri=file://{0},Version=3".SFormat(Utils.DatabasePath));
                    break;

                default: throw new FormatException("UserStatistics: TShock database formatted improperly! Use \"mysql\" or \"sqlite\".");
            }
            #endregion

            var creator = new SqlTableCreator(Sequel, Sequel.GetSqlType() == SqlType.Sqlite ? 
                (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            creator.EnsureExists(new SqlTable("Statistics",
                new SqlColumn("UserID", MySqlDbType.Int32),
                new SqlColumn("RegisterTime", MySqlDbType.Text),
                new SqlColumn("LastLogin", MySqlDbType.Text),
                new SqlColumn("TotalTime", MySqlDbType.Text)));

            ReadDB();
        }
        private static void ReadDB()
        {
            var deletedUsers = new List<int>(); Infos.Clear();

            using (var reader = Sequel.QueryReader("SELECT * FROM Statistics"))
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
                            UserID = ID,
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

        public static void AddSQL(DBInfo info)
        {
            Sequel.Query("INSERT INTO Statistics (UserID, RegisterTime, LastLogin, TotalTime) VALUES (@0, @1, @2, @3)",
                info.UserID, info.RegisterTime.ToSQLString(), info.LastLogin.ToSQLString(), info.TotalTime.ToSqlString());
        }

        public static void UpdateSQL(DBInfo info)
        {
            Sequel.Query("UPDATE Statistics SET TotalTime=@0, LastLogin=@1 WHERE UserID=@2", 
                info.TotalTime.ToSqlString(), info.LastLogin.ToSQLString(), info.UserID);
        }

        public static void DelSQL(int userID)
        {
            Sequel.Query("DELETE FROM Statistics WHERE UserID = @0", userID);
        }

        #endregion
    }

    /// <summary>
    /// Object of the database's tables.
    /// </summary>
    class DBInfo
    {
        public int UserID { get; set; }
        public DateTime RegisterTime { get; set; }
        public DateTime LastLogin { get; set; }
        public TimeSpan TotalTime { get; set; }

        public bool LongTimeUser { get { return TotalTime >= Utils.Config.LongtimeUserLength; } }

        public DBInfo(int ID)
        {
            UserID = ID;
            RegisterTime = DateTime.Now;
        }
        public DBInfo() { } // Constructor for DB
    }
}
