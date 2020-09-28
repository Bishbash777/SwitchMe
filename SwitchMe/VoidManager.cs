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
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Groups;
using VRageMath;
using System.Threading.Tasks;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace SwitchMe {

    public class VoidManager {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly string ExportPath = "SwitchTemp\\{0}.xml";

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

                if (await UploadGridAsync(serverTarget, gridTarget, player.DisplayName, ip, currentIp, path, pos, targetAlias)) {
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

        public async Task<Tuple<string, string, Vector3D>> DownloadGridAsync(string currentIp, ulong steamid, string POS) {

            Directory.CreateDirectory("SwitchTemp");
            using (WebClient client = new WebClient()) {

                Vector3D newPos;
                string filename;
                string targetFile;

                string gatename = await Plugin.GetGateAsync(steamid.ToString());
                //
                // DO THE RANDOMISER SHIT BISH
                //
                bool foundGate = false;
                IEnumerable<string> channelIds = Plugin.Config.Gates.Where(c => c.Split('/')[2].Equals(gatename));
                foreach (string chId in channelIds) {
                    POS = chId.Split('/')[1];
                    foundGate = true;
                }
                if (Plugin.Config.RandomisedExit) {
                    Dictionary<string, string> gateSelection = new Dictionary<string, string>();
                    channelIds = Plugin.Config.Gates;
                    int i = 0;
                    foreach (string gate in channelIds) {
                        i++;
                        gateSelection.Add(gate.Split('/')[2], gate.Split('/')[1]);
                    }
                    if (i != 0) {
                        POS = utils.SelectRandomGate(gateSelection);
                    }
                }
                if (!Plugin.Config.RandomisedExit) {
                    Log.Warn($"API: Gate elected = {gatename}");
                }
                else {
                    Log.Warn("Using randomly selected gate as exit");
                }

                if (!foundGate) {
                    POS = "{X:" + Plugin.Config.XCord + " Y:" + Plugin.Config.YCord + " Z:" + Plugin.Config.ZCord + "}";
                    Log.Error($"Target gate ({gatename}) does not exist... Using default");
                }
                /*
                else if (config.EnabledMirror)
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));
                */
                POS = POS.TrimStart('{').TrimEnd('}');
                Vector3D.TryParse(POS, out Vector3D gps);
                newPos = gps;
                Log.Info("Selected GPS: " + gps.ToString());
                

                utils.webdata.Add("STEAMID", steamid.ToString());
                utils.webdata.Add("CURRENTIP", Plugin.currentIP());
                utils.webdata.Add("FUNCTION", "FindWebGridAsync");
                var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
                if (Plugin.debug) {
                    foreach(var kvp in api_response) {
                        Log.Warn($"{kvp.Key}=>{kvp.Value}");
                    }
                }
                if (api_response["responseCode"] == "0") {
                    Log.Info("Grid found in database... attempting download!");
                    filename = api_response["filename"] + ".xml";

                    try {

                        string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                        targetFile = "SwitchTemp\\" + filename;

                        WebClient myWebClient = new WebClient();
                        myWebClient.DownloadFile(remoteUri, targetFile);
                        return new Tuple<string, string, Vector3D>(targetFile, filename, newPos);

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

        public async Task AddTransferAsync(string steamid, string ip, string filename,string targetpos, string grid_target) {
            utils.webdata.Add("STEAMID", steamid);
            utils.webdata.Add("TARGETIP", ip);
            utils.webdata.Add("FILENAME", steamid + "-" + grid_target);
            utils.webdata.Add("BINDKEY", Plugin.Config.LocalKey);
            utils.webdata.Add("TARGETPOS", targetpos);
            utils.webdata.Add("GRIDNAME", grid_target);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("FUNCTION", "AddTransferAsync");
            await utils.SendAPIRequestAsync(Plugin.debug);
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

        private async Task<bool> UploadGridAsync(string serverTarget, string gridTarget, string playername, string ip, string currentIp, string path, string pos, string targetAlias) {
            var player = utils.GetPlayerByNameOrId(playername);


            try {
                if (UploadGridFile(path)) {

                    utils.NotifyMessage("Grid has been sent to the void! - Good luck!", player.SteamUserId);

                    Log.Info($"Connecting clients to {serverTarget} @ {ip}");

                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), player.SteamUserId);

                    //Add entry into transfers table
                    await AddTransferAsync(player.SteamUserId.ToString(),ip, player.SteamUserId.ToString() + "-" + gridTarget, pos, gridTarget);
                    //Add user to transfer Queue (Active users)
                    utils.webdata.Add("BINDKEY", Plugin.Config.LocalKey);
                    utils.webdata.Add("CURRENTIP",Plugin.currentIP());
                    utils.webdata.Add("TARGETIP", ip);
                    utils.webdata.Add("TARGETALIAS", targetAlias);
                    utils.webdata.Add("STEAMID", player.SteamUserId.ToString());
                    utils.webdata.Add("FUNCION","AddConnectionAsync");
                    await utils.SendAPIRequestAsync(Plugin.debug);
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
            string ip = "";
            string name = "";
            string port = "";
            int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
            if (type == "single") {
                currentLocalPlayers = 1;
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
                bool slotsAvailable = bool.Parse(await Plugin.CheckSlotsAsync(target, currentLocalPlayers.ToString()));
                bool paired = await Plugin.CheckKeyAsync(target);

                if (ip == null || name == null || port == null) {
                    utils.Respond("Invalid Configuration!", "Server" , steamid);
                    return;
                }

                if (target.Length < 1) {
                    utils.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!", "Server", steamid);
                    return;
                }

                if (!bool.Parse(await Plugin.CheckExistance(target))) {
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

                channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));

                foreach (string chId in channelIds) {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];
                }

                string target = ip + ":" + port;
                ip += ":" + port;
                bool slotsAvailable = bool.Parse(await Plugin.CheckSlotsAsync(target, currentLocalPlayers.ToString()));
                bool paired = await Plugin.CheckKeyAsync(target);

                if (ip == null || name == null || port == null) {
                    Context.Respond("Invalid Configuration!");
                    return;
                }

                if (target.Length < 1) {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    return;
                }

                if (!bool.Parse(await Plugin.CheckExistance(target))) {
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
