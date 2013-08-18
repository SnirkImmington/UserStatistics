using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI.DB;
using TShockAPI;
using Terraria;
using Hooks;

namespace UserStatistics
{
    [APIVersion(1,12)]
    public class PlugMain : TerrariaPlugin
    {
        #region Properties

        private DateTime LastRefresh = DateTime.Now;
        private DateTime LastPurge = DateTime.Now;
        private DateTime LastUpdate = DateTime.Now;
        private StatPlayer[] StatPlayers;

        #endregion


        #region Beautiful Code Rectangle

        public override string    Name   { get { return "User Statistics"; } }
        public override string   Author  { get { return "Snirk Immington"; } }
        public override string Description { get { return "I'mSoOCDRight"; } }
        public override Version Version  { get { return new Version(1, 1); } }

        #endregion


        #region Inititialize

        public override void Initialize()
        {
            StatPlayers = new StatPlayer[255];

            ConfigObject.Setup();
            Database.SetupDB();
            Utils.InitializeLog();

            GameHooks.Update += OnUpdate;
            NetHooks.GreetPlayer += OnGr;
            ServerHooks.Leave += OnLeave;

            #region StartupPurge Settings
            if (Utils.Config.PurgeOnStartup)
            {
                Console.WriteLine("UserStatistics is purging the database of old entries...");
                // TODO log

                int @return = Utils.DatabasePurge();

                if (@return == -1) TSPlayer.Server.SendErrorMessage("The user database purge has failed!");
                else Console.WriteLine("Removed {0} users from the user database.");
            }
            #endregion

            #region Creation of Commands

            if (Utils.Config.EnableTimeBasedPurges && Utils.Config.PurgeCommandPermission != "")
                Commands.ChatCommands.Add(new Command(Utils.Config.PurgeCommandPermission, PurgeCommand, "purge"));
                
            if (Utils.Config.AdminReloadConfigCommmandPermission != "")
                Commands.ChatCommands.Add(new Command(Utils.Config.AdminReloadConfigCommmandPermission, ReloadConfig, "statsreload"));

            if (Utils.Config.AdminSeeUserTimeInfoPermission != "")
                Commands.ChatCommands.Add(new Command(Utils.Config.AdminSeeUserTimeInfoPermission, UserInfo, "userstats", "us"));

            if (Utils.Config.SeeSelfTimeInfoPermission != "")
                Commands.ChatCommands.Add(new Command(Utils.Config.SeeSelfTimeInfoPermission, SelfInfoCom, "mystats") { AllowServer = false });

            #endregion

            AppDomain.CurrentDomain.UnhandledException += OnFail;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                ServerHooks.Leave -= OnLeave;
                NetHooks.GreetPlayer -= OnGr;
                SaveDatabase();
            }
            base.Dispose(disposing);
        }
        private void OnFail(object e, UnhandledExceptionEventArgs a)
        {
            if (a.IsTerminating) SaveDatabase(); 
        }
        public PlugMain(Main game) : base(game) { Order = 2; }

        #endregion


        #region Hooks

        private void OnGr(int who, HandledEventArgs e)
        {
            StatPlayers[who] = new StatPlayer(TShock.Players[who]);
            // Login and stuff handled in constructor.
        }
        private void OnLeave(int who)
        {
            // Dayum, that exception happened.
            if (StatPlayers[who] != null) StatPlayers[who].LogOut();
            StatPlayers[who] = null;
        }
        private void OnUpdate()
        {
            #region Refresh Login Stuff
            if ((DateTime.Now - LastRefresh).Seconds >= 2)
            { LastRefresh = DateTime.Now;

                for (int i = 0; i < 255; i++)
                {
                    if (StatPlayers[i] != null && TShock.Players[i].RealPlayer)
                    {
                        // Check that login has changed
                        if (StatPlayers[i].ExpectedID != TShock.Players[i].UserID)
                        {
                            // Log the player in.
                            if (StatPlayers[i].ExpectedID != -1) StatPlayers[i].LogOut();

                            StatPlayers[i].LogIn();
                            Utils.Log("Logged in user {0} to statistics database".SFormat(TShock.Players[i].UserAccountName));
                        }
                    }
                }
            }
            #endregion

            #region Update Stats stuff

            if ((DateTime.Now - LastUpdate).TotalMinutes >= 5)
            {
                LastUpdate = DateTime.Now;

                for (int i = 0; i < 255; i++)
                {
                    if (StatPlayers[i] != null && StatPlayers[i].ExpectedID == TShock.Players[i].UserID)
                        StatPlayers[i].UpdateSession();
                }
            }

            #endregion

            #region Do a purge

            if (Utils.Config.EnableOldAccountPurge && (DateTime.Now - LastPurge).Minutes >= Utils.Config.PurgeCheckDelay)
            {
                LastPurge = DateTime.Now;

                TSPlayer.All.SendInfoMessage("User Statistics is purging the database of old user accounts...");
                Utils.Log("Purge: Purge occured at the config-set purge interval.");
                int ret = Utils.DatabasePurge();
                TSPlayer.All.SendInfoMessage("Database purge complete.");

                if (Utils.Config.AdminLogPurgeStatsAndErrors)
                {
                    if (ret == -1)
                    {
                        TShock.Utils.SendLogs("Log: User Statistics database purge failed!", Color.Red);
                        Utils.Log("The purge failed.");
                    }
                    else
                    {
                        TShock.Utils.SendLogs("Log: User statistics purged {0} old users from the database!".SFormat(ret), Color.ForestGreen);
                        Utils.Log("Purged {0} old accounts from the database.".SFormat(ret));
                    }
                }
            }

            #endregion
        }

