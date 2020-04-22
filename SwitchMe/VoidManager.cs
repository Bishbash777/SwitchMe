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
                string POSsource = "";
                string filename;
                string targetFile;
                using (HttpClient clients = new HttpClient())
                {
                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("steamID", steamid.ToString()),
                        new KeyValuePair<string, string>("posCheck", "1"),
                        new KeyValuePair<string, string>("currentIP", currentIp)
                    };
                    FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                    HttpResponseMessage httpResponseMessage = await clients.PostAsync("http://switchplugin.net/recovery.php", content);
                    HttpResponseMessage response = httpResponseMessage;
                    httpResponseMessage = null;
                    string text = await response.Content.ReadAsStringAsync();
                    POSsource = text;
                }


                var config = Plugin.Config;

                if (config.LockedTransfer)
                    POS = "{X:" + config.XCord + " Y:" + config.YCord + " Z:" + config.ZCord + "}";
                else if (config.EnabledMirror)
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));

                POS = POS.TrimStart('{').TrimEnd('}');
                Vector3D.TryParse(POS, out Vector3D gps);
                newPos = gps;
                string source = "";

                using (HttpClient clients = new HttpClient())
                {
                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("steamID", steamid.ToString()),
                        new KeyValuePair<string, string>("currentIP", currentIp)
                    };
                    FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                    HttpResponseMessage httpResponseMessage = await clients.PostAsync("http://switchplugin.net/recovery.php", content);
                    HttpResponseMessage response = httpResponseMessage;
                    httpResponseMessage = null;
                    string text = await response.Content.ReadAsStringAsync();
                    source = text;
                }
       

                string existance = source.Substring(0, source.IndexOf(":"));
                if (existance == "1") {
                    Log.Info("Grid found in database... attempting download!");
                    filename = source.Split(':').Last() + ".xml";

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

        private async Task<bool> UploadGridAsync(string serverTarget, string gridTarget, string playername, string ip, string currentIp, string path, string pos, string targetAlias) {
            var player = utils.GetPlayerByNameOrId(playername);
            /* DO we need a using here too? */
            WebClient Client = new WebClient();
            Client.Headers.Add("Content-Type", "binary/octet-stream");

            try {

                byte[] result = Client.UploadFile("http://switchplugin.net/gridHandle.php", "POST", path);
                Log.Fatal("Grid was uploaded to webserver!");

                string s = System.Text.Encoding.UTF8.GetString(result, 0, result.Length);

                if (s == "1") {

                    utils.NotifyMessage("Grid has been sent to the void! - Good luck!", player.SteamUserId);

                    Log.Info($"Connecting clients to {serverTarget} @ {ip}");

                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), player.SteamUserId);

                    using (HttpClient clients = new HttpClient())
                    {
                        List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("steamID", player.SteamUserId.ToString()),
                            new KeyValuePair<string, string>("targetIP", ip ),
                            new KeyValuePair<string, string>("fileName", player.SteamUserId.ToString() + "-" + gridTarget ),
                            new KeyValuePair<string, string>("bindKey", Plugin.Config.LocalKey ),
                            new KeyValuePair<string, string>("targetPOS", pos ),
                            new KeyValuePair<string, string>("gridName", gridTarget ),
                            new KeyValuePair<string, string>("currentIP", currentIp)
                        };
                        FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                        HttpResponseMessage httpResponseMessage = await clients.PostAsync("http://switchplugin.net/gridHandle.php", content);
                        HttpResponseMessage response = httpResponseMessage;
                        httpResponseMessage = null;
                        string text = await response.Content.ReadAsStringAsync();
                    }

                    using (HttpClient client = new HttpClient()) {
                        List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("BindKey", Plugin.Config.LocalKey),
                            new KeyValuePair<string, string>("CurrentIP", currentIp),
                            new KeyValuePair<string, string>("TargetIP", ip),
                            new KeyValuePair<string, string>("TargetAlias", targetAlias),
                            new KeyValuePair<string, string>("AddConnection",player.SteamUserId.ToString())
                        };
                        FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                        HttpResponseMessage httpResponseMessage = await client.PostAsync("http://switchplugin.net/api/index.php", content);
                        HttpResponseMessage response = httpResponseMessage;
                        httpResponseMessage = null;
                        string text = await response.Content.ReadAsStringAsync();
                        Log.Warn(text);
                    }
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
            string existanceCheck = "";

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

                if (ip == null || name == null || port == null) {
                    utils.Respond("Invalid Configuration!", "Server" , steamid);
                    return;
                }

                if (target.Length < 1) {
                    utils.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!", "Server", steamid);
                    return;
                }

                if (existanceCheck != "1") {
                    utils.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!", "Server", steamid);
                    return;
                }

                if (paired != true) {
                    utils.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!", "Server", steamid);
                    return;
                }

                ///   Slot checking
                Log.Warn("Checking " + target);
                int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                Log.Warn("MAX: " + max);
                int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                if (type == "single") {
                    currentLocalPlayers = 1;
                }
                int maxi = int.Parse(max);
                int maxcheck = currentLocalPlayers + currentRemotePlayers;
                utils.Respond("Slot Checking...", "Server", steamid);
                Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);
                if (maxcheck > maxi && Context.Player.PromoteLevel != MyPromoteLevel.Admin) {
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
                string slotinfo = await Plugin.CheckSlotsAsync(target);
                existanceCheck = slotinfo.Split(';').Last();
                bool paired = await Plugin.CheckKeyAsync(target);

                if (ip == null || name == null || port == null) {
                    Context.Respond("Invalid Configuration!");
                    return;
                }

                if (target.Length < 1) {
                    Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    return;
                }

                if (existanceCheck != "1") {
                    Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    return;
                }

                if (paired != true) {
                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                    return;
                }

                ///     Slot checking
                Log.Warn("Checking " + target);
                int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                Log.Warn("MAX: " + max);
                int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                int maxi = int.Parse(max);
                int maxcheck = currentLocalPlayers + currentRemotePlayers;
                Context.Respond("Slot Checking...");
                Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);
                if (maxcheck > maxi) {
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
