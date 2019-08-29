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
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers.ChatManager;
using Torch.Server;
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

                _config = Persistent<SwitchMeConfig>.Load(configFile);

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
    }
}
