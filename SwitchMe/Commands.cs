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
                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), Context.Player.SteamUserId);
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
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
                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                    ModCommunication.SendMessageTo(new JoinServerMessage(ip), Context.Player.SteamUserId);
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                }
                catch {
                    Context.Respond("Failure");
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
                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                    ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
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
                    Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip);
                    ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                    Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip);
                }
                catch {
                    Context.Respond("Failure");
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
        public async void Recover() {

            if (Context.Player == null) {
                Context.Respond("Command cannot be ran from console");
                return;
            }

            string externalIP = Utilities.CreateExternalIP(Plugin.Config);
            string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;

            VoidManager voidManager = new VoidManager(Plugin, Context);

            
            Tuple<string, string, Vector3D> data = await voidManager.DownloadGridAsync(currentIp);

            if (data == null)
            {
                return;
            }
            string targetFile = data.Item1;
            string filename = data.Item2;
            Vector3D newPos = data.Item3;
            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                GridImporter gridManager = new GridImporter(Plugin, Context);
                if (gridManager.DeserializeGridFromPath(targetFile, Context.Player.IdentityId, newPos))
                {
                    File.Delete(targetFile);
                    Plugin.DeleteFromWeb(currentIp);
                }
            });
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
            }


            string slotinfo = await Plugin.CheckSlotsAsync(target);
            existanceCheck = slotinfo.Split(';').Last();
            bool paired = await Plugin.CheckKeyAsync(target);

            if (target.Length < 1) {
                Context.Respond("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                return;
            }

            if (existanceCheck != "1") {
                Context.Respond("Cannot communicate with target, please make sure SwitchMe is installed there!");
            }

            if (!Plugin.CheckStatus(target)) {
                Context.Respond("Target server is offline, preventing switch");
                return;
            }

            bool InboundCheck = await Plugin.CheckInboundAsync(target);
            if (!InboundCheck) {
                Context.Respond("The target server does not allow inbound transfers");
                return;
            }

            if (!paired) {
                Context.Respond("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
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
            if (maxcheck > maxi) {
                Context.Respond("Cannot switch, not enough slots available");
            }
            Context.Respond("Slot checking passed!");

            var p = Context.Player;
            var parent = p.Character?.Parent;
            if (parent == null) {
            }
            if (parent is MyShipController sc) {
                sc.RemoveUsers(false);
            }


            try {

                string externalIP = Utilities.CreateExternalIP(Plugin.Config);
                string pagesource = "";
                string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;

                /* Not sure what this does but it does not belong here */
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

                    if (!await new VoidManager(Plugin, Context).SendGrid(gridTarget, serverTarget, Context.Player.IdentityId, target))
                    {
                        return;
                    }
                    Log.Warn("Connected clients to " + serverTarget + " @ " + ip);
                } else {

                    Log.Fatal(pagesource);
                    Context.Respond("Cannot transfer! You have a transfer ready to be recieved!");
                    return;
                }

            } catch (Exception e) {
                Log.Fatal(e, e.Message);
                Context.Respond("Failure");
            }
        }

        [Command("restore", "Completes the transfer of one grid from one server to another")]
        [Permission(MyPromoteLevel.None)]
        public void Restore() {
            Recover();
        }
    }
}