        #endregion


        #region Commands

        public void PurgeCommand(CommandArgs com)
        {
            #region Various Failures
            if (!Utils.Config.CreatePurgeCommand)
            {
                com.Player.SendErrorMessage("Config has been changed to prevent this command! Will not appear with restart."); return;
            }
            if (!Utils.Config.EnableOldAccountPurge)
            {
                com.Player.SendErrorMessage("Config allows for purge command and not purge itself!"); return;
            }
            if (com.Parameters.Count == 0 || com.Parameters[1].ToLower() != "confirm")
            {
                com.Player.SendWarningMessage("This is the purge command. User Statistics will purge the database of old/unused user accounts.");
                com.Player.SendInfoMessage("This command is not to be executed accidentally. To use it, you must type \"/purge confirm\" for safety.");
                return;
            }
            #endregion

            com.Player.SendInfoMessage("Purging the user database...");

            LastPurge = DateTime.Now;
            Utils.Log("Purge: {0} executed /purge.".SFormat(com.Player.Name));

            int purged = Utils.DatabasePurge();
            if (purged != -1)
            {
                com.Player.SendSuccessMessage("Removed {0} accounts!".SFormat(purged));
                //Utils.Log("Purge: removed {0} old user accounts.".SFormat(purged));
                if (Utils.Config.AdminLogPurgeStatsAndErrors)
                    TShock.Utils.SendLogs("{0} used /purge, purged {1} accounts.".SFormat(com.Player.Name, purged), Color.Yellow);
            }
            else
            {
                com.Player.SendErrorMessage("Error in the purge!");
                if (Utils.Config.AdminLogPurgeStatsAndErrors)
                    TShock.Utils.SendLogs("{0} used /purge, there was an error.".SFormat(com.Player.Name), Color.Red);
            }
        }

        /*
        public void SelfInfo(CommandArgs com)
        {
            com.Player.SendInfoMessage("Account statistics for {0}:".SFormat(com.Player.UserAccountName));
            com.Player.SendSuccessMessage("Registered {0} | Member for {1}");

            if (Utils.Config.UserInfoIncludesWhetherAccountIsSafe && Utils.Config.EnableOldAccountPurge)
            {
                if (Utils.IsSafe(StatPlayers[com.Player.Index].StatInfo))
                    com.Player.SendSuccessMessage("Your account is protected from User Statistics' old account purging.");

                else
                {
                    if (Utils.Config.EnableTimeBasedPurges)
                        com.Player.SendInfoMessage("User Statistics may remove this account if you are inactive for {0}{1}."
                            .SFormat(Utils.Config.PurgeAfterInactiveTime.ToDisplayString(),
                            Config.ProtectPurgeIfLongtimeUser ? ", or if your total login time is greater than "+Utils.Config.LongtimeUserLength : ""));


                    var reasons = new List<string>();

                    if (Utils.Config.EnableTimeBasedPurges) reasons.Add("you are inactive for " + Utils.Config.PurgeAfterInactiveTime.ToDisplayString());
                    if (Utils.Config.ProtectPurgeIfLongtimeUser) reasons.Add("you are online for a total of " + Utils.Config.LongtimeUserLength.ToDisplayString());
                }

            }
        }
        */ // To-be-implemented command with nice syntax.

        public static void ReloadConfig(CommandArgs com)
        {
            Utils.Log(com.Player.Name + " used /statsreload.");
            ConfigObject.Setup();
            com.Player.SendInfoMessage("Reloaded the User Statistics config file.");
        }

