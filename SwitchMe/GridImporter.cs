using NLog;
using Sandbox;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Collections.Generic;
using System.Linq;
using Torch.Commands;
using VRage.Game;
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

        public bool DeserializeGridFromPath(string targetFile, long playerId, Vector3D newpos) {

            if (MyObjectBuilderSerializer.DeserializeXML(targetFile, out MyObjectBuilder_Definitions myObjectBuilder_Definitions)) {

                IMyEntity targetEntity = Context.Player?.Controller.ControlledEntity.Entity;

                if (Plugin.Config.EnabledMirror) {

                    var p = Context.Player;
                    var parent = p.Character?.Parent;

                    if (parent == null) {
                    }

                    if (parent is MyShipController c) {
                        c.RemoveUsers(false);
                    }
                }

                Context.Respond($"Importing grid from {targetFile}");

                var prefabs = myObjectBuilder_Definitions.Prefabs;

                if (prefabs == null || prefabs.Length != 1) {
                    Context.Respond($"Grid has unsupported format!");
                    return false;
                }

                var prefab = prefabs[0];
                var grids = prefab.CubeGrids;

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids);
                if (pos == null) {
                    Context.Respond("No free place.");
                    return false;
                }

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newpos))
                    return false;

                /* Remapping to prevent any key problems upon paste. */
                MyEntities.RemapObjectBuilderCollection(grids);

                foreach (var grid in grids) {

                    if (MyEntities.CreateFromObjectBuilderAndAdd(grid, true) is MyCubeGrid cubeGrid)
                        FixOwnerAndAuthorShip(cubeGrid, playerId);
                }

                if (Plugin.Config.EnabledMirror || Plugin.Config.LockedTransfer) {

                    targetEntity.SetPosition(newpos);
                    Context.Respond("***Transporting***");
                }

                Context.Respond("Grid has been pulled from the void!");

                return true;
            }

            return false;
        }

        public void SerializeGridsToPath(MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group relevantGroup, string gridTarget, string path) {

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

                /* Reset ownership as it will be different on the new server anyway */
                foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids) {
                    foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks) {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                    }
                }

                MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
                builderDefinition.Prefabs = new MyObjectBuilder_PrefabDefinition[] { definition };

                bool worked = MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);

                Log.Fatal("exported " + path + " " + worked);

            } catch (Exception e) {
                Log.Fatal(e, "ERROR AT SERIALIZATION: " + e.Message);
            }
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

        private Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids) {

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

            return MyEntities.FindFreePlace(Context.Player.GetPosition(), 50F);
        }

        private bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition) {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            foreach (var grid in grids) {

                var position = grid.PositionAndOrientation;

                if (position == null) {
                    Context.Respond($"Grid is missing location Information!");
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
