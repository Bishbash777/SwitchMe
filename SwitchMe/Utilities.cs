﻿using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Mod;
using System.Web;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.ModAPI;
using VRage.Groups;
using VRageMath;
using Torch;
using Torch.API;
using System.Net;
using System.Collections.Specialized;
using VRage.Network;
using Sandbox.Engine.Multiplayer;
using Torch.Utils;
using VRage.Collections;
using VRage.Replication;
using System.Collections;
using VRage.ModAPI;

namespace SwitchMe {

    public class utils {
        public static string API_URL = "http://switchplugin.net/api2/";
        public static ITorchBase Torch { get; }
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Dictionary<string, string> webdata = new Dictionary<string, string>();
        public static Dictionary<string, string> UpdateData = new Dictionary<string, string>();
        public static List<string> ReservedDicts = new List<string>();
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
        public static string CreateExternalIP(SwitchMeConfig Config) {

            if (MySandboxGame.ConfigDedicated.IP.Contains("0.0") || MySandboxGame.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
                return Config.LocalIP;

            return MySandboxGame.ConfigDedicated.IP;
        }

        public static string GetSubstringByString(string from, string until, string wholestring) {
            return wholestring.Substring((wholestring.IndexOf(from) + from.Length), (wholestring.IndexOf(until) - wholestring.IndexOf(from) - from.Length));
        }

        public static Dictionary<string, string> ParseQueryString(string queryString) {
            var nvc = HttpUtility.ParseQueryString(queryString);
            return nvc.AllKeys.ToDictionary(k => k, k => nvc[k]);
        }

        public static string SelectRandomGate(Dictionary<string, string> dictionary) {
            Random rand = new Random();
            var k = dictionary.Keys.ToList()[rand.Next(dictionary.Count)];
            return dictionary[k];
        }

        public static MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group FindRelevantGroup(
            string gridTarget, long playerId) {
            try {
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = FindGridGroup(gridTarget);

                Log.Warn("Target and ID:   " + gridTarget + " | " + playerId);
                /* Each Physical Grid group (physical included Connectors) */
                foreach (var group in groups) {

                    bool groupFound = false;

                    /* Check each grid */
                    foreach (var node in group.Nodes) {

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
                        if (gridOwner == playerId) {
                            groupFound = true;
                            break;

                        } else if (gridOwner == 0) {

                            groupFound = true;

                        } else {

                            //context.Respond("You are not the grid Owner");
                            Log.Warn("Conditionals... GridOwner: " + gridOwner + "Player: " + playerId);
                        }
                    }

                    if (groupFound)
                        return group;
                }

                return null;

            } catch (Exception e) {
                Log.Fatal("Error at groupfinder: " + e.ToString());
                return null;
            }
        }


        public static void NotifyMessage(string message, ulong steamid, string colour = "Blue") {
            ModCommunication.SendMessageTo(new NotificationMessage(message, 15000, colour), steamid);
        }


        public static void RefreshPlayer(ulong steamid) {
            MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                var playerEndpoint = new Endpoint(steamid, 0);
                var replicationServer = (MyReplicationServer)MyMultiplayer.ReplicationLayer;
                var clientDataDict = _clientStates.Invoke(replicationServer);
                object clientData;
                try {
                    clientData = clientDataDict[playerEndpoint];
                }
                catch { return; }
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

        public static void Respond(string message, string sender = null, ulong steamid = 0, string font = null) {
            if (sender == "Server") {
                sender = null;
                font = null;
            }
            IChatManagerServer manager = Torch.CurrentSession.Managers.GetManager<IChatManagerServer>();
            if (manager == null) {
                return;
            }
            manager.SendMessageAsOther(sender, message, default, steamid, "White");
        }

        public static void Delete(string entityName) {
            try {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => {
                    var name = entityName;

                    if (string.IsNullOrEmpty(name))
                        return;

                    if (!utils.TryGetEntityByNameOrId(name, out IMyEntity entity))
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

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName) {

            int i = 0;
            bool foundIdentifier = false;

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();

            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group => {

                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes) {

                    IMyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.CustomName.Equals(gridName)) {
                        i++;
                        continue;
                    }

                    groups.Add(group);
                    foundIdentifier = true;
                }
            });

            if (i >= 1 && !foundIdentifier)
                Log.Info("No grid found...");

            return groups;
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

        public static async Task<Dictionary<string,string>> SendAPIRequestAsync(bool debug) {
            HttpResponseMessage response = null;
            string function = string.Empty;
            try {
                using (HttpClient client = new HttpClient()) {

                    List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();

                    foreach (var kvp in webdata) {
                        if (kvp.Key == "FUNCTION") { function = kvp.Value; }
                        pairs.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value));
                    }

                    FormUrlEncodedContent content = new FormUrlEncodedContent(pairs);
                    response = await client.PostAsync(API_URL, content);
                    
                    var api_response = ParseQueryString(await response.Content.ReadAsStringAsync());
                    webdata.Clear();
                    if (debug) {
                        Log.Warn($"{function} response data:");
                        foreach (var kvp in api_response) {
                            Log.Warn($"{kvp.Key}={kvp.Value}");
                        }
                    }
                    return api_response;
                }
            } catch(Exception e) {
                Log.Fatal(e.ToString());
                if (debug) { Log.Warn(response); }
                return null;
            }
        }

        public static async void SendAPIData(bool UpdateDebug = false) {
            try {
                using (HttpClient client = new HttpClient()) {
                    List<KeyValuePair<string, string>> postData = new List<KeyValuePair<string, string>>();
                    foreach (var kvp in UpdateData) {
                        postData.Add(new KeyValuePair<string, string> (kvp.Key, kvp.Value));
                    }
                    FormUrlEncodedContent content = new FormUrlEncodedContent(postData);
                    HttpResponseMessage response = await client.PostAsync(API_URL, content);
                    if (UpdateDebug) { Log.Warn(await response.Content.ReadAsStringAsync()); }
                    UpdateData.Clear();
                }
            }
            catch (Exception e) {
                Log.Fatal(e.ToString());
            }
        }

    }
}
