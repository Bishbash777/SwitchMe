using NLog;
using Sandbox.Game.Entities;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Torch.Commands;
using Sandbox.Common;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Groups;
using VRageMath;
using System.Threading.Tasks;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using Sandbox.Common.ObjectBuilders;

namespace SwitchMe {

    public class VoidManager {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly string ExportPath = "SwitchTemp\\{0}.xml";
        public ConfigObjects ConfigObjects = new ConfigObjects();

        private readonly SwitchMePlugin Plugin;
        private readonly CommandContext Context;

        public VoidManager(SwitchMePlugin Plugin) {
            this.Plugin = Plugin;
        }

        public async Task<bool> SendGrid(string gridTarget, string serverTarget, string playername, long playerId, string ip, string targetAlias, bool debug = false) {
            var player = utils.GetPlayerByNameOrId(playername);
            string externalIP = utils.CreateExternalIP(Plugin.Config);
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            try {

                Log.Warn("Getting Group");

                MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup = 
                    utils.FindRelevantGroup(gridTarget, playerId);

                string pos = "";

                foreach (var node in relevantGroup.Nodes) {
                    MyCubeGrid grid = node.NodeData;
                    pos = grid.PositionComp.GetPosition().ToString();
                }

                if (relevantGroup == null) {
                    utils.NotifyMessage("Cannot transfer somone elses grid!", player.SteamUserId);
                    return false;
                }

                Directory.CreateDirectory("SwitchTemp");
                var path = string.Format(ExportPath, player.SteamUserId + "-" + gridTarget);

                if (!new GridImporter(Plugin, Context).SerializeGridsToPath(relevantGroup, gridTarget, path, player.DisplayName)) {
                    return false;
                }

                if (await UploadGridAsync(serverTarget, gridTarget, player.DisplayName, ip, path, pos, targetAlias)) {
                    /* Upload successful close the grids */
                    DeleteUploadedGrids(relevantGroup);

                    /* Also delete local file */
                    File.Delete(path);
                    return true;
                }

            } catch (Exception e) {
                Log.Fatal(e, " Target: " + gridTarget + " Server: " + serverTarget + " id: " + playerId);
                return false;
            }
            return false;
        }

        public async Task<Tuple<string, string, GateObject>> DownloadGridAsync(string currentIp, ulong steamid, string POS) {
            APIMethods API = new APIMethods(Plugin);

            Directory.CreateDirectory("SwitchTemp");
            using (WebClient client = new WebClient()) {

                GateObject gate = new GateObject();
                Vector3D gps = Vector3D.Zero;
                string filename;
                string targetFile;

                string gatename = await API.GetGateAsync(steamid.ToString());
                //
                // DO THE RANDOMISER SHIT BISH
                //
                bool foundGate = false;

                foreach (GateObject gateOb in Plugin.zones.Where(zone => zone.gateName.Equals($"SwitchGate-{gatename}"))) {
                    gps = gateOb.position;
                    gate = gateOb;
                    foundGate = true;
                }
                if (Plugin.Config.RandomisedExit) {
                    //Dictionary<string, string> gateSelection = new Dictionary<string, string>();
                    //channelIds = Plugin.Config.Gates;
                    //int i = 0;
                    //foreach (string gate in channelIds) {
                    //    i++;
                        //gateSelection.Add(gate.Split('/')[2], gate.Split('/')[1]);
                    //}
                    //if (i != 0) {
                    //    POS = utils.SelectRandomGate(gateSelection);
                    //}
                }
                if (!Plugin.Config.RandomisedExit) {
                    Log.Warn($"API: Gate elected = {gatename}");
                }
                else {
                    Log.Warn("Using randomly selected gate as exit");
                }

                /*
                else if (config.EnabledMirror)
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));
                */

                Log.Info("Selected GPS: " + gps.ToString());


                var api_response = await API.FindWebGridAsync(steamid);
                if (api_response["responseCode"] == "0") {
                    Log.Info("Grid found in database... attempting download!");
                    filename = api_response["filename"] + ".xml";

                    try {

                        string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                        targetFile = "SwitchTemp\\" + filename;

                        WebClient myWebClient = new WebClient();
                        myWebClient.DownloadFile(remoteUri, targetFile);
                        return new Tuple<string, string, GateObject>(targetFile, filename, gate);

                    } catch (Exception error) {
                        Log.Fatal("Unable to download grid: " + error.ToString());
                    }

                } else {
                    utils.NotifyMessage("You have no grids in active transport!", steamid);
                    targetFile = null;
                }
                return null;
                
            }
        }

