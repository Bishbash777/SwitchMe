using NLog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Sandbox;
using Sandbox.Game.Entities;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game.ModAPI;
using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Groups;
using System.Collections.Concurrent;
using System.Net;
using System.Collections.Specialized;
using VRageMath;
using Sandbox.Engine.Multiplayer;
using VRage.Network;
using VRage.Replication;
using VRage.Collections;
using Torch.Utils;
using System.Collections;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using System.Net.Http;
using Sandbox.Game;
using Sandbox.Common.ObjectBuilders;
using VRage;
using VRage.ObjectBuilders;
using Torch;

namespace SwitchMe {

    [Category("switch")]
    public class Commands : CommandModule {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public SwitchMePlugin Plugin => (SwitchMePlugin) Context.Plugin;

        [Command("me", "Automatically connect to your server of choice within this network. USAGE: !switch me <Insert Server name here>")]
        [Permission(MyPromoteLevel.None)]
        public async Task SwitchLocalAsync() {
            IMyPlayer player = Context.Player;
            if (player == null) {
                Context.Respond("Command cannot be ran from console");
                return;
            }
            if (!Plugin.Config.Enabled) {
                Context.Respond("Switching is not enabled!");
                return;
            }
            VoidManager voidManager = new VoidManager(Plugin);
            await voidManager.PlayerTransfer("single", Sandbox.Game.Multiplayer.Sync.MyId);
        }


        [Command("debug")]
        [Permission(MyPromoteLevel.Admin)]
        public void debug(bool state) {
            Plugin.debug = state;
            Context.Respond($"Debug mode set to {Plugin.debug} ");
        }
        
        [Command("update-debug")]
        [Permission(MyPromoteLevel.Admin)]
        public void Updatedebug(bool state) {
            Plugin.update_debug = state;
            Context.Respond($"Debug mode set to {Plugin.update_debug} ");
        }

        [Command("all", "Automatically connects all players to your server of choice within this network. USAGE: !switch all <Insert Server name here>")]
        [Permission(MyPromoteLevel.Admin)]
        public async Task SwitchAllAsync() {
            ulong steamid = Sandbox.Game.Multiplayer.Sync.MyId;
            VoidManager voidManager = new VoidManager(Plugin);
            await voidManager.PlayerTransfer("all", steamid);
        }

        [Command("list", "Displays a list of Valid Server names for the '!switch me <servername>' command. ")]
        [Permission(MyPromoteLevel.None)]
        public async Task SwitchListAsync() {
            APIMethods API = new APIMethods(Plugin);

            if (!Plugin.Config.Enabled) {
                Context.Respond("Switching is not enabled!");
                return;
            }

            StringBuilder sb = new StringBuilder();
            string name;

            

            foreach (ConfigObjects.Server server in Plugin.Config.Servers) {

                name = server.ServerName;
                string target = server.ServerIP + ":" + server.ServerPort.ToString();
                bool paired = await API.CheckKeyAsync(target);

                if (paired == true) 
                    sb.Append("'" + name + "' ");
            }
            Log.Info($"Servers available to switch to: {sb}");
            Context.Respond("--------------------------");
            Context.Respond("List of Servers available to switch to:");
            Context.Respond(sb.ToString());
            Context.Respond("--------------------------");
        }

        [Command("help", "Displays a list of Valid Server names for !switch me <servername> ")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchHelp() {

            Context.Respond("`!switch me <servername>` Switches you to selected server.");
            Context.Respond("`!switch list` Displays a list of valid Server names to connect to.");
            Context.Respond("`!switch grid '<targetgrid>' '<targetserver>'` Transfers the target grid to the target server.");
            Context.Respond("`!switch recover` Completes the transfer of a grid");
            Context.Respond("`!switch gates` Displayes the GPS locations of valid jump gates.");
        }

 
        [Command("gates","Get the gps locations of jump gates in this server")]
        [Permission(MyPromoteLevel.None)]
        public void GetGates() {
            ConfigObjects configObjects = new ConfigObjects();
            Context.Respond("Getting GPS locations for active jumpgates...");
            foreach (ConfigObjects.Gate gate in Plugin.Config.Gates) {
                Context.Respond($"GPS:{configObjects.ParseConvertXYZObject(gate.GateLocation)}");
            }
        }

        [Command("recover", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public async void Recover() {
            IMyPlayer player = Context.Player;
            if (player == null) {
                Context.Respond("Command cannot be ran from console");
                return;
            }
            APIMethods API = new APIMethods(Plugin);

            string externalIP = utils.CreateExternalIP(Plugin.Config);
            string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;
            VoidManager voidManager = new VoidManager(Plugin);

            
            Tuple<string, string, Vector3D> data = await voidManager.DownloadGridAsync(currentIp, Context.Player.SteamUserId, Context.Player.GetPosition().ToString());

            if (data == null)
            {
                return;
            }
            string targetFile = data.Item1;
            string filename = data.Item2;
            Vector3D newPos = data.Item3;
            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                GridImporter gridManager = new GridImporter(Plugin, Context);
                if (gridManager.DeserializeGridFromPath(targetFile, Context.Player.DisplayName, newPos))
                {
                    File.Delete(targetFile);
                }
            });
            await API.MarkCompleteAsync(Context.Player.SteamUserId);
            await API.RemoveConnectionAsync(Context.Player.SteamUserId);
            utils.RefreshPlayer(Context.Player.SteamUserId);
        }

        [Command("restore", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Restore() {
            Recover();
        }

        [Command("reload", "Reload and refresh jumpgates with debug options")]
        [Permission(MyPromoteLevel.Admin)]
        public async void Reload() {
            APIMethods API = new APIMethods(Plugin);
            if (Plugin.Config.UseOnlineConfig) {
                Context.Respond("Online config mode enabled! Reloading online config");
                var api_response = await API.LoadOnlineConfig();
                if (api_response["responseCode"] == "0") {
                    WebClient myWebClient = new WebClient();
                    myWebClient.DownloadFile($"{Plugin.API_URL + api_response["path"]}", Path.Combine(Plugin.StoragePath, "SwitchMeOnline.cfg"));
                    Plugin._config = Persistent<SwitchMeConfig>.Load(Path.Combine(Plugin.StoragePath, "SwitchMeOnline.cfg"));
                    Plugin.Save();
                }
            }
            Plugin.CloseGates();
            Plugin.OpenGates();
            Context.Respond("Plugin reloaded!");
        }

        [Command("password", "retrieve pre-generated password for use on the online configuration panel")]
        [Permission(MyPromoteLevel.Owner)]
        public void Password() {
            Context.Respond($"Your online config password is: {utils.GetMachineId()}");
            Log.Info($"Config panel password: {utils.GetMachineId()}");
        }
    }
}

