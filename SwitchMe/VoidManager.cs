using NLog;
using Sandbox.Game.Entities;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Groups;
using VRageMath;

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

        public bool DownloadGrid(string currentIp, out string targetFile, out string filename, out Vector3D newPos) {

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
                    POS = "{X:" + config.XCord + " Y:" + config.YCord + " Z:" + config.ZCord + "}";
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
    }
}
