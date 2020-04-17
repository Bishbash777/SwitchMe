using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Torch;
using Torch.Commands;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;
using VRageMath;

namespace SwitchMe {

    public class GridImporter {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly SwitchMePlugin Plugin;
        private readonly CommandContext Context;

        public GridImporter(SwitchMePlugin Plugin, CommandContext Context) {
            this.Plugin = Plugin;
            this.Context = Context;
        }

        public bool DeserializeGridFromPath(string targetFile, string playername, Vector3D newpos) {
            var player = utils.GetPlayerByNameOrId(playername);
            if (MyObjectBuilderSerializer.DeserializeXML(targetFile, out MyObjectBuilder_Definitions myObjectBuilder_Definitions)) {

                IMyEntity targetEntity = player?.Controller.ControlledEntity.Entity;

                if (Plugin.Config.EnabledMirror) {

                    var p = player;
                    var parent = p.Character?.Parent;

                    if (parent == null) {
                    }

                    if (parent is MyShipController c) {
                        c.RemoveUsers(false);
                    }
                }

                utils.NotifyMessage($"Importing grid from {targetFile}", player.SteamUserId);

                var prefabs = myObjectBuilder_Definitions.Prefabs;

                if (prefabs == null || prefabs.Length != 1) {
                    utils.NotifyMessage($"Grid has unsupported format!", player.SteamUserId);
                    return false;
                }

                var prefab = prefabs[0];
                var grids = prefab.CubeGrids;

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids, newpos, player.DisplayName);
                if (pos == null) {
                    utils.NotifyMessage("No free place.", player.SteamUserId);
                    return false;
                }

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newpos, player.DisplayName)) {
                    Log.Error("Failed to update grid position");
                    return false;
                }