        public void UserInfo(CommandArgs com)
        {
            if (com.Parameters.Count == 0)
            {
                com.Player.SendErrorMessage("Usage: /userstats (/us) <player> - gets the User Statistics data of the player's account!"); return;
            }

            var ply = TShock.Utils.FindPlayer(string.Join(" ", com.Parameters));

            if (ply.Count != 1) { com.Player.SendErrorMessage(ply.Count + " players matched!"); return; }

            if (!ply[0].IsLoggedIn) { com.Player.SendErrorMessage(ply[0].Name + " is not logged in."); return; }

            if (Utils.Config.PreventAdminSpyingOnAdminUserData && ply[0].Group.HasPermission(Utils.Config.AdminSeeUserTimeInfoPermission))
            { com.Player.SendErrorMessage("You are not allowed to view other admins' user statistics."); return; }

            var dat = StatPlayers[ply[0].Index];

            com.Player.SendInfoMessage("Account statistics for {0}:".SFormat(ply[0].UserAccountName));
            com.Player.SendSuccessMessage("Registered {0} | Member for {1}".SFormat(dat.StatInfo.RegisterTime.ToDisplayString(), dat.StatInfo.TotalTime.ToDisplayString()));

            if (Utils.Config.EnableOldAccountPurge && Utils.Config.UserInfoIncludesWhetherAccountIsSafe)
            {
                if (Utils.IsSafe(dat.StatInfo)) com.Player.SendInfoMessage("This account is safe from purging.");
                else com.Player.SendInfoMessage("This account is not extra protected from the old accounts purge - if it becomes stale, it will be gone.");
            }
        }

        public void SelfInfoCom(CommandArgs com)
        {
            var dat = StatPlayers[com.Player.Index];
            com.Player.SendInfoMessage("Account statistics for {0}:".SFormat(com.Player.UserAccountName));
            com.Player.SendSuccessMessage("Registered {0} | Member for {1}".SFormat(dat.StatInfo.RegisterTime.ToDisplayString(), dat.StatInfo.TotalTime.ToDisplayString()));

            if (Utils.Config.EnableOldAccountPurge && Utils.Config.UserInfoIncludesWhetherAccountIsSafe)
            {
                if (Utils.IsSafe(dat.StatInfo)) com.Player.SendInfoMessage("Your account is safe from any account purging.");
                else com.Player.SendInfoMessage("You is not extra protected from the purge, if you abandon this account for too long it may eventually be deleted.");
            }
        }

        public void SaveDatabase()
        {
            for (int i = 0; i < 255; i++)
            {
                if (StatPlayers[i] != null && StatPlayers[i].ExpectedID != -1)
                {
                    StatPlayers[i].LogOut();
                }
                
            }
        }

        #endregion
    }

    /// <summary>
    /// Contains plugin-unique information.
    /// </summary>
    public class StatPlayer
    {
        /// <summary>
        /// The index of the player.
        /// </summary>
        public int Index { get; set; }
        /// <summary>
        /// The last known user id of the player.
        /// </summary>
        public int ExpectedID { get; set; }
        /// <summary>
        /// The database-stored info based on userid.
        /// </summary>
        internal DBInfo StatInfo { get; set; }
        /// <summary>
        /// Used to see last login time as well.
        /// </summary>
        public DateTime SessionLogin { get; set; }
        /// <summary>
        /// Gets the time of the last check for updating user time.
        /// </summary>
        public DateTime LastCheck { get; set; }

        /// <summary>
        /// Constructor for onjoin.
        /// </summary>
        public StatPlayer(TSPlayer ply)
        {
            Index = ply.Index;
            ExpectedID = ply.UserID;
            if (ply.IsLoggedIn) LogIn();
        }

        /// <summary>
        /// Registers the player's DBInfo with the database and sets up login timing.
        /// </summary>
        public void LogIn()
        {
            ExpectedID = TShock.Players[Index].UserID;
            StatInfo = Database.GetPlayerInfo(ExpectedID);
            StatInfo.LastLogin = DateTime.Now;
            LastCheck = DateTime.Now;
            Database.UpdateSQL(StatInfo);

            if (Utils.Config.LoginTimeGreeting)
            {
                TShock.Players[Index].SendSuccessMessage("UserStatistics: Total login time for \"{0}\" is {1}."
                    .SFormat(TShock.Players[Index].UserAccountName, StatInfo.TotalTime.ToDisplayString()));
            }
        }
        /// <summary>
        /// Gets logged in time, udpates the DB object.
        /// </summary>
        public void LogOut()
        {
            StatInfo.TotalTime += StatInfo.LastLogin - DateTime.Now;
            Database.UpdateSQL(StatInfo);
        }

        public void UpdateSession()
        {
            StatInfo.TotalTime += DateTime.Now - LastCheck;
            LastCheck = DateTime.Now;

            Database.UpdateSQL(StatInfo);

            if (Utils.Config.ProtectPurgeIfLongtimeUser && // I love this if statement.
                Utils.Config.NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage != "" &&
                !TShock.Players[Index].Group.HasPermission(Utils.Config.PurgeProtectionPermission) &&
                StatInfo.TotalTime - Utils.Config.PurgeAfterInactiveTime > TimeSpan.Zero &&
                StatInfo.TotalTime - Utils.Config.PurgeAfterInactiveTime < TimeSpan.FromMinutes(8))
            {
                TShock.Players[Index].SendSuccessMessage(Utils.Config.NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage);
            }
        }
    }
}
