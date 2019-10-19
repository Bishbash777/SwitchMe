using NLog;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using Torch.API.Plugins;
using Torch.Session;
using System.Windows.Controls;
using System.Threading;
using Torch.API.Managers;
using System;
using System.Timers;
using System.Collections.Specialized;
using System.Net;
using Timer = System.Timers.Timer;
using Torch.API.Session;
using Torch.API;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;

using System.IO;
using Torch.Managers;

namespace SwitchMe
{
    public sealed class SwitchMePlugin : TorchPluginBase, IWpfPlugin
    {
        public SwitchMeConfig Config => _config?.Data;

        private Persistent<SwitchMeConfig> _config;

        private UserControl _control;
        public static string ip;
        private Timer _timer;
        private DateTime timerStart = new DateTime(0);
        private TorchSessionManager _sessionManager;
        private IMultiplayerManagerBase _multibase;



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

                Log.Info("Creating default confuration file, because none was found!");

                _config = new Persistent<SwitchMeConfig>(configFile, new SwitchMeConfig());
                
                
                Save();
            }
        }


        private void _multibase_PlayerJoined(IPlayer obj)
        {
            if (!Config.Enabled) return;

        }


        public void Delete(string entityName)
        {
            var name = entityName;
            if (string.IsNullOrEmpty(name))
                return;

            if (!TryGetEntityByNameOrId(name, out IMyEntity entity))
            {
                return;
            }

            if (entity is IMyCharacter)
            {
                return;
            }

            entity.Close();
            Log.Warn("Entitiy deleted.");
        }

        public async Task<string> CheckSlotsAsync(string targetIP)
        {
            string pagesource = "";
            try
            {
                string maxPlayers = MySession.Static.MaxPlayers.ToString();

                string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();

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
                Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
            }

            return await Task.FromResult(pagesource);
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
            Task.Run(() => _timer.Elapsed += _timer_Elapsed);
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

        public async Task<bool> CheckKeyAsync(string target)
        {
           string pagesource;
            try
            {
                using (WebClient client = new WebClient())
                {
                    NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"targetip", target},
                            {"bindKey", Config.LocalKey },
                            {"bindCheck", "1"}
                        };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/index.php", postData));
                    if (pagesource == Config.LocalKey)
                    {
                        return await Task.FromResult(true);
                    }
                }
            }
            catch
            {
                Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
            }
            return false;
        }

        public bool CheckInbound(string target)
        {
            string pagesource;
            try
            {
                using (WebClient client = new WebClient())
                {
                    NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"targetip", target},
                        };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/endpoint.php", postData));
                    if (pagesource == "Y")
                    {
                        return true;
                    }
                }
            }
            catch
            {
                Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
            }
            return false;
        }

        public string debug()
        {
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            if (Config.LocalKey == "10551Debug")
            {
                string pagesource;
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"key", Config.ActivationKey},
                            {"currentIP", currentIp}
                        };
                        pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/test.php", postData));
                        return pagesource;
                    }
                }
                catch
                {
                    Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
                }
            }
            return null;
        }


        public bool CheckStatus(string target)
        {
            string pagesource;
            try
            {
                using (WebClient client = new WebClient())
                {
                    NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"targetip", target},
                        };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/status.php", postData));
                    if (pagesource == "ONLINE")
                    {
                        return true;
                    }
                }
            }
            catch
            {
                Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
            }
            return false;
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
                _multibase.PlayerJoined += _multibase_PlayerJoined;
                InitPost();
            }
        }



        public override void Dispose()
        {
            if (_multibase != null)
            {
                _multibase.PlayerJoined -= _multibase_PlayerJoined;
            }
            _multibase = null;

            if (_sessionManager != null)
                _sessionManager.SessionStateChanged -= SessionChanged;
            _sessionManager = null;;
            StopTimer();
        }



        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity)
        {
            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            foreach (var ent in MyEntities.GetEntities())
            {
                if (ent.DisplayName == nameOrId)
                {
                    entity = ent;
                    return true;
                    
                }
            }

            entity = null;
            return false;
        }


        public static MyIdentity GetIdentityByName(string playerName)
        {

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
                if (identity.DisplayName == playerName)
                    return (MyIdentity)identity;

            return null;
        }

       int i = 0;
        private void InitPost()
        {
            StartTimer();
        }

        public void DeleteFromWeb(string filename)
        {
            using (WebClient client = new WebClient())
            {
                string pagesource = "";
                NameValueCollection postData = new NameValueCollection()
                    {   {"remove", "1"},{"filename", filename}
                };

                pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridRecovery.php", postData));
            }
        }
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            string externalIP;
            string Inbound = "N";
            if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
            {
                externalIP = Config.LocalIP;
                if (Config.LocalIP == "" || Config.LocalIP == null)
                {
                    i++;
                    if (i == 600) { Log.Warn("Please have your public ip set in the SwitchMe Config."); i = 0;  }
                }
                
            }
            else
            {
                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            }
            
            if (timerStart.Ticks == 0) timerStart = e.SignalTime;
            string maxPlayers = MySession.Static.MaxPlayers.ToString();
            string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            if (Torch.CurrentSession != null && currentIp.Length > 1)
            {
                if (Config.InboundTransfersState == true)
                {
                    Inbound = "Y";
                }
                using (WebClient client = new WebClient())
                {
                    string pagesource = "";
                    NameValueCollection postData = new NameValueCollection()
                    {
                        //order: {"parameter name", "parameter value"}
                        { "currentplayers", currentPlayers },
                        { "maxplayers", maxPlayers },
                        { "serverip", currentIp},
                        { "verion", "1.2.4.1"},
                        { "bindKey", Config.LocalKey},
                        { "inbound", Inbound }
                    };
                    
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/index.php", postData));
                }
            }
        }
    }
}