                /* Remapping to prevent any key problems upon paste. */
                MyEntities.RemapObjectBuilderCollection(grids);
                foreach (var grid in grids) {

                    if (MyEntities.CreateFromObjectBuilderAndAdd(grid, true) is MyCubeGrid cubeGrid)
                        FixOwnerAndAuthorShip(cubeGrid, player.DisplayName);
                }
               utils.NotifyMessage("Grid has been pulled from the void!", player.SteamUserId);
                return true;
            }

            return false;
        }

        public bool SerializeGridsToPath(MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup, string gridTarget, string path, string playername) {
            var player = utils.GetPlayerByNameOrId(playername);
            try {

                List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

                foreach (var node in relevantGroup.Nodes) {
                    MyCubeGrid grid = node.NodeData;

                    /* We wanna Skip Projections... always */
                    if (grid.Physics == null)
                        continue;

                    /* What else should it be? LOL? */
                    if (!(grid.GetObjectBuilder(true) is MyObjectBuilder_CubeGrid objectBuilder))
                        throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");
                    objectBuilders.Add(objectBuilder);
                }
                MyObjectBuilder_PrefabDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_PrefabDefinition>();
                definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_PrefabDefinition)), gridTarget);
                definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();
                long i = 0;
                bool BlockCheck = false;
                string SubTypes = Regex.Replace(Plugin.Config.SubTypes, @"\s+", string.Empty);
                string[] SubTypesArray = SubTypes.Split('-');
                /* Reset ownership as it will be different on the new server anyway and chceck to see if any listed blocks are included*/
                foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids) {
                    foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks) {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                        i++;
                        /* Remove Pilot and Components (like Characters) from cockpits */
                        if (cubeBlock is MyObjectBuilder_Cockpit cockpit) {

                            cockpit.Pilot = null;

                            if (cockpit.ComponentContainer != null) {

                                var components = cockpit.ComponentContainer.Components;

                                if (components != null) {

                                    for (int j = components.Count - 1; j >= 0; j--) {

                                        var component = components[j];

                                        if (component.TypeId == "MyHierarchyComponentBase") {
                                            components.RemoveAt(j);
                                            continue;
                                        }
                                    }
                                }
                            }
                        }
                        if (SubTypesArray.Contains(cubeBlock.SubtypeId.ToString())) {
                            BlockCheck = true;
                        }
                    }
                }
                //Block checking;
                if (Plugin.Config.EnableBlockEnforcement && Plugin.Config.BlockAllow && !BlockCheck) {
                   utils.NotifyMessage("You do not have the required block for Switching.", player.SteamUserId);
                    return false;
                }
                if (Plugin.Config.EnableBlockEnforcement && Plugin.Config.BlockDisallow && BlockCheck) {
                    utils.NotifyMessage("You have disallowed blocks on your grid!", player.SteamUserId);
                    return false;
                }
                //Economy stuff
                if (Plugin.Config.EnableEcon && Plugin.Config.PerTransfer && Plugin.Config.PerBlock) {
                    Log.Warn("Invalid econ setup");
                    utils.NotifyMessage("Invalid econ setup - please notify an admin.", player.SteamUserId);
                    return false;
                }
                if (Plugin.Config.EnableEcon) {
                    i = Plugin.Config.TransferCost * i;
                    if (Plugin.Config.PerTransfer) {
                        i = Plugin.Config.TransferCost;
                    }
                    long balance;
                    long withdraw = i;
                    player.TryGetBalanceInfo(out balance);
                    long mathResult = (balance - withdraw);
                    Log.Info("Cost of transfer for" + player.DisplayName + ": " + i);
                    CurrentCooldown cooldown = new CurrentCooldown(Plugin);
                    //verify that user wants to go ahead with transfer.
                    if (cooldown.Confirm(i, player.SteamUserId)) {
                        if (mathResult < 0) {
                            Log.Info("Cost of transfer for" + player.DisplayName + ": " + i);
                            utils.NotifyMessage("Not enough funds for transfer", player.SteamUserId);
                            return false;
                        }
                        player.RequestChangeBalance(-withdraw);
                    }
                }
                MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
                builderDefinition.Prefabs = new MyObjectBuilder_PrefabDefinition[] { definition };
                bool worked = MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);
                Log.Debug("exported " + path + " " + worked);
                return true;

            } catch (Exception e) {
                Log.Fatal(e.Message);
                return false;
            }
        }

        private void FixOwnerAndAuthorShip(MyCubeGrid myCubeGrid, string playername) {

            HashSet<long> authors = new HashSet<long>();
            HashSet<MySlimBlock> blocks = new HashSet<MySlimBlock>(myCubeGrid.GetBlocks());
            var player = utils.GetPlayerByNameOrId(playername);
            MyPlayer id = MySession.Static.Players.TryGetPlayerBySteamId(player.SteamUserId);


            foreach (MySlimBlock block in blocks) {

                if (block == null || block.CubeGrid == null || block.IsDestroyed)
                    continue;

                MyCubeBlock cubeBlock = block.FatBlock;
                if (cubeBlock != null && cubeBlock.OwnerId != player.Identity.IdentityId) {

                    myCubeGrid.ChangeOwnerRequest(myCubeGrid, cubeBlock, 0, MyOwnershipShareModeEnum.Faction);

                    if (player.IdentityId != 0)
                        myCubeGrid.ChangeOwnerRequest(myCubeGrid, cubeBlock, player.IdentityId, MyOwnershipShareModeEnum.Faction);
                }
                if (block.BuiltBy == 0) {
                    /* 
                    * Hack: TransferBlocksBuiltByID only transfers authorship if it has an author. 
                    * Transfer Authorship Client just sets the author so we need to take care of limits ourselves. 
                    */
                    block.TransferAuthorshipClient(player.IdentityId);
                    block.AddAuthorship();
                }
                authors.Add(block.BuiltBy);

                IMyCharacter character = player.Character;

                if (cubeBlock is Sandbox.ModAPI.IMyCockpit cockpit && cockpit.CanControlShip)
                    cockpit.AttachPilot(character);
            }

            foreach (long author in authors)
                MyMultiplayer.RaiseEvent(myCubeGrid, x => new Action<long, long>(x.TransferBlocksBuiltByID), author, player.IdentityId, new EndpointId());
        }

        private Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D pos, string playername) {
            var player = utils.GetPlayerByNameOrId(playername);
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

            if (Plugin.Config.LockedTransfer && Plugin.Config.EnabledMirror || Plugin.Config.EnabledMirror)
            {
                return MyEntities.FindFreePlace(pos, radius);
            }

            return MyEntities.FindFreePlace(player.GetPosition(), radius);
        }

        public bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition, string playername) {
            var player = utils.GetPlayerByNameOrId(playername);
            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            foreach (var grid in grids) {

                var position = grid.PositionAndOrientation;

                if (position == null) {
                    utils.NotifyMessage($"Grid is missing location Information!", player.SteamUserId);
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

                } else {

                    currentPosition.X += deltaX;
                    currentPosition.Y += deltaY;
                    currentPosition.Z += deltaZ;
                }

                realPosition.Position = currentPosition;
                grid.PositionAndOrientation = realPosition;
            }

            return true;
        }
    }
}