        private bool UploadGridFile(string path) {
            WebClient Client = new WebClient();
            Client.Headers.Add("Content-Type", "binary/octet-stream");
            byte[] result = Client.UploadFile("http://switchplugin.net/api2/grid-upload.php", "POST", path);
            Log.Fatal("Grid was uploaded to webserver!");
            var api_response = utils.ParseQueryString(Encoding.UTF8.GetString(result, 0, result.Length));
            if (api_response["responseCode"] != "0") {
                if (Plugin.debug) { Log.Warn($"{api_response["responseMessage"]}"); }
                return false;
            }
            return true;
        }

        private async Task<bool> UploadGridAsync(string serverTarget, string gridTarget, string playername, string ip, string path, string pos, string targetAlias) {
            var player = utils.GetPlayerByNameOrId(playername);
            APIMethods API = new APIMethods(Plugin);

            try {
                if (UploadGridFile(path)) {

                    utils.NotifyMessage("Grid has been sent to the void! - Good luck!", player.SteamUserId);

                    Log.Info($"Connecting clients to {serverTarget} @ {ip}");

                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), player.SteamUserId);

                    //Add entry into transfers table
                    await API.AddTransferAsync(player.SteamUserId.ToString(),ip, player.SteamUserId.ToString() + "-" + gridTarget, pos, gridTarget);
                    //Add user to transfer Queue (Active users)
                    await API.AddConnectionAsync(player.SteamUserId, ip, targetAlias);
                    return true;

                } else {
                    utils.NotifyMessage("Unable to switch grid!", player.SteamUserId);
                    return false;
                }

            } catch (Exception e) {
                Log.Fatal(e.ToString());
                return false;
            }
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

        public async Task PlayerTransfer(string type, ulong steamid) {
            APIMethods API = new APIMethods(Plugin);
            string ip = "";
            string name = "";
            string port = "";
            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
            if (type == "single") {
                currentLocalPlayers = 1;
            }

            int i = 0;

            foreach (ConfigObjects.Server server in Plugin.Config.Servers) {
                name = server.ServerName;
                ip = server.ServerIP;
                port = server.ServerPort.ToString();
                i++;
            }

            if (i == 1) {

                string target = ip + ":" + port;
                ip += ":" + port;
                bool slotsAvailable = bool.Parse(await API.CheckSlotsAsync(target, currentLocalPlayers.ToString()));
                bool paired = await API.CheckKeyAsync(target);

                if (ip == null || name == null || port == null) {
                    utils.Respond("Invalid Configuration!", "Server" , steamid);
                    return;
                }

                if (target.Length < 1) {
                    utils.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!", "Server", steamid);
                    return;
                }

                if (!await API.CheckExistance(target)) {
                    utils.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!", "Server", steamid);
                    return;
                }

                if (paired != true) {
                    utils.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!", "Server", steamid);
                    return;
                }

                ///   Slot checking
                utils.Respond("Slot Checking...", "Server", steamid);
                if (!slotsAvailable && Context.Player.PromoteLevel != MyPromoteLevel.Admin) {
                    return;
                }


                /// Connection phase
                try {
                    utils.Respond("Connecting client(s) to " + Context.RawArgs + " @ " + ip, "Server", steamid);
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                    if (type == "single") {
                        ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                        return;
                    }
                    if (type == "all") {
                        ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                        return;
                    }
                }
                catch {
                    Context.Respond("Failure");
                }
                /// Move onto Specific connection
            }

            else {


                foreach (ConfigObjects.Server server in Plugin.Config.Servers.Where(server => server.ServerName.Equals(Context.RawArgs))) {
                    name = server.ServerName;
                    ip = server.ServerIP;
                    port = server.ServerPort.ToString();
                }

                string target = ip + ":" + port;
                ip += ":" + port;
                bool slotsAvailable = bool.Parse(await API.CheckSlotsAsync(target, currentLocalPlayers.ToString()));
                bool paired = await API.CheckKeyAsync(target);

                if (ip == null || name == null || port == null) {
                    Context.Respond("Invalid Configuration!");
                    return;
                }

                if (target.Length < 1) {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    return;
                }

                if (!await API.CheckExistance(target)) {
                    Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    return;
                }

                if (paired != true) {
                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                    return;
                }

                ///     Slot checking
                if (slotsAvailable) {
                    Context.Respond("Cannot switch, not enough slots available");
                    return;
                }
                Context.Respond("Slot checking passed!");

                ///     Connection phase
                try {
                    if (type == "single") {
                        ModCommunication.SendMessageTo(new JoinServerMessage(ip), Context.Player.SteamUserId);
                        return;
                    }
                    ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                    return;
                }
                catch {
                    Context.Respond("Failure");
                }
            }
        }
    }
}
