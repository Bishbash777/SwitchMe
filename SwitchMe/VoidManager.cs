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

namespace SwitchMe {

    public class VoidManager {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly string ExportPath = "ExportedGrids\\{0}.xml";

        private readonly SwitchMePlugin Plugin;
        private readonly CommandContext Context;

        public VoidManager(SwitchMePlugin Plugin, CommandContext Context) {
            this.Plugin = Plugin;
            this.Context = Context;
        }

        public void SendGrid(string gridTarget, string serverTarget, long playerId, string ip, bool debug = false) {

            string externalIP = Utilities.CreateExternalIP(Plugin.Config);
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            try {

                Log.Warn("Getting Group");

                MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup = 
                    Utilities.FindRelevantGroup(gridTarget, playerId, Context);

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
                Log.Fatal(e, "Target:" + gridTarget + "Server: " + serverTarget + "id: " + playerId);
            }
        }

        public async Task<Tuple<string, string, Vector3D>> DownloadGridAsync(string currentIp) {

            Directory.CreateDirectory("ExportedGrids");
            using (WebClient client = new WebClient()) {

                Vector3D newPos;
                string POS = "";
                string POSsource = "";
                string filename;
                string targetFile;
                using (HttpClient clients = new HttpClient())
                {
                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("steamID", Context.Player.SteamUserId.ToString()),
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


                POS = Context.Player.GetPosition().ToString();

                var config = Plugin.Config;

                if (config.LockedTransfer && config.EnabledMirror)
                    POS = "{X:" + config.XCord + " Y:" + config.YCord + " Z:" + config.ZCord + "}";
                else if (config.EnabledMirror && !config.LockedTransfer)
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));

                Vector3D.TryParse(POS, out Vector3D gps);
                newPos = gps;
                string source = "";

                using (HttpClient clients = new HttpClient())
                {
                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("steamID", Context.Player.SteamUserId.ToString()),
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

                    filename = source.Split(':').Last() + ".xml";

                    try {

                        string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                        targetFile = "ExportedGrids\\" + filename;

                        WebClient myWebClient = new WebClient();
                        myWebClient.DownloadFile(remoteUri, targetFile);
                        return new Tuple<string, string, Vector3D>(targetFile, filename, newPos);

                    } catch (Exception error) {
                        Log.Fatal("Unable to download grid: " + error.ToString());
                    }

                } else {
                    Context.Respond("You have no grids in active transport!");
                    targetFile = null;
                }
                return null;
                
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

        public async Task PlayerTransfer(string type) {
            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";

            if (type == "single" && Context.Player == null) {
                Context.Respond("Console cannot run this command");
                return;
            }

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
                Context.Respond("Slot Checking...");
                Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);
                if (maxcheck > maxi) {
                    Context.Respond("Cannot switch, not enough slots available");
                    return;
                }
                Context.Respond("Slot checking passed!");


                /// Connection phase
                try {
                    Context.Respond("Connecting client(s) to " + Context.RawArgs + " @ " + ip);
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                    if (type == "single") {
                        ModCommunication.SendMessageTo(new JoinServerMessage(ip), Context.Player.SteamUserId);
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
