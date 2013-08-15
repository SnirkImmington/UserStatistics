using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace UserStatistics
{
    static class Utils
    {
        #region Objects

        public static string ConfigPath { get; }
        public static string DatabasePath { get; }
        public static string LogPath { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Purges things from the database, as according to the config file.
        /// </summary>
        public static int DatabasePurge()
        {
            try
            {
                int turn = 0;
                foreach (var info in DB.Infos)
                {
                    var count = TShock.Users.GetUserByID(info.AccountID);
                    if (Utils.IsSafe(info, count)) continue;

                    // So... it has come to this.
                    Log("Purging user \"{0}\" (ID {1}): IP = {2}, Group = {3}, Registered {4}, Last Login {5}, Total Time = {6}"
                        .SFormat(count.Name, count.ID, count.Address, count.Group, info.RegisterTime.ToDisplayString(),
                        info.LastLogin.ToDisplayString(), info.TotalTime.ToDisplayString()));
                    TShock.Users.RemoveUser(count);
                    DB.Infos.Remove(info);
                    turn++;
                }
                return turn;
            }
            catch (Exception ex)
            {
                Log("Exception while purging! " + ex.ToString()); return -1;
            }
        }

        public static bool IsSafe(DBInfo info, TShockAPI.DB.User acct = null)
        {
            // Copy account.
            if (acct == null) acct = TShock.Users.GetUserByID(info.AccountID);

            // I love these long config value names.
            if (Config.ProtectPurgeIfLongtimeUser &&
                info.TotalTime > Config.PurgeAfterInactiveTime ||
                (TShock.Groups.GetGroupByName(acct.Group)
                .HasPermission(Config.PurgeProtectionPermission)) ||
                (Config.EnableTimeBasedPurges && Config.PurgeAfterInactiveTime
                < DateTime.Now - info.LastLogin)) return false;

            return true;
        }

        public static void Log(string info)
        {
            // DateTime.Now.ToString() - info
        }

        #endregion

        #region Extensions

        /// <summary>
        /// Returns the DateTime in SQL serialized specific form.
        /// </summary>
        public static string ToSQLString(this DateTime time)
        {
            return time.ToString("dd/MM/yy, hh:mm");
        }
        /// <summary>
        /// Creates a DateTime from serialized form in the SQL database.
        /// </summary>
        /// <param name="input">The text in the SQL table.</param>
        public static DateTime DTFromSQLString(string input)
        { // dd/MM/yy, hh:mm

            var parseComma = input.Split(',');
            // dd/mm/yy = pc[0] // hh:mm = pc[1]

            var dmy = parseComma[0].Trim().Split('/');
            // dd = 0, MM = 1, yy = 2

            var hm = parseComma[1].Trim().Split(':');
            // hh = o, mm = 1

            if (dmy[2].Length != 4) dmy[2] = "20" + dmy[2];

            int day, month, year, hour, min;

            int.TryParse(dmy[0], out day);
            int.TryParse(dmy[1], out month);
            int.TryParse(dmy[2], out year);
            int.TryParse(hm[0], out hour);
            int.TryParse(hm[1], out min);

            return new DateTime(year, month, day, hour, min, 0);
        }
        /// <summary>
        /// Returns a specific DateTime.ToString() for display purposes.
        /// </summary>
        public static string ToDisplayString(this DateTime time)
        {
            //return time.ToString(@"hh\:mm on dd/mm/yy");
            //return string.Format("{0} {1} at {3}:{4}",
            return time.ToString("MMM dd at hh:mm");
                
        }
        /// <summary>
        /// Returns the TimeSpam in SQL serialized string form.
        /// </summary>
        public static string ToSqlString(this TimeSpan time)
        {
            return time.ToString(@"d\:hh\:mm\:ss"); 
        }
        /// <summary>
        /// Constructor from the SQL serialized string form.
        /// </summary>
        /// <param name="input">The string from SQL table.</param>
        public static TimeSpan TSFromSQLString(string input)
        {
            var times = input.Split(':');
            // d:hh:mm:ss
            int day, hour, min, sec;
            int.TryParse(times[0], out day);
            int.TryParse(times[1], out hour);
            int.TryParse(times[2], out min);
            int.TryParse(times[3], out sec);

            return new TimeSpan(day, hour, min, sec);
        }
        /// <summary>
        /// A TimeSpam.ToString() specifically for plugin's display purposes.
        /// </summary>
        public static string ToDisplayString(this TimeSpan time)
        {
            return time.ToString("dd days, hh hours and m minutes");
        }

        #endregion
    }
}
