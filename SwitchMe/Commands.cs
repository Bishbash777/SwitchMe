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
                bool paired = await Plugin.CheckKeyAsync(target);

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
            await RemoveConnection(Context.Player.SteamUserId);
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

        public async Task RemoveConnection(ulong player) {;
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            if (!externalIP.Contains("0.0")
                || !externalIP.Contains("127.0")
                || !externalIP.Contains("192.168")) {
                externalIP = Plugin.Config.LocalIP;
            }

            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            Log.Warn("Removing conneciton flag for " + player);
            using (HttpClient client = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("BindKey", Plugin.Config.LocalKey),
                    new KeyValuePair<string, string>("CurrentIP", currentIp ),
                    new KeyValuePair<string, string>("RemoveConnection", player.ToString())
                };
                FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                await client.PostAsync(Plugin.API_URL, content);
            }
        }

        /*
        [Command("grid", "Transfers the target grid to the target server")]
        [Permission(MyPromoteLevel.None)]
        public async Task GridAsync(string gridTarget, string serverTarget) {

            if (!Plugin.Config.Enabled) {
                Context.Respond("SwitchMe not enabled");
                return;
            }
            if (Context.Player == null) {
                Context.Respond("Console cannot run this command");
                return;
            }
            if (!Plugin.Config.EnabledTransfers) {
                Context.Respond("Grid Transfers are not enabled!");
                return;
            }

            int i = 0;
            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";

            IEnumerable<string> channelIds = Plugin.Config.Servers;
            foreach (string chId in channelIds) {

                ip = chId.Split(':')[1];
                name = chId.Split(':')[0];
                port = chId.Split(':')[2];
                i++;
            }
            channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(serverTarget));
            foreach (string chId in channelIds) {

                ip = chId.Split(':')[1];
                name = chId.Split(':')[0];
                port = chId.Split(':')[2];
            }

            string target = ip + ":" + port;
            ip += ":" + port;
            if (ip == null || name == null || port == null) {
                Context.Respond("Invalid Configuration!");
                return;
            }


            string slotinfo = await Plugin.CheckSlotsAsync(target);
            existanceCheck = slotinfo.Split(';').Last();
            bool paired = await Plugin.CheckKeyAsync(target);

            if (target.Length < 1) {
                Context.Respond("Unknown Server. Please use '!switch list' to see a list of validated servers!");
                return;
            }

            if (existanceCheck != "1") {
                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                return;
            }

            if (!paired) {
                Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                return;
            }

            if (!Plugin.CheckStatus(target)) {
                Context.Respond("Target server is offline... preventing switch");
                return;
            }

            bool InboundCheck = await Plugin.CheckInboundAsync(target);
            if (!InboundCheck) {
                Context.Respond("The target server does not allow inbound transfers");
                return;
            }

            Log.Warn("Checking " + target);
            int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
            string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
            Log.Warn("MAX: " + max);
            int maxi = int.Parse(max);
            int maxcheck = 1 + currentRemotePlayers;
            Context.Respond("Slot Checking...");
            Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);
            if (maxcheck > maxi && Context.Player.PromoteLevel != MyPromoteLevel.Admin) {
                Log.Warn("Not enough slots available.");
                Context.Respond("No slots available.");
                return;
            }
            var player = MySession.Static.Players.GetPlayerByName(Context.Player.DisplayName);
            if (player != null) {
                // If he is online we check if he is currently seated. If he is eject him.
                if (player?.Controller.ControlledEntity is MyCockpit controller) {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                        controller.Use();
                    });
                }
                try {

                    string externalIP = utils.CreateExternalIP(Plugin.Config);
                    string pagesource = "";
                    string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;

                    //Not sure what this does but it does not belong here 
                    using (WebClient client = new WebClient()) {
                        NameValueCollection postData = new NameValueCollection()
                        {
                        {"steamID", Context.Player.SteamUserId + ""},
                        {"currentIP", currentIp },
                        {"gridCheck", ""}
                    };
                        pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridHandle.php", postData));
                    }

                    if (pagesource == "0") {
                        if (!await new VoidManager(Plugin).SendGrid(gridTarget, serverTarget, Context.Player.DisplayName, Context.Player.IdentityId, target)) {
                            return;
                        }
                        Log.Warn("Connected clients to " + serverTarget + " @ " + ip);
                    }
                    else {
                        Log.Fatal(pagesource);
                        Context.Respond("Cannot transfer! You have a transfer ready to be recieved!");
                        return;
                    }
                }
                catch (Exception e) {
                    Log.Fatal(e, e.Message);
                    Context.Respond("Failure");
                }
            }
        }
        */

        [Command("restore", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Restore() {
            Recover();
        }

        [Command("reload", "Reload and refresh jumpgates with debug options")]
        [Permission(MyPromoteLevel.Admin)]
        public void reload() {
            //Delete all registered gates
            int i = 0;
            foreach (var zone in Plugin.zones) {
                foreach (var entity in MyEntities.GetEntities()) {
                    if (entity?.DisplayName?.Contains(zone, StringComparison.CurrentCultureIgnoreCase) ?? false) {
                        i++;
                        entity.Close();
                    }
                }
            }
            IMyPlayer player = Context.Player;
            if (player == null) {
                Context.Respond($"{i} Jumpgates closed!");
            }
            else {
                utils.NotifyMessage($"{i} Jumpgates closed!", Context.Player.SteamUserId);
            }

            //Rebuild all gates
            int gates = 0;
            IEnumerable<string> channelIds = Plugin.Config.Gates;
            string name = "";
            string location = "";
            foreach (string chId in channelIds) {
                name = chId.Split('/')[0];
                location = chId.Split('/')[1];
                location = location.TrimStart('{').TrimEnd('}');
                Vector3D.TryParse(location, out Vector3D gps);
                var ob = new MyObjectBuilder_SafeZone();
                ob.PositionAndOrientation = new MyPositionAndOrientation(gps, Vector3.Forward, Vector3.Up);
                ob.PersistentFlags = MyPersistentEntityFlags2.InScene;
                ob.Shape = MySafeZoneShape.Sphere;
                ob.Radius = Plugin.Config.GateSize;
                ob.Enabled = true;
                ob.DisplayName = $"SM-{gps}";
                ob.AccessTypeGrids = MySafeZoneAccess.Blacklist;
                ob.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;
                ob.AccessTypeFactions = MySafeZoneAccess.Blacklist;
                ob.AccessTypePlayers = MySafeZoneAccess.Blacklist;
                var zone = MyEntities.CreateFromObjectBuilderAndAdd(ob, true);
                gates++;
                if (!Plugin.zones.Contains(ob.DisplayName)) {
                    Plugin.zones.Add(ob.DisplayName);
                }
            }
            if (player == null) {
                Context.Respond($"{gates} Jumpgates created!");
            }
            else {
                utils.NotifyMessage($"{gates} Jumpgates created!", Context.Player.SteamUserId);
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

