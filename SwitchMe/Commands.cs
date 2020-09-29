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

namespace SwitchMe {

    [Category("switch")]
    public class Commands : CommandModule {

        #pragma warning disable 649
        [ReflectedGetter(Name = "m_clientStates")]
        private static Func<MyReplicationServer, IDictionary> _clientStates;

        private const string CLIENT_DATA_TYPE_NAME = "VRage.Network.MyClient, VRage";
        [ReflectedGetter(TypeName = CLIENT_DATA_TYPE_NAME, Name = "Replicables")]
        private static Func<object, MyConcurrentDictionary<IMyReplicable, MyReplicableClientData>> _replicables;

        [ReflectedMethod(Name = "RemoveForClient", OverrideTypeNames = new[] { null, CLIENT_DATA_TYPE_NAME, null })]
        private static Action<MyReplicationServer, IMyReplicable, object, bool> _removeForClient;

        [ReflectedMethod(Name = "ForceReplicable")]
        private static Action<MyReplicationServer, IMyReplicable, Endpoint> _forceReplicable;
        #pragma warning restore 649

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

            IEnumerable<string> channelIds = Plugin.Config.Servers;

            foreach (string chId in channelIds) {

                name = chId.Split(':')[0];
                string ip = chId.Split(':')[1];
                string port = chId.Split(':')[2];
                string target = ip + ":" + port;
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

        [Command("verify", "Verify gate configuration")]
        [Permission(MyPromoteLevel.Admin)]
        public async void verify() {

        }

        [Command("gates","Get the gps locations of jump gates in this server")]
        [Permission(MyPromoteLevel.None)]
        public void GetGates() {
            Context.Respond("Getting GPS locations for active jumpgates...");
            IEnumerable<string> channelIds = Plugin.Config.Gates;
            string name = "";
            string location = "";
            foreach (string chId in channelIds) {
                name = chId.Split('/')[0];
                location = chId.Split('/')[1];
                Context.Respond($"GPS:{name}:{utils.GetSubstringByString("X:","Y", location)}:{utils.GetSubstringByString("Y:", "Z", location)}:{ utils.GetSubstringByString("Z:", "}", location)}");
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
                    Plugin.DeleteFromWeb(Context.Player.SteamUserId);
                }
            });
            await API.RemoveConnectionAsync(Context.Player.SteamUserId);
            var playerEndpoint = new Endpoint(Context.Player.SteamUserId, 0);
            var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
            var clientDataDict = _clientStates.Invoke(replicationServer);
            object clientData;

            try {
                clientData = clientDataDict[playerEndpoint];
            } catch {
                return;
            }

            var clientReplicables = _replicables.Invoke(clientData);

            var replicableList = new List<IMyReplicable>(clientReplicables.Count);
            foreach (var pair in clientReplicables)
                replicableList.Add(pair.Key);

            foreach (var replicable in replicableList) {

                _removeForClient.Invoke(replicationServer, replicable, clientData, true);
                _forceReplicable.Invoke(replicationServer, replicable, playerEndpoint);
            }
        }

        [Command("restore", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Restore() {
            Recover();
        }

        [Command("reload", "Reload and refresh jumpgates with debug options")]
        [Permission(MyPromoteLevel.Admin)]
        public void reload() {
            var player = Context.Player;
            Plugin.CloseGates();
            Plugin.OpenGates();
            if (player == null) {
                Context.Respond($"Jumpgates created!");
            }
            else {
                utils.NotifyMessage($"Jumpgates created!", Context.Player.SteamUserId);
            }
        }


        /*[Command("link")]
        [Permission(MyPromoteLevel.Admin)]
        public void Link(string target) {
            Vector3D Linkpos = Context.Player.GetPosition();
            Plugin.Config.Gates.Add(txtServerName.Text + ":" + txtServerIP.Text + ":" + txtServerPort.Text);
        }
        */
    }
}

