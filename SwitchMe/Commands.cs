using NLog;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Groups;
using System.Collections.Concurrent;
using System.Net;
using System.Collections.Specialized;

namespace SwitchMe
{
    [Category("switch")]
    public class Commands : CommandModule
    {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public SwitchMePlugin Plugin => (SwitchMePlugin)Context.Plugin;
        [Command("me", "Automatically connect to your server of choice within this network. USAGE: !switch me <Insert Server name here>")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchLocal()
        {

            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";
            int i = 0;
            if (Plugin.Config.Enabled)
            {
                if (Context.Player != null)
                {

                    IEnumerable<string> channelIds = Plugin.Config.Servers;
                    foreach (string chId in channelIds)
                    {
                        ip = chId.Split(':')[1];
                        name = chId.Split(':')[0];
                        port = chId.Split(':')[2];
                        i++;

                    }
                    if (i == 1)
                    {
                        string target = ip + ":" + port;
                        ip += ":" + port;
                        string slotinfo = Plugin.CheckSlots(target);
                        existanceCheck = slotinfo.Split(';').Last();
                        bool paired = Plugin.CheckKey(target);
                        if (target.Length > 1)
                        {
                            if (existanceCheck == "1")
                            {
                                if (paired == true)
                                {
                                    Log.Warn("Checking " + target);
                                    int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                                    string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                                    Log.Warn("MAX: " + max);
                                    int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                                    int maxi = int.Parse(max);
                                    int maxcheck = (1 + currentRemotePlayers);
                                    Context.Respond("Slot Checking...");
                                    Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);


                                    if (maxcheck <= maxi)
                                    {
                                        if (ip == null || name == null || port == null)
                                        {
                                            Context.Respond("Invalid Configuration!");
                                        }
                                        Context.Respond("Slot checking passed!");
                                        try
                                        {
                                            ulong steamid = Context.Player.SteamUserId;
                                            Context.Respond("Connecting client to " + name + " @ " + target);
                                            ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                                            Log.Warn("Connected client to " + name + " @ " + ip);


                                        }
                                        catch
                                        {
                                            Context.Respond("Failure");
                                        }
                                    }
                                    else
                                    {
                                        Context.Respond("Cannot switch, not enough slots available");
                                    }
                                }
                                else
                                {
                                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                                }
                            }
                            else
                            {
                                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                            }
                        }
                        else
                        {
                            Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                        }

                    }
                    else
                    {
                        channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));
                        foreach (string chId in channelIds)
                        {
                            ip = chId.Split(':')[1];
                            name = chId.Split(':')[0];
                            port = chId.Split(':')[2];

                        }
                        string target = ip + ":" + port;
                        ip += ":" + port;
                        string slotinfo = Plugin.CheckSlots(target);
                        existanceCheck = slotinfo.Split(';').Last();
                        bool paired = Plugin.CheckKey(target);

                        if (target.Length > 1)
                        {
                            if (existanceCheck == "1")
                            {
                                if (paired == true)
                                {
                                    Log.Warn("Checking " + target);
                                    int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                                    string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                                    Log.Warn("MAX: " + max);
                                    int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                                    int maxi = int.Parse(max);
                                    int maxcheck = (1 + currentRemotePlayers);
                                    Context.Respond("Slot Checking...");
                                    Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);


                                    if (maxcheck <= maxi)
                                    {
                                        if (ip == null || name == null || port == null)
                                        {
                                            Context.Respond("Invalid Configuration!");
                                        }
                                        Context.Respond("Slot checking passed!");
                                        try
                                        {
                                            ulong steamid = Context.Player.SteamUserId;
                                            Context.Respond("Connecting client to " + Context.RawArgs + " @ " + ip);
                                            ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                                            Log.Warn("Connected client to " + Context.RawArgs + " @ " + ip);


                                        }
                                        catch
                                        {
                                            Context.Respond("Failure");
                                        }
                                    }
                                    else
                                    {
                                        Context.Respond("Cannot switch, not enough slots available");
                                    }
                                }
                                else
                                {
                                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                                }
                            }
                            else
                            {
                                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                            }
                        }
                        else
                        {
                            Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                        }
                    }
                }
                else
                {
                    Context.Respond("Cannot run this command from outside the server!");
                }
            }
            else
            {
                Context.Respond("Switching is not enabled!");
            }

        }
        [Command("all", "Automatically connects all players to your server of choice within this network. USAGE: !switch all <Insert Server name here>")]
        [Permission(MyPromoteLevel.Admin)]
        public void SwitchAll()
        {
            int i = 0;
            string ip = "";
            string name = "";
            string port = "";
            string existanceCheck = "";
            if (Plugin.Config.Enabled)
            {

                IEnumerable<string> channelIds = Plugin.Config.Servers;
                foreach (string chId in channelIds)
                {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];
                    i++;

                }
                if (i == 1)
                {
                    string target = ip + ":" + port;
                    ip += ":" + port;
                    string slotinfo = Plugin.CheckSlots(target);
                    existanceCheck = slotinfo.Split(';').Last();
                    bool paired = Plugin.CheckKey(target);
                    if (target.Length > 1)
                    {
                        if (existanceCheck == "1")
                        {
                            if (paired == true)
                            {
                                Log.Warn("Checking " + target);
                                int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                                string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                                Log.Warn("MAX: " + max);
                                int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                                int maxi = int.Parse(max);
                                int maxcheck = currentLocalPlayers + currentRemotePlayers;
                                Context.Respond("Slot Checking...");
                                Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                                if (maxcheck <= maxi)
                                {
                                    if (ip == null || name == null || port == null)
                                    {
                                        Context.Respond("Invalid Configuration!");
                                    }
                                    Context.Respond("Slot checking passed!");
                                    try
                                    {
                                        Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                                        ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                                        Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                                    }
                                    catch
                                    {
                                        Context.Respond("Failure");
                                    }
                                }
                                else
                                {
                                    Context.Respond("Cannot switch, not enough slots available");
                                }
                            }
                            else
                            {
                                Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                            }
                        }
                        else
                        {
                            Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                        }
                    }
                    else
                    {
                        Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    }
                }
                else
                {

                    channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));
                    foreach (string chId in channelIds)
                    {
                        ip = chId.Split(':')[1];
                        name = chId.Split(':')[0];
                        port = chId.Split(':')[2];

                    }
                    string target = ip + ":" + port;
                    ip += ":" + port;
                    string slotinfo = Plugin.CheckSlots(target);
                    existanceCheck = slotinfo.Split(';').Last();
                    bool paired = Plugin.CheckKey(target);
                    if (target.Length > 1)
                    {
                        if (existanceCheck == "1")
                        {
                            if (paired == true)
                            {
                                Log.Warn("Checking " + target);
                                int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                                string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                                Log.Warn("MAX: " + max);
                                int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                                int maxi = int.Parse(max);
                                int maxcheck = currentLocalPlayers + currentRemotePlayers;
                                Context.Respond("Slot Checking...");
                                Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                                if (maxcheck <= maxi)
                                {
                                    if (ip == null || name == null || port == null)
                                    {
                                        Context.Respond("Invalid Configuration!");
                                    }
                                    Context.Respond("Slot checking passed!");
                                    try
                                    {
                                        Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                                        ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                                        Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                                    }
                                    catch
                                    {
                                        Context.Respond("Failure");
                                    }
                                }
                                else
                                {
                                    Context.Respond("Cannot switch, not enough slots available");
                                }
                            }
                            else
                            {
                                Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                            }
                        }
                        else
                        {
                            Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                        }
                    }
                    else
                    {
                        Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    }
                }
            }
            else
            {
                Context.Respond("Switching is not enabled!");
            }
        }
        [Command("list", "Displays a list of Valid Server names for the '!switch me <servername>' command. ")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchList()
        {
            if (Plugin.Config.Enabled)
            {
                StringBuilder sb = new StringBuilder();
                string name;

                IEnumerable<string> channelIds = Plugin.Config.Servers;
                foreach (string chId in channelIds)
                {
                    name = chId.Split(':')[0];
                    string ip = chId.Split(':')[1];
                    string port = chId.Split(':')[2];
                    string target = ip + ":" + port;
                    bool paired = Plugin.CheckKey(target);
                    if (paired == true)
                    {
                        sb.Append("'" + name + "' ");
                    }

                }
                Context.Respond("--------------------------");
                Context.Respond("List of Servers available to switch to:");
                Context.Respond(sb.ToString());
                Context.Respond("--------------------------");

            }
            else
            {
                Context.Respond("Switching is not enabled!");
            }
        }

        [Command("help", "Displays a list of Valid Server names for !switch me <servername> ")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchHelp()
        {
            Context.Respond("`!switch me <servername>` Switches you to selected server.");
            Context.Respond("`!switch list` Displays a list of valid Server names to connect to.");
            Context.Respond("`!switch grid '<targetgrid>' '<targetserver>'` Transfers the target grid to the target server.");
        }

        private readonly string ExportPath = "ExportedGrids\\{0}.xml";


        [Command("recover", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Recover()
        {
            if (Context.Player != null)
            {
                string externalIP;
                if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
                {
                    externalIP = Plugin.Config.LocalIP;
                }
                else
                {
                    externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
                }
                string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
                using (WebClient client = new WebClient())
                {
                    string pagesource = "";
                    NameValueCollection postData = new NameValueCollection()
                {
                    //order: {"parameter name", "parameter value"}
                    {"steamID", Context.Player.SteamUserId + ""},
                    {"currentIP", currentIp },
                };
                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridRecovery.php", postData));

                    string existance = pagesource.Substring(0, pagesource.IndexOf(":"));
                    if (existance == "1")
                    {
                        string filename = pagesource.Split(':').Last() + ".xml";
                        try
                        {
                            string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                            string targetFile = "ExportedGrids\\" + filename;

                            WebClient myWebClient = new WebClient();
                            myWebClient.DownloadFile(remoteUri, targetFile);

                            Context.Respond(targetFile);
                            if (MyObjectBuilderSerializer.DeserializeXML(targetFile, out MyObjectBuilder_CubeGrid grid))
                            {
                                Context.Respond($"Importing grid from {targetFile}");
                                MyEntities.RemapObjectBuilder(grid);
                                var pos = MyEntities.FindFreePlace(Context.Player.GetPosition(), grid.CalculateBoundingSphere().Radius);
                                if (pos == null)
                                {
                                    Context.Respond("No free place.");
                                    return;
                                }

                                var x = grid.PositionAndOrientation ?? new MyPositionAndOrientation();
                                x.Position = pos.Value;
                                grid.PositionAndOrientation = x;
                                MyEntities.CreateFromObjectBuilderParallel(grid, true);
                                Context.Respond("Grid has been pulled from the void!");
                                File.Delete(targetFile);
                                Plugin.DeleteFromWeb(filename);
                            }
                        }
                        catch
                        {
                            Log.Fatal("Unable to download grid");
                        }
                    }
                    else
                    {
                        Context.Respond("You have no grids in active transport!");
                    }
                }
            }
            else
            {
                Context.Respond("Command cannot be ran from console");
            }
        }


        [Command("grid", "Transfers the target grid to the target server")]
        [Permission(MyPromoteLevel.None)]
        public void Grid(string gridTarget, string serverTarget)
        {
            if (Context.Player == null)
            {
                Context.Respond("Console cannot run this command");
                return;
            }
            else
            {
                int i = 0;
                string ip = "";
                string name = "";
                string port = "";
                string existanceCheck = "";
                if (Plugin.Config.Enabled)
                {
                    if (Plugin.Config.EnabledTransfers)
                    {

                        IEnumerable<string> channelIds = Plugin.Config.Servers;
                        foreach (string chId in channelIds)
                        {
                            ip = chId.Split(':')[1];
                            name = chId.Split(':')[0];
                            port = chId.Split(':')[2];
                            i++;

                        }
                        channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(serverTarget));
                        foreach (string chId in channelIds)
                        {
                            ip = chId.Split(':')[1];
                            name = chId.Split(':')[0];
                            port = chId.Split(':')[2];

                        }
                        string target = ip + ":" + port;
                        ip += ":" + port;
                        string slotinfo = Plugin.CheckSlots(target);
                        existanceCheck = slotinfo.Split(';').Last();
                        bool paired = Plugin.CheckKey(target);
                        if (target.Length > 1)
                        {
                            if (existanceCheck == "1")
                            {
                                if (paired == true)
                                {
                                    Log.Warn("Checking " + target);
                                    int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                                    string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                                    Log.Warn("MAX: " + max);
                                    int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                                    int maxi = int.Parse(max);
                                    int maxcheck = currentLocalPlayers + currentRemotePlayers;
                                    Context.Respond("Slot Checking...");
                                    Log.Warn(maxcheck + " Player Count Prediction|Player Count Threshold " + max);

                                    if (maxcheck <= maxi)
                                    {
                                        if (ip == null || name == null || port == null)
                                        {
                                            Context.Respond("Invalid Configuration!");
                                        }
                                        Context.Respond("Slot checking passed!");
                                        try
                                        {
                                            string externalIP;
                                            if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
                                            {
                                                externalIP = Plugin.Config.LocalIP;
                                            }
                                            else
                                            {
                                                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
                                            }
                                            string pagesource = "";
                                            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
                                            using (WebClient client = new WebClient())
                                            {

                                                NameValueCollection postData = new NameValueCollection()
                                                {
                                                    //order: {"parameter name", "parameter value"}
                                                    {"steamID", Context.Player.SteamUserId + ""},
                                                    {"currentIP", currentIp },
                                                    {"gridCheck", ""}
                                                };
                                                pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridHandle.php", postData));

                                            }
                                            if (pagesource == "0")
                                            {
                                                SendGrid(gridTarget, serverTarget, Context.Player.IdentityId, target);

                                                Log.Warn("Connected clients to " + serverTarget + " @ " + ip);
                                            }
                                            else
                                            {
                                                Log.Fatal(pagesource);
                                                Context.Respond("Cannot transfer! You already have a transfer ready to be recieved!");
                                            }
                                        }
                                        catch
                                        {
                                            Context.Respond("Failure");
                                        }
                                    }
                                    else
                                    {
                                        Context.Respond("Cannot switch, not enough slots available");
                                    }
                                }
                                else
                                {
                                    Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                                }
                            }
                            else
                            {
                                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
                            }
                        }
                        else
                        {
                            Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                        }
                    }
                    else
                    {
                        Context.Respond("Grid Transfers are not enabled!");
                    }
                }
            }
        }

        [Command("debugsend", "Displays a list of Valid Server names for the '!switch me <servername>' command. ")]
        [Permission(MyPromoteLevel.None)]
        public void DebugSend(string gridTarget) {

            SendGrid(gridTarget, "", Context.Player.IdentityId, "", true);

        }

        public void SendGrid(string gridTarget, string serverTarget, long playerId, string ip, bool debug = false)
        {
            string externalIP;
            if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
            {
                externalIP = Plugin.Config.LocalIP;
            }
            else
            {
                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            }
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group relevantGroup = FindRelevantGroup(gridTarget, playerId);

            if (relevantGroup != null)
            {

                Directory.CreateDirectory("ExportedGrids");

                var path = string.Format(ExportPath, Context.Player.SteamUserId + "-" + gridTarget);
                if (File.Exists(path)) 
                {
                    Context.Respond("Export file already exists.");
                    return;
                }

                SerializeGridsToPath(relevantGroup, gridTarget, path);

                if(!debug && UploadGrid(serverTarget, gridTarget, ip, currentIp, path)) 
                {

                    /* Upload successful close the grids */
                    DeleteUploadedGrids(relevantGroup);

                    /* Also delete local file */
                    File.Delete(path);
                }
            }
            else
            {
                Context.Respond("Cannot transfer somone elses grid!");
            }
        }

        private void SerializeGridsToPath(MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group relevantGroup, string gridTarget, string path) {

            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (var node in relevantGroup.Nodes) 
            {

                MyCubeGrid grid = node.NodeData;

                /* We wanna Skip Projections... always */
                if (grid.Physics == null)
                    continue;

                var objectBuilder = grid.GetObjectBuilder(true) as MyObjectBuilder_CubeGrid;

                /* What else should it be? LOL? */
                if (objectBuilder == null)
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }

            MyObjectBuilder_PrefabDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_PrefabDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_PrefabDefinition)), gridTarget);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();

            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids) 
            {
                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks) 
                {
                    cubeBlock.Owner = 0L;
                    cubeBlock.BuiltBy = 0L;
                }
            }

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.Prefabs = new MyObjectBuilder_PrefabDefinition[] { definition };

            bool worked = MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);

            Log.Fatal("exported " + path+ " "+worked);
        }

        private bool UploadGrid(string serverTarget, string gridTarget, string ip, string currentIp, string path) {

            /* DO we need a using here too? */
            System.Net.WebClient Client = new System.Net.WebClient();
            Client.Headers.Add("Content-Type", "binary/octet-stream");

            try {

                byte[] result = Client.UploadFile("http://switchplugin.net/gridHandle.php", "POST", path);
                Log.Fatal("Grid was uploaded to webserver!");

                string s = System.Text.Encoding.UTF8.GetString(result, 0, result.Length);

                if (s == "1") {

                    Context.Respond("Connecting clients to " + serverTarget + " @ " + ip);
                    Context.Respond("Grid has been sent to the void! - Good luck!");
                    //ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                    using (WebClient client = new WebClient()) 
                    {

                        string pagesource = "";
                        NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"steamID", Context.Player.SteamUserId + ""},
                            {"gridName", gridTarget },
                            {"targetIP", ip },
                            {"currentIP", currentIp },
                            {"fileName", Context.Player.SteamUserId + "-" + gridTarget }
                        };

                        pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/gridHandle.php", postData));
                    }

                    return true;
                }
                else
                {
                    Context.Respond("Unable to switch grid!");
                }
            } 
            catch 
            {
                Log.Fatal("Cannot upload grid");
            }

            return false;
        }

        private void DeleteUploadedGrids(MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group relevantGroup) {

            foreach (var node in relevantGroup.Nodes) {

                MyCubeGrid grid = node.NodeData;

                /* We wanna Skip Projections... always */
                if (grid.Physics == null)
                    continue;

                grid.Close();
            }
        }

        private MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group FindRelevantGroup(string gridTarget, long playerId) {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = GridFinder.findGridGroupMechanical(gridTarget);

            /* Each Physical Grid group (physical included Connectors) */
            foreach (var group in groups) 
            {

                bool groupFound = false;

                /* Check each grid */
                foreach (var node in group.Nodes) 
                {

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

                    /* If nobody isnt the big owner then everythings fine. otherwise take the second biggest owner */
                    if (ownerCount > 0 && bigOnwerIds[0] != 0)
                        gridOwner = bigOnwerIds[0];
                    else if (ownerCount > 1)
                        gridOwner = bigOnwerIds[1];

                    /* Is the player ID the biggest owner? */
                    if (gridOwner == playerId) 
                    {
                        Log.Fatal("checking was completed");
                        groupFound = true;
                        break;
                    }
                }

                if (groupFound)
                    return group;
            }

            return null;
        }
    }
}

