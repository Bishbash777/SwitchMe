using NLog;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Torch.API.Plugins;
using Torch.Session;
using System.Runtime;
using System.Windows.Controls;
using Torch.API.Managers;
using System;
using System.Timers;
using System.Collections.Specialized;
using System.Net;
using Torch.API.Session;
using Torch.API;
using VRageMath;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Sandbox.Game;
using Sandbox.ModAPI.Ingame;
using VRage.Groups;
using VRage.Utils;
using VRage.Network;
using Sandbox.Engine.Multiplayer;
using Torch.Utils;
using System.Collections;
using VRage.Collections;
using VRage.ObjectBuilders;
using VRage.Replication;
using Sandbox;
using Torch.Commands;
using System.Linq;
using VRage.Game;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.Character;
using System.Collections.Concurrent;
using Sandbox.Common.ObjectBuilders;
using VRage;
using Sandbox.Game.Screens.Helpers;

namespace SwitchMe {

    public sealed class SwitchMePlugin : TorchPluginBase, IWpfPlugin {

        public SwitchMeConfig Config => _config?.Data;
        private Persistent<SwitchMeConfig> _config;
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
        private UserControl _control;
        public static string ip;
        private Timer _timer;
        private Vector3D JumpPos = Vector3D.One;
        private DateTime timerStart = new DateTime(0);
        private TorchSessionManager _sessionManager;
        private IMultiplayerManagerBase _multibase;
        private readonly List<long> player_ids_to_spawn = new List<long>();
        private readonly List<IMyPlayer> all_players = new List<IMyPlayer>();
        public List<string> zones = new List<string>();
        private readonly Dictionary<long, IMyPlayer> current_player_ids = new Dictionary<long, IMyPlayer>();
        private readonly Dictionary<ulong, string> target_file_list = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, bool> connecting = new Dictionary<ulong, bool>();
        public Dictionary<Vector3D, string> JumpInfo = new Dictionary<Vector3D, string>();
        private readonly List<long> clear_ids = new List<long>();
        //public bool connecting = false;
        private static Vector3D spawn_vector_location = Vector3D.One;
        private MatrixD spawn_matrix = MatrixD.Identity;
        private Dictionary<ulong, string> ClosestGate = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> DisplayedMessage = new Dictionary<ulong, bool>();
        public MyPlayer closestPlayer = null;
        private int _timerSpawn = 0;
        Dictionary<double, MyPlayer> distanceData = new Dictionary<double, MyPlayer>();
        private Dictionary<ulong,double> distance = new Dictionary<ulong, double>();
        private Dictionary<ulong,double> closestDistance = new Dictionary<ulong, double>();
        private Dictionary<ulong, bool> SafetyNet = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> PlayerSending = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> inZone = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> JumpProtect = new Dictionary<ulong, bool>();
        private Dictionary<long, string> Factions = new Dictionary<long, string>();
        private Dictionary<long, Dictionary<long, bool>> FMembers = new Dictionary<long, Dictionary<long, bool>>();

        private int tick = 0;


        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public Dictionary<ulong, CurrentCooldown> CurrentCooldownMapCommand { get; } = new Dictionary<ulong, CurrentCooldown>();
        public Dictionary<ulong, CurrentCooldown> ConfirmationsMapCommand { get; } = new Dictionary<ulong, CurrentCooldown>();
        public Dictionary<ulong, CurrentCooldown> ProtectionCooldownMap { get; } = new Dictionary<ulong, CurrentCooldown>();
        public long Cooldown { get { return Config.CooldownInSeconds * 1000; } }
        public long CooldownConfirmationSeconds { get { return Config.ConfirmationInSeconds; } }
        public long CooldownConfirmation { get { return Config.ConfirmationInSeconds * 1000; } }
        public long JoinProtect { get { return Config.JoinProtectTimer; } }
        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new SwitchMeControl(this));
        public void Save() => _config?.Save();
        MyPlayer player; 

