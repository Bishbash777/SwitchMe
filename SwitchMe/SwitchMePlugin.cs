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
using System.Text.Json;
using System.Text.Json.Serialization;
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
using System.Reflection;

namespace SwitchMe {

    public sealed class SwitchMePlugin : TorchPluginBase, IWpfPlugin {

        public utils utils = new utils();
        public SwitchMeConfig Config => _config?.Data;
        private Persistent<SwitchMeConfig> _config;

        private UserControl _control;
        public static string ip;
        private Timer _timer;
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
        private static Vector3D spawn_vector_location = Vector3D.One;
        private MatrixD spawn_matrix = MatrixD.Identity;
        public bool update_debug = false;
        private Dictionary<ulong, bool> DisplayedMessage = new Dictionary<ulong, bool>();
        private int _timerSpawn = 0;
        private Dictionary<ulong, bool> inZone = new Dictionary<ulong, bool>();
        private Dictionary<ulong, bool> JumpProtect = new Dictionary<ulong, bool>();
        private Dictionary<long, string> Factions = new Dictionary<long, string>();

        public bool use_online_config = false;

        private Dictionary<long, Dictionary<long, bool>> FMembers = new Dictionary<long, Dictionary<long, bool>>();
        private int tick = 0;
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public Dictionary<ulong, CurrentCooldown> CurrentCooldownMapCommand { get; } = new Dictionary<ulong, CurrentCooldown>();
        public Dictionary<ulong, CurrentCooldown> ConfirmationsMapCommand { get; } = new Dictionary<ulong, CurrentCooldown>();
        public Dictionary<ulong, CurrentCooldown> ProtectionCooldownMap { get; } = new Dictionary<ulong, CurrentCooldown>();
        public long Cooldown { get { return Config.CooldownInSeconds * 1000; } }
        public long CooldownConfirmationSeconds { get { return Config.ConfirmationInSeconds; } }
        public long CooldownConfirmation { get { return Config.ConfirmationInSeconds * 1000; } }
        public UserControl GetControl() => _control ?? (_control = new SwitchMeControl(this));
        public void Save() => _config?.Save();
        MyPlayer player;
        public bool debug = false;
        public string API_URL = "http://switchplugin.net/api2/";
        public bool loadFailure = false;




