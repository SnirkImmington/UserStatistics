using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace UserStatistics
{
    class ConfigObject
    {
        #region Properties

        public bool EnableOldAccountPurge = false;
        public int PurgeCheckDelay = 60;
        public bool PurgeOnStartup = false;

        public bool CreatePurgeCommand = false;
        public string PurgeCommandPermission = "*";
        public bool AdminLogPurgeStatsAndErrors = false;

        public bool EnableTimeBasedPurges = true;
        public TimeSpan PurgeAfterInactiveTime = new TimeSpan(30, 0, 0, 0); // 30 days

        public bool ProtectPurgeIfLongtimeUser = false;
        public TimeSpan LongtimeUserLength = new TimeSpan(12, 0, 0); // 12 hours logged in

        public string NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage = "";
        //public static string LoginGreetingMessage = "Welcome back, {0}. You last logged in at {1}, and your account {2} be purged if you don't check in every {3}.";

        public string PurgeProtectionPermission = "dontpurge";

        public string AdminSeeUserTimeInfoPermission = TShockAPI.Permissions.userinfo;
        public string AdminReloadConfigCommmandPermission = TShockAPI.Permissions.maintenance;
        public bool PreventAdminSpyingOnAdminUserData = false;
        public string SeeSelfTimeInfoPermission = "mytime";

        public bool UserInfoIncludesWhetherAccountIsSafe = false;

        public bool LoginTimeGreeting = false;

        #endregion

        #region Setup

        public static void Setup()
        {
            try
            {
                if (!File.Exists(Utils.ConfigPath))
                {
                    File.WriteAllText(Utils.ConfigPath, JsonConvert.SerializeObject(new ConfigObject(), Formatting.Indented));
                }
                else
                {
                    Utils.Config = JsonConvert.DeserializeObject<ConfigObject>(File.ReadAllText(Utils.ConfigPath));
                }
            }
            catch (Exception ex)
            {
                TShockAPI.Log.Error("Failed to set up database: " + ex.ToString());
            }
        }

        #endregion
    }
}