        public override void Init(ITorchBase torch) {
            base.Init(torch);
            var configFile = Path.Combine(StoragePath, "SwitchMe.cfg");
            try {
                LoadSEDB();
                StartTimer();
                _config = Persistent<SwitchMeConfig>.Load(configFile);
                timerStart = new DateTime(0);
            } catch (Exception e) {
                Log.Warn(e);
            }
            if (_config?.Data == null) {
                Log.Info("Creating default confuration file, because none was found!");
                _config = new Persistent<SwitchMeConfig>(configFile, new SwitchMeConfig());
                Save();
            }
        }
        private async void Multibase_PlayerJoined(IPlayer obj) {

            if (Config.RecoverOnJoin && (!Config.EnabledMirror && !Config.LockedTransfer)) {
                Log.Error("Invalid setup for onjoin spawning - please make sure a position option is selected");
                return;
            }

            Log.Info( obj.SteamId.ToString() + " connected - Starting SwitchMe handle");
            CurrentCooldown cooldown = new CurrentCooldown(this);
            if (!Config.Enabled || !Config.RecoverOnJoin) 
                return;
            if (Config.XCord == null || Config.YCord == null || Config.ZCord == null) {
                Log.Warn("Invalid GPS configuration - cancelling spawn operation");
                return;
            }
            bool SwitchConnection = await CheckConnection(obj);
            if (!SwitchConnection) {
                return;
            }
            if (Config.EnabledJumpgate) {
                IEnumerable<string> channelIds = Config.Gates;
                foreach (string chId in channelIds) {
                    string name = chId.Split('/')[0];
                    string location = chId.Split('/')[1];
                    Vector3D.TryParse(location, out Vector3D gps);
                    var entry = gps;
                    MyAPIGateway.Session?.GPS.AddGps(MySession.Static.Players.TryGetIdentityId(obj.SteamId), new MyGps());
                }
            }

            string source = "";
            string filename = "";
            string targetFile = "";
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            using (HttpClient clients = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                        new KeyValuePair<string, string>("steamID",obj.SteamId.ToString()),
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
            Directory.CreateDirectory(StoragePath + "SwitchTemp");
            if (existance == "1") {
                filename = source.Split(':').Last() + ".xml";
                try {
                    string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                    targetFile = "SwitchTemp\\" + filename;
                    Log.Info("Downloading " + targetFile);
                    WebClient myWebClient = new WebClient();
                    myWebClient.DownloadFile(remoteUri, targetFile);
                    if (!target_file_list.ContainsKey(obj.SteamId)) {
                        target_file_list.Add(obj.SteamId, targetFile);
                    }
                    target_file_list[obj.SteamId] = targetFile;
                }
                catch (Exception error) {
                    Log.Fatal("Unable to download grid: " + error.ToString());
                    return;
                }
            }
            else {
                Log.Info("Player has no grid in transit");
                targetFile = null;
                return;
            }

            string POS = "";
            string POSsource = "";
            using (HttpClient clients = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("steamID", obj.SteamId.ToString()),
                    new KeyValuePair<string, string>("posCheck", "1"),
                    new KeyValuePair<string, string>("currentIP", currentIp)
                };
                FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                HttpResponseMessage httpResponseMessage = await clients.PostAsync("http://switchplugin.net/recovery.php", content);
                HttpResponseMessage response = httpResponseMessage;
                httpResponseMessage = null;
                string texts = await response.Content.ReadAsStringAsync();
                POSsource = texts;
                var config = Config;
                if (config.LockedTransfer)
                    POS = "{X:" + config.XCord + " Y:" + config.YCord + " Z:" + config.ZCord + "}";
                else if (config.EnabledMirror)
                    POS = POSsource.Substring(0, POSsource.IndexOf("^"));
                Vector3D.TryParse(POS, out Vector3D gps);
                spawn_vector_location = gps;
                Log.Info("Selected GPS: " + gps.ToString());
            }
            if (!connecting.ContainsKey(obj.SteamId)) {
                connecting.Add(obj.SteamId, true);
            }
            connecting[obj.SteamId] = true;
            if (!JumpProtect.ContainsKey(obj.SteamId)) {
                JumpProtect.Add(obj.SteamId, true);
            }
        }
        private void PlayerConnect(long playerId) {
            ulong steamid = MySession.Static.Players.TryGetSteamId(playerId);
            if (!player_ids_to_spawn.Contains(playerId) && connecting.ContainsKey(steamid)) {
                Log.Info("Spawning player");
                player_ids_to_spawn.Add(playerId);
            }
        }

        private void Multibase_PlayerLeft(IPlayer obj) {
            if (distance.ContainsKey(obj.SteamId)) {
                distance.Remove(obj.SteamId);
            }
            if (closestDistance.ContainsKey(obj.SteamId)) {
                closestDistance.Remove(obj.SteamId);
            }
            if (ClosestGate.ContainsKey(obj.SteamId)) {
                ClosestGate.Remove(obj.SteamId);
            }
            if (PlayerSending.ContainsKey(obj.SteamId)) {
                PlayerSending.Remove(obj.SteamId);
            }
            if (SafetyNet.ContainsKey(obj.SteamId)) {
                SafetyNet.Remove(obj.SteamId);
            }
            if (target_file_list.ContainsKey(obj.SteamId)) {
                target_file_list.Remove(obj.SteamId);
            }
            if (connecting.ContainsKey(obj.SteamId)) {
                connecting.Remove(obj.SteamId);
            }
            if (inZone.ContainsKey(obj.SteamId)) {
                inZone.Remove(obj.SteamId);
            }
        }



