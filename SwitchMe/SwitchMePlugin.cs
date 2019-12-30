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
        private DateTime timerStart = new DateTime(0);
        private TorchSessionManager _sessionManager;
        private IMultiplayerManagerBase _multibase;
        private readonly List<long> player_ids_to_spawn = new List<long>();
        private ulong Steamid;
        private readonly List<IMyPlayer> all_players = new List<IMyPlayer>();
        private readonly Dictionary<long, IMyPlayer> current_player_ids = new Dictionary<long, IMyPlayer>();
        private readonly List<long> clear_ids = new List<long>();
        public bool connecting = false;
        private static Vector3D spawn_vector_location = Vector3D.One;
        private MatrixD spawn_matrix = MatrixD.Identity;
        private int _timerSpawn = 0;
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public Dictionary<long, CurrentCooldown> CurrentCooldownMap { get; } = new Dictionary<long, CurrentCooldown>();
        public Dictionary<long, CurrentCooldown> ConfirmationsMap { get; } = new Dictionary<long, CurrentCooldown>();
        public long Cooldown { get { return Config.CooldownInSeconds * 1000; } }
        public long CooldownConfirmationSeconds { get { return Config.ConfirmationInSeconds; } }
        public long CooldownConfirmation { get { return Config.ConfirmationInSeconds * 1000; } }
        /// <inheritdoc />
        public UserControl GetControl() => _control ?? (_control = new SwitchMeControl(this));
        public void Save() => _config?.Save();

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
            Log.Info( obj.SteamId.ToString() + " connected - Starting SwitchMe handle");
            if (!Config.Enabled) 
                return;
            bool SwitchConnection = await CheckConnection(obj);
            if (!SwitchConnection) {
                connecting = false;
                return;
            }
            Steamid = obj.SteamId;
            connecting = true;
        }
        private void PlayerConnect(long playerId) {
            if (!player_ids_to_spawn.Contains(playerId) && connecting)
                Log.Info("Spawning player");
            player_ids_to_spawn.Add(playerId);
        }
        public override void Update() {

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
                        if (connecting) {
                            spawn_vector_location = (spawn_vector_location * MyUtils.GetRandomFloat(60000f, 100000f));
                            spawn_matrix = MatrixD.CreateWorld(spawn_vector_location);
                            MyVisualScriptLogicProvider.SpawnPlayer(spawn_matrix, Vector3D.Zero, player_id); //Spawn function
                            recovery(player_id, spawn_vector_location);
                            var playerEndpoint = new Endpoint(Steamid, 0);
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
                        }
                        clear_ids.Add(player_id);
                    }
                }
                foreach (var clear_id in clear_ids) {
                    player_ids_to_spawn.Remove(clear_id); //Cleanup
                }
            }
        }

        private async void recovery(long playerid, Vector3D spawn_vector_location) {
            string source = "";
            string filename = "";
            string targetFile = "";
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            using (HttpClient clients = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                        new KeyValuePair<string, string>("steamID", Steamid.ToString()),
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

                }
                catch (Exception error) {
                    Log.Fatal("Unable to download grid: " + error.ToString());
                }
            }
            else {
                Log.Info("Player has no grid in transit");
                targetFile = null;
            }
            if (MyObjectBuilderSerializer.DeserializeXML(targetFile, out MyObjectBuilder_Definitions myObjectBuilder_Definitions)) {

                MyAPIGateway.Utilities.InvokeOnGameThread(() => {

                    Log.Info($"Importing grid from {targetFile}");

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
                    if (!UpdateGridsPosition(grids, (Vector3D)pos))
                        return ;

                    /* Remapping to prevent any key problems upon paste. */
                    MyEntities.RemapObjectBuilderCollection(grids);
                    foreach (var grid in grids) {

                        if (MyEntities.CreateFromObjectBuilderAndAdd(grid, true) is MyCubeGrid cubeGrid)
                            FixOwnerAndAuthorShip(cubeGrid, playerid);
                    }

                });
                Log.Info("Grid has been pulled from the void!");

                return ;
            }

            return;
        }

        private void FixOwnerAndAuthorShip(MyCubeGrid myCubeGrid, long playerId) {

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
            }

            foreach (long author in authors)
                MyMultiplayer.RaiseEvent(myCubeGrid, x => new Action<long, long>(x.TransferBlocksBuiltByID), author, playerId, new EndpointId());
        }
        private Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D position) {
            Vector3D gps = position;
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

            //if (Plugin.Config.LockedTransfer && Plugin.Config.EnabledMirror || Plugin.Config.EnabledMirror)
            //{
            //    Vector3D.TryParse("{X:" + Plugin.Config.XCord + " Y:" + Plugin.Config.YCord + " Z:" + Plugin.Config.ZCord + "}", out Vector3D gps);
            //    return MyEntities.FindFreePlace(gps, 100F);
            //}

            return MyEntities.FindFreePlace(gps, radius);
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

        public async Task<bool> CheckConnection(IPlayer player) {
            Log.Warn("Checking inbound conneciton for " + player.SteamId);
            string pagesource;
            using (HttpClient client = new HttpClient()) {
                List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("BindKey", Config.LocalKey),
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
                connecting = false;
                return false;
            }
            return true ;
        }
        public void Delete(string entityName) {

            var name = entityName;

            if (string.IsNullOrEmpty(name))
                return;

            if (!TryGetEntityByNameOrId(name, out IMyEntity entity)) 
                return;
            
            if (entity is IMyCharacter) 
                return;

            entity.Close();

            Log.Warn("Entitiy deleted.");
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
                    MyVisualScriptLogicProvider.PlayerConnected += PlayerConnect;
                    LoadSEDB();
                    break;

                case TorchSessionState.Unloaded:
                    //unload
                    MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnect;
                    timerStart = new DateTime(0);
                    UnloadSEDB();
                    break;

                default:
                    // ignore
                    break;
            }
        }

        public void StartTimer() {

            if (_timer != null)
                StopTimer();

            _timer = new Timer(3000);

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

        public string Debug() {
            string externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;
            if (Config.LocalKey == "10551Debug") {

                string pagesource;

                try {

                    using (WebClient client = new WebClient()) {

                        NameValueCollection postData = new NameValueCollection()
                        {
                            //order: {"parameter name", "parameter value"}
                            {"key", Config.ActivationKey},
                            {"currentIP", currentIp}
                        };

                        pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/test.php", postData));

                        return pagesource;
                    }

                } catch {
                    Log.Warn("http connection error: Please check you can connect to 'http://switchplugin.net/index.php'");
                }
            }
            return null;
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
        private object myObjectBuilder_Definitions;

        private void InitPost() {
            StartTimer();
        }

        public void DeleteFromWeb(string ip) {

            using (WebClient client = new WebClient()) {

                string pagesource = "";

                NameValueCollection postData = new NameValueCollection()
                {
                    { "posCheck", "processed"},
                    { "currentIP", ip}
                };

                pagesource = Encoding.UTF8.GetString(client.UploadValues("http://switchplugin.net/recovery.php", postData));
            }
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e) {

            string externalIP;
            string Inbound = "N";

            if (Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("0.0") 
                || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("127.0") 
                || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168")) {

                externalIP = Config.LocalIP;

                if (Config.LocalIP == "" || Config.LocalIP == null) {
                    i++;
                    if (i == 600) { Log.Warn("Please have your public ip set in the SwitchMe Config."); i = 0; }
                }

            } else {
                externalIP = Sandbox.MySandboxExternal.ConfigDedicated.IP;
            }

            if (timerStart.Ticks == 0) timerStart = e.SignalTime;

            string maxPlayers = MySession.Static.MaxPlayers.ToString();
            string currentPlayers = MySession.Static.Players.GetOnlinePlayers().Count.ToString();
            string currentIp = externalIP + ":" + Sandbox.MySandboxGame.ConfigDedicated.ServerPort;

            if (Torch.CurrentSession != null && currentIp.Length > 1) {

                if (Config.InboundTransfersState) 
                    Inbound = "Y";
                
                using (WebClient client = new WebClient()) {

                    NameValueCollection postData = new NameValueCollection()
                    {
                        //order: {"parameter name", "parameter value"}
                        { "currentplayers", currentPlayers },
                        { "maxplayers", maxPlayers },
                        { "serverip", currentIp},
                        { "verion", "1.3.1-Test-build-Framework"},
                        { "bindKey", Config.LocalKey},
                        { "inbound", Inbound },
                        { "name", Sandbox.MySandboxGame.ConfigDedicated.ServerName }
                    };

                    client.UploadValues("http://switchplugin.net/index.php", postData);
                }
            }
        }
    }
}
