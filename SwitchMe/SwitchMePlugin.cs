using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using Torch;
using System.Text;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Server;
using Torch.Managers.ChatManager;
using Torch.Session;
using VRage.Game.ModAPI;

namespace SwitchMe
{
    public sealed class SwitchMePlugin : TorchPluginBase, IWpfPlugin
    {
        public SwitchMeConfig Config => _config?.Data;

        private Persistent<SwitchMeConfig> _config;

        //public SwitchMe DDBridge;

        private UserControl _control;
        public static string ip;
        private Timer _timer;
        private DateTime timerStart = new DateTime(0);
        private TorchSessionManager _sessionManager;


        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new SwitchMeControl(this));

        public void Save() => _config?.Save();

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var configFile = Path.Combine(StoragePath, "SwitchMe.cfg");

            try
            {
                StartTimer();
                _config = Persistent<SwitchMeConfig>.Load(configFile);
                timerStart = new DateTime(0);

            }
            catch (Exception e)
            {
                Log.Warn(e);
            }

            if (_config?.Data == null)
            {

                Log.Info("Create Default Config, because none was found!");

                _config = new Persistent<SwitchMeConfig>(configFile, new SwitchMeConfig());
                Save();
            }
        }
        public string CheckSlots(string targetIP)
        {
            string pagesource = "";
            try
            {
                string maxPlayers = MySession.Static.MaxPlayers.ToString();
                string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
                string currentIp = Sandbox.MySandboxExternal.ConfigDedicated.IP + ":" + Sandbox.MySandboxExternal.ConfigDedicated.ServerPort;
                using (WebClient client = new WebClient())
                {

                    NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            { "currentplayers", currentPlayers }, {"maxplayers", maxPlayers }, {"targetip", targetIP},
                        };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/index.php", postData));
                }
            }

            catch
            {
                Log.Warn("Cannot connect to captainjackyt.com database.");
            }

            return pagesource;
        }
        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            if (!Config.Enabled) return;

            switch (state)
            {
                case TorchSessionState.Loaded:

                    //load
                    LoadSEDB();


                    break;
                case TorchSessionState.Unloaded:

                    //unload
                    timerStart = new DateTime(0);

                    UnloadSEDB();

                    break;
                default:
                    // ignore
                    break;
            }
        }
        public void StartTimer()
        {
            if (_timer != null) StopTimer();

            _timer = new Timer(3000);
            _timer.Elapsed += _timer_Elapsed;
            _timer.Enabled = true;
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Enabled = false;
                _timer.Dispose();
                _timer = null;
            }
        }
        public void UnloadSEDB()
        {
            Dispose();
        }


        public void LoadSEDB()
        {
            if (_sessionManager == null)
            {
                _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
                if (_sessionManager == null)
                {
                    Log.Warn("No session manager loaded!");
                }
                else
                {
                    _sessionManager.SessionStateChanged += SessionChanged;
                }
            }
            if (Torch.CurrentSession != null)
            {
                InitPost();
            }
        }


        private void InitPost()
        {
            StartTimer();
        }
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string maxPlayers = MySession.Static.MaxPlayers.ToString();
            string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
            string currentIp = Sandbox.MySandboxExternal.ConfigDedicated.IP + ":" + Sandbox.MySandboxExternal.ConfigDedicated.ServerPort;
            if (Torch.CurrentSession != null)
            {
                using (WebClient client = new WebClient())
                {
                    string pagesource = "";
                    NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            { "currentplayers", currentPlayers }, {"maxplayers", maxPlayers }, {"serverip", currentIp},
                        };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/index.php", postData));
                }
            }
            else
            {
            }
        }
    }
}
