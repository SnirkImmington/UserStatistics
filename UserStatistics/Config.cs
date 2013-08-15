using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserStatistics
{
    static class Config
    {
        #region Properties

        public static bool EnableOldAccountPurge = false;
        public static int PurgeCheckDelay = 60;
        public static bool PurgeOnStartup = false;

        public static bool CreateSuperadminPurgeCommand = false;
        public static bool AdminLogPurgeStatsAndErrors = false;

        public static bool EnableTimeBasedPurges = true;
        public static TimeSpan PurgeAfterInactiveTime = new TimeSpan(30, 0, 0, 0); // 30 days

        public static bool ProtectPurgeIfLongtimeUser = false;
        public static TimeSpan LongtimeUserLength = new TimeSpan(12, 0, 0); // 12 hours logged in

        public static string NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage = "";
        //public static string LoginGreetingMessage = "Welcome back, {0}. You last logged in at {1}, and your account {2} be purged if you don't check in every {3}.";

        public static string PurgeProtectionPermission = "dontpurge";

        public static string SeeUserTimeInfo = TShockAPI.Permissions.userinfo;
        public static string SeeSelfTimeInfo = "mytime";
        public static bool UserInfoIncludesWhetherAccountIsSafe = false;

        public static bool LoginTimeGreeting = false;

        #endregion

        #region Setup

        public static void Setup()
        {
        }

        public static void Save()
        {
        }

        #endregion
    }
}
