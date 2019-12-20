﻿using NLog;
using Sandbox;
using Sandbox.Game.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage.Game.ModAPI;
using VRage.Groups;

namespace SwitchMe {

    public class Utilities {

        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static string CreateExternalIP(SwitchMeConfig Config) {

            if (MySandboxGame.ConfigDedicated.IP.Contains("0.0") || MySandboxGame.ConfigDedicated.IP.Contains("127.0") || Sandbox.MySandboxExternal.ConfigDedicated.IP.Contains("192.168"))
                return Config.LocalIP;

            return MySandboxGame.ConfigDedicated.IP;
        }

        public static MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group FindRelevantGroup(
            string gridTarget, long playerId, CommandContext context) {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = FindGridGroup(gridTarget, context);

            Log.Warn("Target and ID:   " + gridTarget + " | " + playerId);

            try {

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

                        string pos = grid.PositionComp.GetPosition().ToString();

                        /* If nobody isnt the big owner then everythings fine. otherwise take the second biggest owner */
                        if (ownerCount > 0 && bigOnwerIds[0] != 0)
                            gridOwner = bigOnwerIds[0];
                        else if (ownerCount > 1)
                            gridOwner = bigOnwerIds[1];

                        /* Is the player ID the biggest owner? */
                        if (gridOwner == playerId) {

                            Log.Fatal("checking was completed");
                            groupFound = true;
                            break;

                        } else if (gridOwner == 0) {

                            groupFound = true;

                        } else {

                            context.Respond("You are not the grid Owner");
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

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName, CommandContext context) {

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
                context.Respond("No grid found...");

            return groups;
        }
    }
}