        public override void Init(ITorchBase torch) {
            base.Init(torch);
            var configFile = Path.Combine(StoragePath, "SwitchMe.cfg");
            try {
                Load();
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
             APIMethods API = new APIMethods(this);
            if (debug) { Log.Info(obj.SteamId.ToString() + " connected - Starting SwitchMe process"); }
            DisplayedMessage.Add(obj.SteamId, false);
            inZone.Add(obj.SteamId, false);
            CurrentCooldown cooldown = new CurrentCooldown(this);
            if (!Config.Enabled) 
                return;
            if (Config.XCord == null || Config.YCord == null || Config.ZCord == null) {
                if (debug) { Log.Error("Invalid GPS configuration - cancelling spawn operation"); }
                return;
            }
            bool SwitchConnection = await API.CheckConnectionAsync(obj);
            if (!SwitchConnection) {
                return;
            }
           
            string filename = "";
            string targetFile = "";


            var api_response = await API.FindWebGridAsync(obj.SteamId);

            if (api_response["responseCode"] == "0") {
                Log.Info("Grid found in database... attempting download!");
                filename = api_response["filename"] + ".xml";
                try {
                    string remoteUri = "http://www.switchplugin.net/transportedGrids/" + filename;
                    targetFile = "SwitchTemp\\" + filename;
                    if (debug) { Log.Info("Downloading " + targetFile); }
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
                if (debug) { Log.Error("Player has no grid in transit"); }
                targetFile = null;
                return;
            }

            string POS = "";
            string gateName = await API.GetGateAsync(obj.SteamId.ToString());
            bool foundGate = false;
            IEnumerable<string> channelIds = Config.Gates.Where(c => c.Split('/')[2].Equals(gateName));
            foreach (string chId in channelIds) {
                POS = chId.Split('/')[1];
                foundGate = true;
            }
            if (Config.RandomisedExit) {
                Dictionary<string, string> gateSelection = new Dictionary<string, string>();
                channelIds = Config.Gates;
                int i = 0;
                foreach (string gate in channelIds) {
                    i++;
                    gateSelection.Add(gate.Split('/')[2], gate.Split('/')[1]);
                }
                if (i != 0) {
                    POS = utils.SelectRandomGate(gateSelection);
                }
            }
            if (!Config.RandomisedExit) {
                if (debug) { Log.Info($"API: Gate elected = {gateName}"); }
            }
            else {
                if (debug) { Log.Info("Using randomly selected gate as exit"); }
            }

            if (!foundGate) {
                POS = "{X:" + Config.XCord + " Y:" + Config.YCord + " Z:" + Config.ZCord + "}";
                if (debug) { Log.Info($"Target gate ({gateName}) does not exist... Using default"); }
            }

            POS = POS.TrimStart('{').TrimEnd('}');
            Vector3D.TryParse(POS, out Vector3D gps);
            spawn_vector_location = gps;
            if (debug) { Log.Info("Selected GPS: " + gps.ToString()); }
            
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
                if (debug) { Log.Info("Spawning player"); }
                player_ids_to_spawn.Add(playerId);
            }
        }

        private void Multibase_PlayerLeft(IPlayer obj) {
            if (target_file_list.ContainsKey(obj.SteamId)) {
                target_file_list.Remove(obj.SteamId);
            }
            if (connecting.ContainsKey(obj.SteamId)) {
                connecting.Remove(obj.SteamId);
            }
            if (inZone.ContainsKey(obj.SteamId)) {
                inZone.Remove(obj.SteamId);
            }
            if (DisplayedMessage.ContainsKey(obj.SteamId)) {
                DisplayedMessage.Remove(obj.SteamId);
            }
        }

        public async Task ScanSpawnablePlayers() {
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
                        if (debug) { Log.Info("Character exists! Preventing recovery"); }
                    }
                    else {
                        string externalIP = utils.CreateExternalIP(Config);
                        string currentIp = externalIP + ":" + MySandboxGame.ConfigDedicated.ServerPort;
                        ulong steamid = MySession.Static.Players.TryGetSteamId(player_id);
                        var player = utils.GetPlayerByNameOrId(player_id.ToString());
                        if (debug) { Log.Info("Starting recovery process"); }
                        if (connecting[steamid] == true) {
                            CloseGates();
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                                spawn_matrix = MatrixD.CreateWorld(spawn_vector_location);
                                MyVisualScriptLogicProvider.SpawnPlayer(spawn_matrix, Vector3D.Zero, player_id); //Spawn function
                            });
                            await recovery(player_id, spawn_vector_location);
                            utils.RefreshPlayer(steamid);
                            OpenGates();
                        }
                        clear_ids.Add(player_id);
                    }
                }
                foreach (var clear_id in clear_ids) {
                    player_ids_to_spawn.Remove(clear_id); //Cleanup
                }
            }
        }

        public override async void Update() {
            try {
                //Scan for players near/inside jump areas
                await Scan();

                //Check for players that need to be spawned
                await ScanSpawnablePlayers();
            }
            catch (Exception e) {
                Log.Error(e.ToString());
            }
        }

        private async Task recovery(long playerid, Vector3D spawn_vector_location) {
            APIMethods API = new APIMethods(this);
            ulong steamid = MySession.Static.Players.TryGetSteamId(playerid);
            connecting.Remove(steamid);


            if (MyObjectBuilderSerializer.DeserializeXML(target_file_list[steamid], out MyObjectBuilder_Definitions myObjectBuilder_Definitions)) {
                bool failure = false;
                try {
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => {

                        Log.Info($"Importing grid from {target_file_list[steamid]}");

                        var prefabs = myObjectBuilder_Definitions.Prefabs;

                        if (prefabs == null || prefabs.Length != 1) {
                            Log.Error($"Grid has unsupported format!");
                            failure = true;
                            return;
                        }
                        var prefab = prefabs[0];
                        var grids = prefab.CubeGrids;
                        /* Where do we want to paste the grids? Lets find out. */
                        var pos = FindPastePosition(grids, spawn_vector_location);
                        if (pos == null) {
                            Log.Error("No free place.");
                            failure = true;
                            return;
                        }

                        /* Update GridsPosition if that doesnt work get out of here. */
                        if (!UpdateGridsPosition(grids, (Vector3D)pos)) {
                            Log.Error("Failed to find update the grids position");
                            failure = true;
                            return;
                        }

                        /* Remapping to prevent any key problems upon paste. */
                        MyEntities.RemapObjectBuilderCollection(grids);
                        foreach (var grid in grids) {

                            if (MyEntities.CreateFromObjectBuilderAndAdd(grid, true) is MyCubeGrid cubeGrid)
                                FixOwnerAndAuthorShip(cubeGrid, playerid);

                        }
                    });
                    
                    if (!failure) {
                        await API.MarkCompleteAsync(steamid);
                        await API.RemoveConnectionAsync(steamid);
                        if (debug) { Log.Info("Grid has been pulled from the void!"); }
                    }

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
                    Log.Error($"Grid is missing location Information!");
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

        public string currentIP() {
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
                    if (debug) { Log.Warn("Please have your public ip set in the SwitchMe or Torch Config. Search 'Whats my ip?' on google if you are not sure how to find this."); }
                }

            }
            else {
                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            }

            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            return currentIp;
        }

        public async Task Scan() {
            APIMethods API = new APIMethods(this);
            tick++;
            string name = "";
            string location = "";
            string port = "";
            string TargetAlias = string.Empty;

            //Enter jumpgate logic.
            if (Config.EnabledJumpgate && tick % 16 == 0) {
                foreach (var playerOnline in MySession.Static.Players.GetOnlinePlayers()) {
                    var player = utils.GetPlayerByNameOrId(playerOnline.DisplayName);
                    if (player.Character == null) { continue; }

                    /*
                    * Loop over each gate, find how far
                    * away the player is and then add
                    * the name of that gate as they key
                    * and then the distance squared as the value
                    * to a Dictionary type variable
                    */
                    IEnumerable<string> Gates = Config.Gates;
                    Dictionary<string, double> GateDistances = new Dictionary<string, double>();
                    foreach (string gateId in Gates) {
                        name = gateId.Split('/')[0];
                        location = gateId.Split('/')[1];
                        TargetAlias = gateId.Split('/')[3];
                        location = location.TrimStart('{').TrimEnd('}');
                        Vector3D.TryParse(location, out Vector3D gps);
                        GateDistances.Add(name, Vector3D.DistanceSquared(player.GetPosition(), gps));
                    }

                    /*
                    * Sort the gates by double (value not key) ascending
                    * and take the first entry (Meaning the one closest to the player) 
                    * and then get the name of that gate (which is stored as the key).
                    * After that, temporarily store the 'TargetAlias' for use in the next
                    * section of code to start the transfer process
                    */
                    GateDistances = GateDistances.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                    string closest_gate = GateDistances.FirstOrDefault().Key;
                    IEnumerable<string> SpecficGate = Config.Gates.Where(c => c.Split('/')[0].Equals(closest_gate));
                    foreach (string bit in SpecficGate) {
                        TargetAlias = bit.Split('/')[3];
                    }

                    /*
                    * Use the name of the closest gate (relative to current player)
                    * to find the server details of where the gate has been setup to 
                    * direct the players to.
                    */
                    IEnumerable<string> channelIds = Config.Servers.Where(c => c.Split(':')[0].Equals(closest_gate));
                    foreach (string chId in channelIds) {
                        ip = chId.Split(':')[1];
                        name = chId.Split(':')[0];
                        port = chId.Split(':')[2];
                    }
                    string target = ip + ":" + port;


                    /*
                    * Where is the player?
                    */
                    if (debug && tick % 64 == 0) {
                        Log.Info($"{player.DisplayName} is {GateDistances.FirstOrDefault().Value} away (meters squared)");
                    }

                    /*
                    * Check to see if player is outside radius
                    * of outer ring and set their displayedMessage
                    * flag to 'false' if so so that the plugin can
                    * then display a message again when they fall
                    * inside the second ring radius
                    */
                    if (DisplayedMessage.ContainsKey(player.SteamUserId) && GateDistances.FirstOrDefault().Value > (Math.Pow((Config.GateSize * 10), 2))) {
                        DisplayedMessage[player.SteamUserId] = false;
                    }

                    /*
                    * Check to see if the player is within
                    * the warning radius for the gate...
                    * The 'Outer Ring' if you will
                    * (10x the radius of the gate)
                    */
                    if (GateDistances.FirstOrDefault().Value < (Math.Pow((Config.GateSize * 10), 2))) {

                        if (GateDistances.FirstOrDefault().Value > (Math.Pow((Config.GateSize * 4), 2))) {
                            if (JumpProtect.ContainsKey(player.SteamUserId)) {
                                JumpProtect[player.SteamUserId] = false;
                            }
                        }

                        if (!DisplayedMessage[player.SteamUserId]) {
                            utils.NotifyMessage($"You are approaching the Jumpgate for {name}... Proceed with Caution", player.SteamUserId);
                            DisplayedMessage[player.SteamUserId] = true;
                        }

                        /* 
                        * Check to see if the player is Within the bounds
                        * of the jump area, if he is a valid player (online)
                        * and then check to see if he is seated in a controller
                        */
                        if ((GateDistances.FirstOrDefault().Value <= Math.Pow(Config.GateSize, 2)) && player?.Controller.ControlledEntity is MyCockpit controller) {
                            try {
                                if (!inZone[player.SteamUserId]) {
                                    inZone[player.SteamUserId] = true;
                                    if (await API.CheckServer(player, name, target) && (!utils.ReservedDicts.Contains("CheckServer"))) {
                                        VoidManager voidm = new VoidManager(this);
                                        await voidm.SendGrid(controller.Parent.DisplayName, closest_gate, player.DisplayName, player.IdentityId, ip, TargetAlias);
                                    }
                                }
                            }
                            catch (Exception e) {
                                Log.Error(e.ToString());
                            }
                        }
                        else {
                            inZone[player.SteamUserId] = false;
                        }
                    }
                    else {
                        if (DisplayedMessage.ContainsKey(player.SteamUserId)) {
                            DisplayedMessage[player.SteamUserId] = false;
                        }
                    }
                }
            }
        }


        private void SessionChanged(ITorchSession session, TorchSessionState state) {

            if (!Config.Enabled)
                return;

            switch (state) {

                case TorchSessionState.Loaded:
                    //load
                    MyVisualScriptLogicProvider.PlayerConnected += PlayerConnect;
                    OpenGates();
                    Load();
                    break;

                case TorchSessionState.Unloaded:
                    //unload
                    MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnect;
                    timerStart = new DateTime(0);
                    Dispose();
                    break;

                case TorchSessionState.Unloading:
                    CloseGates();
                    break;

                default:
                    // ignore
                    break;
            }
        }


        public void OpenGates() {
            int gates = 0;
            IEnumerable<string> channelIds = Config.Gates;
            string name = "";
            string location = "";
            foreach (string chId in channelIds) {
                name = chId.Split('/')[0];
                location = chId.Split('/')[1].TrimStart('{').TrimEnd('}');
                Vector3D.TryParse(location, out Vector3D gps);
                var ob = new MyObjectBuilder_SafeZone();
                ob.PositionAndOrientation = new MyPositionAndOrientation(gps, Vector3.Forward, Vector3.Up);
                ob.PersistentFlags = MyPersistentEntityFlags2.InScene;
                ob.Shape = MySafeZoneShape.Sphere;
                ob.Radius = (float)50;
                ob.Enabled = true;
                ob.DisplayName = $"SM-{gps}";
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
        }
        public void CloseGates() {
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

        public void Load() {

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
                StartTimer();
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

        private void _timer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                string xml = "";
                string name = "";
                string location = "";
                string alias = "";
                string Inbound = "N";
                Dictionary<string, string> gateData = new Dictionary<string,string>();string targetAlias = "";
                Dictionary<string, Dictionary<string, string>> gate = new Dictionary<string, Dictionary<string, string>>();
                Dictionary<string, Dictionary<string, Dictionary<string, string>>> gates = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                IEnumerable<string> channelIds = Config.Gates;
                try {
                    xml = File.ReadAllText(Path.Combine(StoragePath, "SwitchMe.cfg"));
                }
                catch {
                    xml = File.ReadAllText("SwitchMe.cfg");
                }

                if (timerStart.Ticks == 0) timerStart = e.SignalTime;

                if (Torch.CurrentSession?.State == TorchSessionState.Loaded) {
                    string maxPlayers = MySession.Static.MaxPlayers.ToString();
                    string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();

                    foreach (string chId in channelIds) {
                        name = chId.Split('/')[0];
                        location = chId.Split('/')[1];
                        alias = chId.Split('/')[2];
                        targetAlias = chId.Split('/')[3];                        
                    }

                    if (Torch.CurrentSession != null && currentIP().Length > 1) {

                        if (Config.InboundTransfersState)
                            Inbound = "Y";
                        try {
                            if (!utils.ReservedDicts.Contains("UpdateData")) {

                                utils.ReservedDicts.Add("UpdateData");
                                utils.UpdateData.Add("CURRENTPLAYERS", currentPlayers);
                                utils.UpdateData.Add("MAXPLAYERS", maxPlayers);
                                utils.UpdateData.Add("CURRENTIP", currentIP());
                                utils.UpdateData.Add("VERSION", "2.0.0");
                                utils.UpdateData.Add("BINDKEY", Config.LocalKey);
                                utils.UpdateData.Add("ALLOWINBOUND", Inbound);
                                utils.UpdateData.Add("NAME", Sandbox.MySandboxGame.ConfigDedicated.ServerName);
                                utils.UpdateData.Add("CONFIG", xml);
                                utils.UpdateData.Add("GATEDATA", JsonSerializer.Serialize(channelIds));
                                utils.UpdateData.Add("FUNCTION", "UpdateServerData");
                                utils.SendAPIData(update_debug);
                                utils.ReservedDicts.Remove("UpdateData");
                            }

                        }
                        catch (Exception es) {
                            Log.Error("Data error: " + es.ToString());
                        }
                    }
                }
            } catch(Exception error) {
                Log.Error(error.ToString);
            }
        }
    }
}
