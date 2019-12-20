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

        private readonly string ExportPath = "ExportedGrids\\{0}.xml";

        public SwitchMePlugin Plugin => (SwitchMePlugin)Context.Plugin;
        [Command("me", "Automatically connect to your server of choice within this network. USAGE: !switch me <Insert Server name here>")]
        [Permission(MyPromoteLevel.None)]
        public async Task SwitchLocalAsync() {

            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";
            int i = 0;

            if (!Plugin.Config.Enabled) {
                Context.Respond("Switching is not enabled!");
                return;
            }

            if (Context.Player == null) {
                Context.Respond("Cannot run this command from outside the server!");
                return;
            }

            IEnumerable<string> channelIds = Plugin.Config.Servers;

            foreach (string chId in channelIds) {

                ip = chId.Split(':')[1];
                name = chId.Split(':')[0];
                port = chId.Split(':')[2];
                i++;
            }

            if (i == 1) {

                string target = ip + ":" + port;
                ip += ":" + port;

                string slotinfo = await Plugin.CheckSlotsAsync(target);
                existanceCheck = slotinfo.Split(';').Last();
                bool paired = await Plugin.CheckKeyAsync(target);

                if (target.Length > 1) {

                    if (existanceCheck == "1") {

                        if (paired == true) {

                            Log.Warn("Checking " + target);

                            int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                            string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);

                            Log.Warn("MAX: " + max);

                            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                            int maxi = int.Parse(max);
                            int maxcheck = (1 + currentRemotePlayers);

                            Context.Respond("Slot Checking...");

                            Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                            if (maxcheck <= maxi) {

                                if (ip == null || name == null || port == null) {
                                    Context.Respond("Invalid Configuration!");
                                }

                                Context.Respond("Slot checking passed!");

                                try {

                                    ulong steamid = Context.Player.SteamUserId;
                                    Context.Respond("Connecting client to " + name + " @ " + target);
                                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                                    Log.Warn("Connected client to " + name + " @ " + ip);

                                } catch {
                                    Context.Respond("Failure");
                                }

                            } else {
                                Context.Respond("Cannot switch, not enough slots available");
                            }

                        } else {
                            Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                        }

                    } else {
                        Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    }

                } else {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                }

            } else {

                channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));

                foreach (string chId in channelIds) {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];
                }

                string target = ip + ":" + port;
                ip += ":" + port;
                string slotinfo = await Plugin.CheckSlotsAsync(target);
                existanceCheck = slotinfo.Split(';').Last();
                bool paired = await Plugin.CheckKeyAsync(target);

                if (target.Length > 1) {

                    if (existanceCheck == "1") {

                        if (paired == true) {

                            Log.Warn("Checking " + target);

                            int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                            string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);

                            Log.Warn("MAX: " + max);

                            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                            int maxi = int.Parse(max);
                            int maxcheck = (1 + currentRemotePlayers);

                            Context.Respond("Slot Checking...");

                            Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                            if (maxcheck <= maxi) {

                                if (ip == null || name == null || port == null) {
                                    Context.Respond("Invalid Configuration!");
                                }

                                Context.Respond("Slot checking passed!");

                                try {

                                    ulong steamid = Context.Player.SteamUserId;
                                    Context.Respond("Connecting client to " + Context.RawArgs + " @ " + ip);
                                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                                    Log.Warn("Connected client to " + Context.RawArgs + " @ " + ip);

                                } catch {
                                    Context.Respond("Failure");
                                }

                            } else {
                                Context.Respond("Cannot switch, not enough slots available");
                            }

                        } else {
                            Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                        }

                    } else {
                        Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    }

                } else {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                }
            }
        }

        [Command("all", "Automatically connects all players to your server of choice within this network. USAGE: !switch all <Insert Server name here>")]
        [Permission(MyPromoteLevel.Admin)]
        public async Task SwitchAllAsync() {

            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";

            if (!Plugin.Config.Enabled) {
                Context.Respond("Switching is not enabled!");
                return;
            }

            int i = 0;
            IEnumerable<string> channelIds = Plugin.Config.Servers;

            foreach (string chId in channelIds) {

                ip = chId.Split(':')[1];
                name = chId.Split(':')[0];
                port = chId.Split(':')[2];
                i++;
            }

            if (i == 1) {

                string target = ip + ":" + port;
                ip += ":" + port;
                string slotinfo = await Plugin.CheckSlotsAsync(target);
                existanceCheck = slotinfo.Split(';').Last();
                bool paired = await Plugin.CheckKeyAsync(target);

                if (target.Length > 1) {

                    if (existanceCheck == "1") {

                        if (paired == true) {

                            Log.Warn("Checking " + target);

                            int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                            string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);

                            Log.Warn("MAX: " + max);

                            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                            int maxi = int.Parse(max);
                            int maxcheck = currentLocalPlayers + currentRemotePlayers;

                            Context.Respond("Slot Checking...");

                            Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                            if (maxcheck <= maxi) {

                                if (ip == null || name == null || port == null) {
                                    Context.Respond("Invalid Configuration!");
                                }

                                Context.Respond("Slot checking passed!");

                                try {

                                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                                    ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);

                                } catch {
                                    Context.Respond("Failure");
                                }

                            } else {
                                Context.Respond("Cannot switch, not enough slots available");
                            }

                        } else {
                            Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                        }

                    } else {
                        Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    }

                } else {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                }

            } else {

                channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));

                foreach (string chId in channelIds) {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];
                }

                string target = ip + ":" + port;
                ip += ":" + port;
                string slotinfo = await Plugin.CheckSlotsAsync(target);
                existanceCheck = slotinfo.Split(';').Last();
                bool paired = await Plugin.CheckKeyAsync(target);

                if (target.Length > 1) {

                    if (existanceCheck == "1") {

                        if (paired == true) {

                            Log.Warn("Checking " + target);

                            int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                            string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);

                            Log.Warn("MAX: " + max);

                            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                            int maxi = int.Parse(max);
                            int maxcheck = currentLocalPlayers + currentRemotePlayers;

                            Context.Respond("Slot Checking...");

                            Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                            if (maxcheck <= maxi) {

                                if (ip == null || name == null || port == null) {
                                    Context.Respond("Invalid Configuration!");
                                }

                                Context.Respond("Slot checking passed!");

                                try {

                                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                                    ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);

                                } catch (Exception e) {
                                    Context.Respond("Failure");
                                    Log.Warn(e.Message);
                                }

                            } else {
                                Context.Respond("Cannot switch, not enough slots available");
                            }

                        } else {
                            Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                        }

                    } else {
                        Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    }

                } else {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                }
            }
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
        }

        [Command("debug", "")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchDebug() {

            string output = Plugin.Debug();
            Context.Respond(output);
            Log.Warn(output);
        }

        [Command("recover", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Recover() {

            if (Context.Player == null) {
                Context.Respond("Command cannot be ran from console");
                return;
            }

            string externalIP = CreateExternalIP();
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            if (DownloadGrid(currentIp, out string targetFile, out string filename, out Vector3D newPos)) {

                GridImporter gridManager = new GridImporter(Plugin, Context);

                if (gridManager.DeserializeGridFromPath(targetFile, Context.Player.IdentityId, newPos)) {

                    File.Delete(targetFile);
                    Plugin.DeleteFromWeb(currentIp);
                }

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
        }


        public ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName) {

            int i = 0;
            bool foundIdentifier = false;

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();

            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group => {

                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes) {

                    IMyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.CustomName.Equals(gridName)) {
                        i++;
                        continue;
                    }

                    groups.Add(group);
                    foundIdentifier = true;
                }
            });

            if (i >= 1 && !foundIdentifier) 
                Context.Respond("No grid found...");

            return groups;
        }

        private string CreateExternalIP() {

            if (MySandboxGame.ConfigDedicated.IP.Contains("0.0") || MySandboxGame.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
                return Plugin.Config.LocalIP;

            return MySandboxGame.ConfigDedicated.IP;
        }

        private bool DownloadGrid(string currentIp, out string targetFile, out string filename, out Vector3D newPos) {

            Directory.CreateDirectory("ExportedGrids");
            using (WebClient client = new WebClient()) {

                string POS = "";
                string POSsource = "";

                NameValueCollection postData = new NameValueCollection()
                {
                    //order: {"parameter name", "parameter value"}
                    {"steamID", Context.Player.SteamUserId + ""},
                    {"currentIP", currentIp },
                    {"posCheck", "1" }
                };

                POSsource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/recovery.php", postData));
                POS = Context.Player.GetPosition().ToString();

                var config = Plugin.Config;

                if (config.LockedTransfer && config.EnabledMirror) 
                    POS = "{X:" +config.XCord + " Y:" + config.YCord + " Z:" + config.ZCord + "}";
                else if (config.EnabledMirror && !config.LockedTransfer) 
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));
                
                Vector3D.TryParse(POS, out Vector3D gps);
                newPos = gps;
                string source = "";
                postData = new NameValueCollection()
                {
                    //order: {"parameter name", "parameter value"}
                    {"steamID", Context.Player.SteamUserId + ""},
                    {"currentIP", currentIp },
                };

                source = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/recovery.php", postData));

                string existance = source.Substring(0, source.IndexOf(":"));
                if (existance == "1") {

                    filename = source.Split(':').Last() + ".xml";

                    try {

                        string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                        targetFile = "ExportedGrids\\" + filename;

                        WebClient myWebClient = new WebClient();
                        myWebClient.DownloadFile(remoteUri, targetFile);

                        return true;

                    } catch (Exception error) {
                        Log.Fatal("Unable to download grid: " + error.ToString());
                    }

                } else {
                    Context.Respond("You have no grids in active transport!");
                    filename = null;
                }

                targetFile = null;
                return false;
            }
        }

        [Command("grid", "Transfers the target grid to the target server")]
        [Permission(MyPromoteLevel.None)]
        public async Task GridAsync(string gridTarget, string serverTarget) {

            if (Context.Player == null) {
                Context.Respond("Console cannot run this command");
                return;
            }

            if (!Plugin.Config.Enabled)
                return;

            int i = 0;
            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";

            if (!Plugin.Config.EnabledTransfers) {
                Context.Respond("Grid Transfers are not enabled!");
                return;
            }

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
            string slotinfo = await Plugin.CheckSlotsAsync(target);
            existanceCheck = slotinfo.Split(';').Last();

            bool paired = await Plugin.CheckKeyAsync(target);

            if (target.Length < 1) {
                Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                return;
            }

            if (existanceCheck == "1") {

                if (!Plugin.CheckStatus(target)) {
                    Context.Respond("Target server is offline, preventing switch");
                    return;
                }

                bool InboundCheck = await Plugin.CheckInboundAsync(target);

                if (!InboundCheck) {
                    Context.Respond("The target server does not allow inbound transfers");
                    return;
                }

                if (paired == true) {

                    Log.Warn("Checking " + target);

                    int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                    string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);

                    Log.Warn("MAX: " + max);

                    int maxi = int.Parse(max);
                    int maxcheck = 1 + currentRemotePlayers;

                    Context.Respond("Slot Checking...");

                    Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                    if (maxcheck <= maxi) {

                        var p = Context.Player;
                        var parent = p.Character?.Parent;
                        if (parent == null) {
                        }

                        if (parent is MyShipController c) {
                            c.RemoveUsers(false);
                        }

                        if (ip == null || name == null || port == null) {
                            Context.Respond("Invalid Configuration!");
                        }

                        Context.Respond("Slot checking passed!");

                        try {

                            string externalIP = CreateExternalIP();

                            string pagesource = "";
                            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
                            using (WebClient client = new WebClient()) {

                                NameValueCollection postData = new NameValueCollection()
                                {
                                    //order: {"parameter name", "parameter value"}
                                    {"steamID", Context.Player.SteamUserId + ""},
                                    {"currentIP", currentIp },
                                    {"gridCheck", ""}
                                };
                                pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridHandle.php", postData));
                            }

                            if (pagesource == "0") {

                                SendGrid(gridTarget, serverTarget, Context.Player.IdentityId, target);

                                Log.Warn("Connected clients to " + serverTarget + " @ " + ip);

                            } else {

                                Log.Fatal(pagesource);
                                Context.Respond("Cannot transfer! You have a transfer ready to be recieved!");
                            }

                        } catch (Exception e) {
                            Log.Fatal(e, e.Message);
                            Context.Respond("Failure");
                        }

                    } else {
                        Context.Respond("Cannot switch, not enough slots available");
                    }

                } else {
                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                }

            } else {
                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
            }
        }

        public void SendGrid(string gridTarget, string serverTarget, long playerId, string ip, bool debug = false) {

            string externalIP = CreateExternalIP();
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            try {

                Log.Warn("Getting Group");

                MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup = FindRelevantGroup(gridTarget, playerId);

                string pos = "";

                foreach (var node in relevantGroup.Nodes) {
                    MyCubeGrid grid = node.NodeData;
                    pos = grid.PositionComp.GetPosition().ToString();
                }

                if (relevantGroup == null) {
                    Context.Respond("Cannot transfer somone elses grid!");
                    return;
                }

                Directory.CreateDirectory("ExportedGrids");

                var path = string.Format(ExportPath, Context.Player.SteamUserId + "-" + gridTarget);
                if (File.Exists(path)) {
                    Context.Respond("Export file already exists.");
                    return;
                }

                Log.Warn("Exproted");

                new GridImporter(Plugin, Context).SerializeGridsToPath(relevantGroup, gridTarget, path);

                if (!debug && UploadGrid(serverTarget, gridTarget, ip, currentIp, path, pos)) {

                    Log.Warn("Uploaded");

                    /* Upload successful close the grids */
                    DeleteUploadedGrids(relevantGroup);

                    /* Also delete local file */
                    File.Delete(path);
                }

            } catch (Exception e) {
                Log.Fatal("Target:" + gridTarget + "Server: " + serverTarget + "id: " + playerId);
                Log.Fatal("ERROR AT SENDGRID: " + e.ToString());
            }
        }

        private bool UploadGrid(string serverTarget, string gridTarget, string ip, string currentIp, string path, string pos) {

            /* DO we need a using here too? */
            WebClient Client = new WebClient();
            Client.Headers.Add("Content-Type", "binary/octet-stream");

            try {

                byte[] result = Client.UploadFile("http://switchplugin.net/gridHandle.php", "POST", path);
                Log.Fatal("Grid was uploaded to webserver!");

                string s = System.Text.Encoding.UTF8.GetString(result, 0, result.Length);

                if (s == "1") {

                    Context.Respond("Connecting clients to " + serverTarget + " @ " + ip);
                    Context.Respond("Grid has been sent to the void! - Good luck!");

                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), Context.Player.SteamUserId);

                    using (WebClient client = new WebClient()) {

                        string pagesource = "";
                        NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"steamID", Context.Player.SteamUserId.ToString()},
                            {"gridName", gridTarget },
                            {"targetIP", ip },
                            {"currentIP", currentIp },
                            {"fileName", Context.Player.SteamUserId + "-" + gridTarget },
                            {"bindKey", Plugin.Config.LocalKey },
                            {"targetPOS", pos },
                            {"key", Plugin.Config.ActivationKey }
                        };

                        pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridHandle.php", postData));
                    }

                    Plugin.Delete(Context.Player.DisplayName);

                    return true;

                } else {
                    Context.Respond("Unable to switch grid!");
                }

            } catch (Exception e) {
                Log.Fatal("Cannot upload grid: " + e.Message);
            }

            return false;
        }

        private void DeleteUploadedGrids(MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup) {

            foreach (var node in relevantGroup.Nodes) {

                MyCubeGrid grid = node.NodeData;

                /* We wanna Skip Projections... always */
                if (grid.Physics == null)
                    continue;

                grid.Close();
            }
        }

        public ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(string gridName) {

            try
            {
                bool foundIdentifer = false;
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();
                Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group => {

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes) {

                        IMyCubeGrid grid = groupNodes.NodeData;

                        if (grid.Physics == null)
                            continue;

                        /* Gridname is wrong ignore */
                        if (!grid.CustomName.Equals(gridName))
                            continue;
                        groups.Add(group);
                        foundIdentifer = true;
                    }
                });
                if (!foundIdentifer)
                {
                    Context.Respond("No grid found");
                }

                return groups;

            } catch (Exception e) {
                Log.Fatal("Error at Mechanical GridFinder: " + e.ToString());
                return null;
            }
        }

        private MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group FindRelevantGroup(string gridTarget, long playerId) {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = FindGridGroup(gridTarget);

            Log.Warn("Target and ID:   " + gridTarget + " | " + playerId);

            try {

                /* Each Physical Grid group (physical included Connectors) */
                foreach (var group in groups) {

                    bool groupFound = false;

                    /* Check each grid */
                    foreach (var node in group.Nodes) {

                        MyCubeGrid grid = node.NodeData;

                        /* We wanna Skip Projections... always */
                        if (grid.Physics == null)
                            continue;

                        /*
                         * Gridname is wrong ignore. I have not yet found out how to relibably get the most 
                         * dominant grid on the group (usually its the base grid, or the biggest one?)
                         * 
                         * Since you passed a gridname I just ignore all other grids of the group. 
                         * This however means a player could just use the name of a piston he happens 
                         * to own. So this one should be optimized. 
                         * 
                         * You need to debug that. Keen is a Mess. IMyCubeGrid has a CustomName that should 
                         * show DisplayName. But MyCubeGrid has not. So I use DisplayName instead. 
                         * 
                         * If this does not work you can cast to IMyCubeGrid also and use CustomName as 
                         * GridFinder does
                         */
                        if (!grid.DisplayName.Equals(gridTarget))
                            continue;

                        /* Big Owners are guys that have 50% or more of the grid. */
                        List<long> bigOnwerIds = grid.BigOwners;

                        /* Nobody can have the Majority of Blocks so there can be serveral owners. */
                        int ownerCount = bigOnwerIds.Count;
                        var gridOwner = 0L;

                        string pos = grid.PositionComp.GetPosition().ToString();

                        /* If nobody isnt the big owner then everythings fine. otherwise take the second biggest owner */
                        if (ownerCount > 0 && bigOnwerIds[0] != 0)
                            gridOwner = bigOnwerIds[0];
                        else if (ownerCount > 1)
                            gridOwner = bigOnwerIds[1];

                        /* Is the player ID the biggest owner? */
                        if (gridOwner == playerId) {

                            Log.Fatal("checking was completed");
                            groupFound = true;
                            break;

                        } else if (gridOwner == 0) {

                            groupFound = true;

                        } else {

                            Context.Respond("You are not the grid Owner");
                            Log.Warn("Conditionals... GridOwner: " + gridOwner + "Player: " + playerId);
                        }
                    }

                    if (groupFound)
                        return group;
                }

                return null;

            } catch (Exception e) {
                Log.Fatal("Error at groupfinder: " + e.ToString());
                return null;
            }
        }

        [Command("restore", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Restore() {
            Recover();
        }
    }
}

