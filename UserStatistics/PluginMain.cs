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
        private StatPlayer[] StatPlayers;

        #endregion


        #region Beautiful Code Rectangle

        public override string    Name   { get { return "User Statistics"; } }
        public override string   Author  { get { return "Snirk Immington"; } }
        public override string Description { get { return "I'mSoOCDRight"; } }
        public override Version Version  { get { return new Version(1, 0); } }
        public PlugMain(Main game) : base(game) { Order = 2; Config.Setup(); }

        #endregion


        #region Inititialize

        public override void Initialize()
        {
            StatPlayers = new StatPlayer[255];

            // Set up database

            GameHooks.Update += OnUpdate;
            NetHooks.GreetPlayer += OnGr;
            ServerHooks.Leave += OnLeave;

            #region StartupPurge
            if (Config.PurgeOnStartup)
            {
                Console.WriteLine("UserStatistics is purging the database of old entries...");
                // TODO log

                int @return = Utils.DatabasePurge();

                if (@return == -1) TSPlayer.Server.SendErrorMessage("The user database purge has failed!");
                else Console.WriteLine("Removed {0} users from the user database.");
            }
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
                // TODO save database
            }
            base.Dispose(disposing);
        }
        private void OnFail(object e, UnhandledExceptionEventArgs a)
        {
            if (a.IsTerminating)
            { } // TODO save database
        }

        #endregion


        #region Hooks

        private void OnGr(int who, HandledEventArgs e)
        {
            StatPlayers[who] = new StatPlayer(TShock.Players[who]);
            // Login and stuff handled in constructor.
        }
        private void OnLeave(int who)
        {
            StatPlayers[who].LogOut();
            StatPlayers[who] = null;
        }
        public void OnUpdate()
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
                            StatPlayers[i].LogOut();
                            StatPlayers[i].LogIn(); // TODO implement
                        }
                        else
                        {
                            StatPlayers[i].UpdateSession();
                        }
                    }
                }
            }
            #endregion

            #region Do a purge

            if (Config.EnableOldAccountPurge && (DateTime.Now - LastPurge).Minutes >= Config.PurgeCheckDelay)
            {LastPurge = DateTime.Now;

                TSPlayer.All.SendInfoMessage("User Statistics is purging the database of old user accounts...");
                Utils.Log("Purge: Purge occured at the config-set purge interval.");
                int ret = Utils.DatabasePurge();
                TSPlayer.All.SendInfoMessage("Database purge complete.");

                if (Config.AdminLogPurgeStatsAndErrors)
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

        public static void PurgeCommand(CommandArgs com)
        {
            if (!Config.CreateSuperadminPurgeCommand)
            {
                com.Player.SendErrorMessage("Config has been changed to prevent this command! Will not appear with restart."); return;
            }
            if (!Config.EnableOldAccountPurge)
            {
                com.Player.SendErrorMessage("Config allows for purge command and not purge itself!"); return;
            }
            if (com.Parameters.Count == 0 || com.Parameters[1].ToLower() != "confirm")
            {
                com.Player.SendWarningMessage("This is the purge command. User Statistics will purge the database of old/unused user accounts.");
                com.Player.SendInfoMessage("This command is not to be executed accidentally. To use it, you must type \"/purge confirm\" for safety.");
                return;
            }
            
            com.Player.SendInfoMessage("Purging the user database...");
            // Perform a purge
            Utils.Log("Purge: {0} executed /purge.".SFormat(com.Player.Name));
            int purged = Utils.DatabasePurge();
            if (purged != -1)
            {
                com.Player.SendSuccessMessage("Removed {0} accounts!".SFormat(purged));
                Utils.Log("Purge: removed {0} old user accounts.".SFormat(purged));
            }
            else com.Player.SendErrorMessage("Error in the purge!");
        }
        public static void SelfInfo(CommandArgs com)
        {
            com.Player.SendInfoMessage("Account statistics for {0}:".SFormat(com.Player.UserAccountName));
            com.Player.SendSuccessMessage("Registered {0} | Member for {1}");

            if (Config.UserInfoIncludesWhetherAccountIsSafe)
            {
                bool isSafe = false;
            }
        }
        public static void ReloadConfig(CommandArgs com)
        {
            Config.Setup();
            com.Player.SendInfoMessage("Reloaded the User Statistics config file!");
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
        public DBInfo StatInfo { get; set; }
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
            StatInfo = DB.GetPlayerInfo(ExpectedID);
            StatInfo.LastLogin = DateTime.Now;
            LastCheck = DateTime.Now;

            if (Config.LoginTimeGreeting)
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
            StatInfo.TotalTime += StatInfo.LastLogin - DateTime.UtcNow;
            // Object reference, Database object is updated.
        }

        public void UpdateSession()
        {
            StatInfo.TotalTime += DateTime.Now - LastCheck;
            LastCheck = DateTime.Now;

            if (Config.ProtectPurgeIfLongtimeUser && // I love this if statement.
                Config.NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage != "" &&
                !TShock.Players[Index].Group.HasPermission(Config.PurgeProtectionPermission) &&
                StatInfo.TotalTime - Config.PurgeAfterInactiveTime > TimeSpan.Zero &&
                StatInfo.TotalTime - Config.PurgeAfterInactiveTime < TimeSpan.FromSeconds(5))
            {
                TShock.Players[Index].SendSuccessMessage(Config.NotifyUsersIfTheyDontHavePermsButPassPurgeTimeMessage);
            }
        }
    }
}