        public async Task<bool> CheckServer(IMyPlayer player, string servername, string target ) {
                string slotinfo = await CheckSlotsAsync(target);
                string existanceCheck = slotinfo.Split(';').Last();
                bool paired = await CheckKeyAsync(target);


                if (target.Length < 1) {
                Log.Warn("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                    utils.NotifyMessage("Unknown Server. Please use '!switch list' to see a list of valid servers!", player.SteamUserId);
                    return false;
                }

                if (existanceCheck != "1") {
                Log.Warn("Cannot communicate with target, please make sure SwitchMe is installed there!");
                    utils.NotifyMessage("Cannot communicate with target, please make sure SwitchMe is installed there!", player.SteamUserId);
                    return false;
                }

                if (paired != true) {
                Log.Warn("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                    utils.NotifyMessage("Unauthorised Switch! Please make sure the servers have the same Bind Key!", player.SteamUserId);
                    return false;
                }

                ///   Slot checking
                Log.Warn("Checking " + target);
                int currentRemotePlayers = int.Parse(slotinfo.Substring(0, slotinfo.IndexOf(":")));
                string max = slotinfo.Substring(slotinfo.IndexOf(':') + 1, slotinfo.IndexOf(';') - slotinfo.IndexOf(':') - 1);
                int currentLocalPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                currentLocalPlayers = 1;
                int maxi = int.Parse(max);
                int maxcheck = currentLocalPlayers + currentRemotePlayers;
                if (maxcheck > maxi && !player.IsAdmin) {
                    utils.NotifyMessage("Not enough slots free to use gate!", player.SteamUserId);
                    return false;
                }
                return true;
        }


        public override async void Update() {
            try {
                tick++;
                string name = "";
                string location = "";
                string port = "";

                //Enter jumpgate logic.
                if (Config.EnabledJumpgate) {
                    if (tick % 16 == 0) {
                        foreach (var playerOnline in MySession.Static.Players.GetOnlinePlayers()) {
                            var player = utils.GetPlayerByNameOrId(playerOnline.DisplayName);
                            if (!PlayerSending.ContainsKey(player.SteamUserId)) {
                                PlayerSending.Add(player.SteamUserId, true);
                            }
                            IEnumerable<string> channelIds = Config.Gates;
                            bool firstcheck = true;
                            distance.Clear();
                            closestDistance.Clear();
                            ClosestGate.Clear();
                            foreach (string chId in channelIds) {

                                name = chId.Split('/')[0];
                                location = chId.Split('/')[1];
                                Vector3D.TryParse(location, out Vector3D gps);
                                if (firstcheck) {
                                    closestDistance.Add(player.SteamUserId, Vector3D.DistanceSquared(player.GetPosition(), gps));
                                    ClosestGate.Add(player.SteamUserId, name);
                                }
                                if (!firstcheck) {
                                    double value1 = Vector3D.DistanceSquared(player.GetPosition(), gps);
                                    double value2 = closestDistance[player.SteamUserId];
                                    if (Vector3D.DistanceSquared(player.GetPosition(), gps) < closestDistance[player.SteamUserId]) {
                                        closestDistance[player.SteamUserId] = Vector3D.DistanceSquared(player.GetPosition(), gps);
                                        ClosestGate[player.SteamUserId] = name;
                                    }
                                }
                                firstcheck = false;
                            }
                            channelIds = Config.Servers.Where(c => c.Split(':')[0].Equals(ClosestGate[player.SteamUserId]));

                            foreach (string chId in channelIds) {
                                ip = chId.Split(':')[1];
                                name = chId.Split(':')[0];
                                port = chId.Split(':')[2];
                            }
                            string target = ip + ":" + port;
                            ip += ":" + port;
                            if (DisplayedMessage.ContainsKey(player.SteamUserId) && closestDistance[player.SteamUserId] > 22505) {
                                DisplayedMessage[player.SteamUserId] = true;
                            }

                            if (Config.Debug && tick % 64 == 0) {
                                Log.Warn($"{player.DisplayName} is {closestDistance[player.SteamUserId]} away (meters squared)");
                            }

                            if (closestDistance[player.SteamUserId] < 1000000 /* 1KM away from jumpCentre */) {
                                if (closestDistance[player.SteamUserId] > 3025) {
                                    if (JumpProtect.ContainsKey(player.SteamUserId)) {
                                        JumpProtect[player.SteamUserId] = false;
                                    }
                                }
                                if (!DisplayedMessage.ContainsKey(player.SteamUserId)) {
                                    DisplayedMessage.Add(player.SteamUserId, false);
                                }

                                if (!DisplayedMessage[player.SteamUserId]) {
                                    if (!await CheckServer(player, name, target)) {
                                        return;
                                    }
                                    utils.NotifyMessage($"You are approaching the Jumpgate for {name}... Proceed with Caution", player.SteamUserId);
                                    DisplayedMessage[player.SteamUserId] = true;
                                }
                                if (closestDistance[player.SteamUserId] <= 2500 ) {
                                    /* If he is online we check if he is currently seated. If he is - get the grid name */
                                    if (player?.Controller.ControlledEntity is MyCockpit controller) {
                                        string gridname = controller.Parent.DisplayName;
                                        //Log.Error("Player seated in: " + gridname);
                                        try {
                                            if (!inZone.ContainsKey(player.SteamUserId)) {
                                                inZone.Add(player.SteamUserId, false);
                                            }
                                            if (inZone[player.SteamUserId] == false) {
                                                inZone[player.SteamUserId] = true;
                                                VoidManager voidm = new VoidManager(this);
                                                await voidm.SendGrid(gridname, ClosestGate[player.SteamUserId], player.DisplayName, player.IdentityId, ip);
                                                inZone[player.SteamUserId] = false;
                                            }
                                        }
                                        catch (Exception e) {
                                            PlayerSending[player.SteamUserId] = true;
                                            Log.Warn(e.ToString());
                                        }
                                    }
                                }
                            }
                            else {
                                if (DisplayedMessage.ContainsKey(player.SteamUserId)) {
                                    DisplayedMessage[player.SteamUserId] = false;
                                }
                            }
                            if (SafetyNet.ContainsKey(player.SteamUserId)) {
                                SafetyNet[player.SteamUserId] = false;
                            }
                            firstcheck = true;
                        }
                        ClosestGate.Clear();

                    }
                }


                _timerSpawn += 1;
                if (_timerSpawn % 60 == 0) {

                    all_players.Clear();
                    current_player_ids.Clear();

                    MyAPIGateway.Multiplayer.Players.GetPlayers(all_players);
                    foreach (var player in all_players)
                        current_player_ids.Add(player.IdentityId, player); //Refresh player list every second
                    clear_ids.Clear();
                    foreach (var player_id in player_ids_to_spawn) {
                        if (!current_player_ids.ContainsKey(player_id))
                            continue;

                        if (current_player_ids[player_id].Character != null && current_player_ids[player_id].Controller?.ControlledEntity?.Entity != null) {
                            clear_ids.Add(player_id); //Avoids spawning people who are in grids / Character already exists

                        }
                        else {
                            string externalIP = utils.CreateExternalIP(Config);
                            string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;
                            ulong steamid = MySession.Static.Players.TryGetSteamId(player_id);
                            var player = utils.GetPlayerByNameOrId(player_id.ToString());
                            if (connecting[steamid] == true) {
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                                    spawn_matrix = MatrixD.CreateWorld(spawn_vector_location);
                                    MyVisualScriptLogicProvider.SpawnPlayer(spawn_matrix, Vector3D.Zero, player_id); //Spawn function
                                });
                                await recovery(player_id, spawn_vector_location);
                                MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                                    var playerEndpoint = new Endpoint(steamid, 0);
                                    var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
                                    var clientDataDict = _clientStates.Invoke(replicationServer);
                                    object clientData;
                                    try {
                                        clientData = clientDataDict[playerEndpoint];
                                    }
                                    catch {
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
                                });
                            }
                            clear_ids.Add(player_id);
                        }
                    }
                    foreach (var clear_id in clear_ids) {
                        player_ids_to_spawn.Remove(clear_id); //Cleanup
                    }
                }
            }
            catch (Exception e) {
                Log.Error(e.ToString());
            }
        }

        private async Task recovery(long playerid, Vector3D spawn_vector_location) {
            ulong steamid = MySession.Static.Players.TryGetSteamId(playerid);
            connecting.Remove(steamid);


            if (MyObjectBuilderSerializer.DeserializeXML(target_file_list[steamid], out MyObjectBuilder_Definitions myObjectBuilder_Definitions)) {

                try {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {

                        Log.Info($"Importing grid from {target_file_list[steamid]}");
                        if (!SafetyNet.ContainsKey(steamid)) {
                            SafetyNet.Add(steamid, true);
                        }

                        var prefabs = myObjectBuilder_Definitions.Prefabs;

                        if (prefabs == null || prefabs.Length != 1) {
                            Log.Info($"Grid has unsupported format!");
                            return;
                        }
                        var prefab = prefabs[0];
                        var grids = prefab.CubeGrids;
                        /* Where do we want to paste the grids? Lets find out. */
                        var pos = FindPastePosition(grids, spawn_vector_location);
                        if (pos == null) {
                            Log.Info("No free place.");
                            return;
                        }

                        /* Update GridsPosition if that doesnt work get out of here. */
                        if (!UpdateGridsPosition(grids, (Vector3D)pos)) {
                            Log.Error("Failed to find update the grids position");
                            return;
                        }

                        /* Remapping to prevent any key problems upon paste. */
                        MyEntities.RemapObjectBuilderCollection(grids);
                        foreach (var grid in grids) {

                            if (MyEntities.CreateFromObjectBuilderAndAdd(grid, true) is MyCubeGrid cubeGrid)
                                FixOwnerAndAuthorShip(cubeGrid, playerid);

                        }
                    });
                    Log.Info("Grid has been pulled from the void!");
                    string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
                    string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
                    DeleteFromWeb(currentIp);
                    await RemoveConnection(steamid);

                    return;
                }
                catch (Exception error) {
                    Log.Error(error.ToString());
                }
            }
        }

        private void FixOwnerAndAuthorShip(MyCubeGrid myCubeGrid, long playerId) {
            ulong steamid = MySession.Static.Players.TryGetSteamId(playerId);
            MyPlayer id = MySession.Static.Players.TryGetPlayerBySteamId(steamid);
            MySession.Static.Players.TryGetPlayerById(id.Id, out player);

            HashSet<long> authors = new HashSet<long>();
            HashSet<MySlimBlock> blocks = new HashSet<MySlimBlock>(myCubeGrid.GetBlocks());

            foreach (MySlimBlock block in blocks) {

                if (block == null || block.CubeGrid == null || block.IsDestroyed)
                    continue;

                MyCubeBlock cubeBlock = block.FatBlock;
                if (cubeBlock != null && cubeBlock.OwnerId != playerId) {

                    myCubeGrid.ChangeOwnerRequest(myCubeGrid, cubeBlock, 0, MyOwnershipShareModeEnum.Faction);

                    if (playerId != 0)
                        myCubeGrid.ChangeOwnerRequest(myCubeGrid, cubeBlock, playerId, MyOwnershipShareModeEnum.Faction);
                }
                if (block.BuiltBy == 0) {
                    /* 
                    * Hack: TransferBlocksBuiltByID only transfers authorship if it has an author. 
                    * Transfer Authorship Client just sets the author so we need to take care of limits ourselves. 
                    */
                    block.TransferAuthorshipClient(playerId);
                    block.AddAuthorship();
                }
                authors.Add(block.BuiltBy);

                IMyCharacter character = player.Character;

                if (cubeBlock is Sandbox.ModAPI.IMyCockpit cockpit && cockpit.CanControlShip)
                    cockpit.AttachPilot(character);
            }

            foreach (long author in authors)
                MyMultiplayer.RaiseEvent(myCubeGrid, x => new Action<long, long>(x.TransferBlocksBuiltByID), author, playerId, new EndpointId());
        }
        private Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D position) {
            Vector3? vector = null;
            float radius = 0F;
            Vector3D gps;

            foreach (var grid in grids) {

                var gridSphere = grid.CalculateBoundingSphere();

                /* If this is the first run, we use the center of that grid, and its radius as it is */
                if (vector == null) {

                    vector = gridSphere.Center;
                    radius = gridSphere.Radius;
                    continue;
                }

                /* 
                 * If its not the first run, we use the vector we already have and 
                 * figure out how far it is away from the center of the subgrids sphere. 
                 */
                float distance = Vector3.Distance(vector.Value, gridSphere.Center);

                /* 
                 * Now we figure out how big our new radius must be to house both grids
                 * so the distance between the center points + the radius of our subgrid.
                 */
                float newRadius = distance + gridSphere.Radius;

                /*
                 * If the new radius is bigger than our old one we use that, otherwise the subgrid 
                 * is contained in the other grid and therefore no need to make it bigger. 
                 */
                if (newRadius > radius)
                    radius = newRadius;
            }

            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */


            return MyEntities.FindFreePlace(position, radius);


        }

        public bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition) {
            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            foreach (var grid in grids) {
                var position = grid.PositionAndOrientation;
                if (position == null) {
                    Log.Info($"Grid is missing location Information!");
                    return false;
                }
                var realPosition = position.Value;
                var currentPosition = realPosition.Position;
                if (firstGrid) {
                    deltaX = newPosition.X - currentPosition.X;
                    deltaY = newPosition.Y - currentPosition.Y;
                    deltaZ = newPosition.Z - currentPosition.Z;
                    currentPosition.X = newPosition.X;
                    currentPosition.Y = newPosition.Y;
                    currentPosition.Z = newPosition.Z;
                    firstGrid = false;
                }
                else {
                    currentPosition.X += deltaX;
                    currentPosition.Y += deltaY;
                    currentPosition.Z += deltaZ;
                }
                realPosition.Position = currentPosition;
                grid.PositionAndOrientation = realPosition;
            }
            return true;
        }

        public async Task RemoveConnection(ulong player) {
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            if (externalIP.Contains("0.0")
                || externalIP.Contains("127.0")
                || externalIP.Contains("192.168")) {
                externalIP = Config.LocalIP;
            }

            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            Log.Warn("Removing conneciton flag for " + player);
            using (HttpClient client = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("BindKey", Config.LocalKey),
                    new KeyValuePair<string, string>("CurrentIP", currentIp ),
                    new KeyValuePair<string, string>("RemoveConnection", player.ToString())
                };
                FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                await client.PostAsync("http://switchplugin.net/api/index.php", content);
            }
        }
        public async Task<bool> CheckConnection(IPlayer player) {
            string externalIP;
            if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("10.0")) {

                externalIP = Config.LocalIP;

                if (externalIP.Contains("127.0")
                || externalIP.Contains("192.168")
                || externalIP.Contains("0.0")
                || externalIP.Contains("10.0")) {
                    Log.Error("Incorrect IP setup... SwitchMe will NOT work");
                    return false;
                }

            }
            else {
                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            }
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            Log.Warn("Checking inbound conneciton for " + player.SteamId);
            string pagesource;
            using (HttpClient client = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("BindKey", Config.LocalKey),
                    new KeyValuePair<string, string>("CurrentIP", currentIp ),
                    new KeyValuePair<string, string>("ConnectionCheck", player.SteamId.ToString())
                };
                FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                HttpResponseMessage httpResponseMessage = await client.PostAsync("http://switchplugin.net/api/index.php", content);
                HttpResponseMessage response = httpResponseMessage;
                httpResponseMessage = null;
                string text = await response.Content.ReadAsStringAsync();
                pagesource = text;
                Log.Warn(pagesource);
            }

            if (pagesource.Contains("connecting=false")) {
                return false;
            }
            return true ;
        }

        public void Delete(string entityName) {
            try {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                    var name = entityName;

                    if (string.IsNullOrEmpty(name))
                        return;

                    if (!TryGetEntityByNameOrId(name, out IMyEntity entity))
                        return;

                    if (entity is IMyCharacter)
                        return;

                    entity.Close();

                    Log.Warn("Entitiy deleted.");
                });
            }
            catch (Exception e) {
                Log.Error(e.ToString());
            }
        }


        public static IMyPlayer GetPlayerByNameOrId(string nameOrPlayerId) {
            if (!long.TryParse(nameOrPlayerId, out long id)) {
                foreach (var identity in MySession.Static.Players.GetAllIdentities()) {
                    if (identity.DisplayName == nameOrPlayerId) {
                        id = identity.IdentityId;
                    }
                }
            }

            if (MySession.Static.Players.TryGetPlayerId(id, out MyPlayer.PlayerId playerId)) {
                if (MySession.Static.Players.TryGetPlayerById(playerId, out MyPlayer player)) {
                    return player;
                }
            }

            return null;
        }


        public async Task<string> CheckSlotsAsync(string targetIP) {

            string maxPlayers = MySession.Static.MaxPlayers.ToString();
            string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
            string pagesource = "";

            using (HttpClient client = new HttpClient()) {

                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("currentplayers", currentPlayers ),
                    new KeyValuePair<string, string>("maxplayers", maxPlayers),
                    new KeyValuePair<string, string>("targetip", targetIP)
                };

                FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);

                HttpResponseMessage httpResponseMessage = await client.PostAsync("http://switchplugin.net/index.php", content);
                HttpResponseMessage response = httpResponseMessage;
                httpResponseMessage = null;

                string text = await response.Content.ReadAsStringAsync();

                pagesource = text;
            }

            return pagesource;
        }

        private void SessionChanged(ITorchSession session, TorchSessionState state) {

            if (!Config.Enabled)
                return;

            switch (state) {

                case TorchSessionState.Loaded:
                    //load
                    int gates = 0;
                    MyVisualScriptLogicProvider.PlayerConnected += PlayerConnect;
                    LoadSEDB();
                    IEnumerable<string> channelIds = Config.Gates;
                    string name = "";
                    string location = "";
                    foreach (string chId in channelIds) {
                        name = chId.Split('/')[0];
                        location = chId.Split('/')[1];
                        Vector3D.TryParse(location, out Vector3D gps);
                        var ob = new MyObjectBuilder_SafeZone();
                        ob.PositionAndOrientation = new MyPositionAndOrientation(gps, Vector3.Forward, Vector3.Up);
                        ob.PersistentFlags = MyPersistentEntityFlags2.InScene;
                        ob.Shape = MySafeZoneShape.Sphere;
                        ob.Radius = (float)50;
                        ob.Enabled = true;
                        ob.DisplayName = $"SM-{gps}";
                        ob.Texture = "RAIN";
                        ob.AccessTypeGrids = MySafeZoneAccess.Blacklist;
                        ob.AccessTypeFloatingObjects = MySafeZoneAccess.Blacklist;
                        ob.AccessTypeFactions = MySafeZoneAccess.Blacklist;
                        ob.AccessTypePlayers = MySafeZoneAccess.Blacklist;
                        var zone = MyEntities.CreateFromObjectBuilderAndAdd(ob, true);
                        gates++;
                        if (!zones.Contains(ob.DisplayName)) {
                            zones.Add(ob.DisplayName);
                        }
                    }
                    Log.Info($"{gates} Jumpgates created!");
                        break;

                case TorchSessionState.Unloaded:
                    //unload
                    MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnect;
                    timerStart = new DateTime(0);
                    UnloadSEDB();
                    break;

                case TorchSessionState.Unloading:
                    int i = 0;
                    foreach (var zone in zones) {
                        foreach (var entity in MyEntities.GetEntities()) {
                            if (entity?.DisplayName?.Contains(zone, StringComparison.CurrentCultureIgnoreCase) ?? false) {
                                i++;
                                entity.Close();
                            }
                        }
                    }
                    Log.Info($"{i} Jumpgates closed!");
                    break;

                default:
                    // ignore
                    break;
            }
        }

        public void StartTimer() {

            if (_timer != null)
                StopTimer();

            _timer = new Timer(5000);

            Task.Run(() => _timer.Elapsed += _timer_Elapsed);

            _timer.Enabled = true;
        }

        public void StopTimer() {

            if (_timer != null) {
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Enabled = false;
                _timer.Dispose();
                _timer = null;
            }
        }

        public void UnloadSEDB() {
            Dispose();
        }

        public async Task<bool> CheckKeyAsync(string target) {

            string pagesource;

            try {

                using (HttpClient client = new HttpClient()) {

                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("targetip", target),
                        new KeyValuePair<string, string>("bindKey", Config.LocalKey),
                        new KeyValuePair<string, string>("bindCheck", "1")
                    };

                    FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                    HttpResponseMessage httpResponseMessage = await client.PostAsync("http://switchplugin.net/index.php", content);
                    HttpResponseMessage response = httpResponseMessage;
                    httpResponseMessage = null;

                    string text = await response.Content.ReadAsStringAsync();

                    pagesource = text;
                }

                return pagesource == Config.LocalKey;
                
            } catch (Exception e) {
                Log.Warn("Error communcating with API: " + e.ToString());
                return false;
            }
        }

        public async Task<bool> CheckInboundAsync(string target) {

            string pagesource;

            try {

                using (HttpClient client = new HttpClient()) {

                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("targetip", target),
                    };

                    FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                    HttpResponseMessage httpResponseMessage = await client.PostAsync("http://switchplugin.net/endpoint.php", content);
                    HttpResponseMessage response = httpResponseMessage;
                    httpResponseMessage = null;

                    string text = await response.Content.ReadAsStringAsync();

                    pagesource = text;
                }

                return pagesource == "Y";

            } catch (Exception e) {
                Log.Warn("Error communcating with API: " + e.ToString());
                return false;
            }
        }

        public bool CheckStatus(string target) {

            string pagesource;

            try {

                using (WebClient client = new WebClient()) {

                    NameValueCollection postData = new NameValueCollection()
                    {
                        //order: {"parameter name", "parameter value"}
                        {"targetip", target},
                    };

                    pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/status.php", postData));

                    if (pagesource == "ONLINE") {
                        return true;
                    }
                }

            } catch {
                Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
            }

            return false;
        }

        public void LoadSEDB() {

            if (_sessionManager == null) {

                _sessionManager = Torch.Managers.GetManager<TorchSessionManager>();

                if (_sessionManager == null) 
                    Log.Warn("No session manager loaded!");
                else 
                    _sessionManager.SessionStateChanged += SessionChanged;
            }

            if (Torch.CurrentSession != null) {
                if (_multibase == null) {
                    _multibase = Torch.CurrentSession.Managers.GetManager<IMultiplayerManagerBase>();
                    if (_multibase == null) {
                        Log.Warn("No join/leave manager loaded!");
                    }
                    else {
                        _multibase.PlayerJoined += Multibase_PlayerJoined;
                        _multibase.PlayerLeft += Multibase_PlayerLeft;
                    }
                }
                InitPost();
            }
        }

        public override void Dispose() {

            if (_multibase != null)
                _multibase.PlayerJoined -= Multibase_PlayerJoined;

            _multibase = null;

            if (_sessionManager != null)
                _sessionManager.SessionStateChanged -= SessionChanged;

            _sessionManager = null; ;

            StopTimer();
        }

        public static bool TryGetEntityByNameOrId(string nameOrId, out IMyEntity entity) {

            if (long.TryParse(nameOrId, out long id))
                return MyAPIGateway.Entities.TryGetEntityById(id, out entity);

            foreach (var ent in MyEntities.GetEntities()) {

                if (ent.DisplayName == nameOrId) {
                    entity = ent;
                    return true;
                }
            }

            entity = null;

            return false;
        }


        public static MyIdentity GetIdentityByName(string playerName) {

            foreach (var identity in MySession.Static.Players.GetAllIdentities())
                if (identity.DisplayName == playerName)
                    return identity;

            return null;
        }

        int i = 0;

        private void InitPost() {
            StartTimer();
        }

        public void DeleteFromWeb(string ip) {

            using (WebClient client = new WebClient()) {


                NameValueCollection postData = new NameValueCollection()
                {
                    { "posCheck", "processed"},
                    { "currentIP", ip}
                };

                client.UploadValues("http://switchplugin.net/recovery.php", postData);
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                string xml = "";
                try {
                    xml = File.ReadAllText(Path.Combine(StoragePath, "SwitchMe.cfg"));
                }
                catch {
                    xml = File.ReadAllText("SwitchMe.cfg");
                }


                string externalIP;
                string Inbound = "N";

                if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168")
                    || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("10.0")) {

                    externalIP = Config.LocalIP;

                    if (externalIP.Contains("127.0")
                    || externalIP.Contains("192.168")
                    || externalIP.Contains("0.0")
                    || externalIP.Contains("10.0")) {
                        i++;
                        if (i == 300) { Log.Warn("Please have your public ip set in the SwitchMe or Torch Config. Search 'Whats my ip?' on google if you are not sure how to find this."); i = 0; }
                    }

                }
                else {
                    externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
                }

                if (timerStart.Ticks == 0) timerStart = e.SignalTime;

                if (Torch.CurrentSession?.State == TorchSessionState.Loaded) {
                    string maxPlayers = MySession.Static.MaxPlayers.ToString();
                    string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
                    string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

                    if (Torch.CurrentSession != null && currentIp.Length > 1) {

                        if (Config.InboundTransfersState)
                            Inbound = "Y";
                        try {
                            using (WebClient client = new WebClient()) {

                                NameValueCollection postData = new NameValueCollection()
                                {
                                    //order: {"parameter name", "parameter value"}
                                    { "currentplayers", currentPlayers },
                                    { "maxplayers", maxPlayers },
                                    { "serverip", currentIp},
                                    { "verion", "1.4.02"},
                                    { "bindKey", Config.LocalKey},
                                    { "inbound", Inbound },
                                    { "name", Sandbox.MySandboxGame.ConfigDedicated.ServerName },
                                    { "config", xml }
                                };

                                client.UploadValues("http://switchplugin.net/index.php", postData);
                            }
                        }
                        catch (Exception es) {
                            Log.Warn("Data error: " + es.ToString());
                        }
                    }
                }
            } catch(Exception error) {
                Log.Error(error.ToString);
            }
        }
    }
}
