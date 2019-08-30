using NLog;
using System;
using System.Collections.Generic;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using Torch.Mod;
using Torch.Mod.Messages;
using System.Linq;
using System.Text;
using Sandbox.Game.World;
using System.Collections.Specialized;
using System.Net;


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
            
            if (Plugin.Config.Enabled)
            {
                IEnumerable<string> channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));
                foreach (string chId in channelIds)
                {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];

                }
                string target = ip + ":" + port;
                string slotinfo = Plugin.CheckSlots(target);
                int current = 0;
                int max = 0;
                current = int.Parse(slotinfo.Substring(slotinfo.IndexOf(",") + 1));
                max = int.Parse(slotinfo.Substring(slotinfo.IndexOf(",") + 2));
                int currentPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                int maxcheck = currentPlayers + 1;

                if (maxcheck > max)
                {

                    if (ip == null)
                    {
                        Context.Respond("Invalid Server!");
                    }
                    if (Context.Player != null)
                    {

                        ulong steamid = Context.Player.SteamUserId;
                        try
                        {
                            Context.Respond("Connecting to " + Context.RawArgs + " @ " + ip + ":" + port);
                            ModCommunication.SendMessageTo(new JoinServerMessage(ip), steamid);
                            Log.Warn(Context.Player.DisplayName + " has switched to " + Context.RawArgs + " @ " + ip + ":" + port);
                        }
                        catch (Exception e)
                        {
                            Context.Respond(e.Message);
                        }
                    }
                    else
                    {
                        Context.Respond("Command can only be ran ingame!");
                    }
                }
                else
                {
                    Context.Respond("Cannot Switch, target server is full!");
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
            string ip = "";
            string name = "";
            string port = "";
            if (Plugin.Config.Enabled)
            {
                IEnumerable<string> channelIds = Plugin.Config.Servers.Where(c => c.Split(':')[0].Equals(Context.RawArgs));
                foreach (string chId in channelIds)
                {
                    ip = chId.Split(':')[1];
                    name = chId.Split(':')[0];
                    port = chId.Split(':')[2];

                }
                string target = ip + ":" + port;
                string slotinfo = Plugin.CheckSlots(target);
                int current = 0;
                int max = 0;
                current = int.Parse(slotinfo.Substring(slotinfo.IndexOf(",") + 1));
                max = int.Parse(slotinfo.Substring(slotinfo.IndexOf(",") + 2));
                int currentPlayers = int.Parse(MySession.Static.Players.GetOnlinePlayers().Count.ToString());
                int maxcheck = currentPlayers + 1;

                if (maxcheck > max)
                {
                    if (ip == null || name == null || port == null)
                    {
                        Context.Respond("Invalid Configuration!");
                    }
                    try
                    {
                        Context.Respond("Connecting clients to " + Context.RawArgs + " @ " + ip + ":" + port);
                        ModCommunication.SendMessageToClients(new JoinServerMessage(ip));
                        Log.Warn("Connected clients to " + Context.RawArgs + " @ " + ip + ":" + port);
                    }
                    catch
                    {
                        Context.Respond("Failure");
                    }
                }
                else
                {
                    Context.Respond("Switching is not enabled!");
                }
            }
        }
        [Command("list", "Displays a list of Valid Server names for the '!switch me <servername>' command. ")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchList()
        {
            if (Plugin.Config.Enabled)
            {
                StringBuilder sb = new StringBuilder();
                string name = "";

                IEnumerable<string> channelIds = Plugin.Config.Servers;
                foreach (string chId in channelIds)
                {
                    name = chId.Split(':')[0];
                    sb.AppendLine("'" + name + "'");
                }
                Context.Respond("");
                Context.Respond("----------------");
                Context.Respond("List of Servers available to switch to:");
                Context.Respond(sb.ToString());
                Context.Respond("----------------");
            }
            else
            {
                Context.Respond("Switching is not enabled!");
            }
        }

        [Command("help", "Displays a list of Valid Server names for !switch Me <servername> ")]
        [Permission(MyPromoteLevel.None)]
        public void SwitchHelp()
        {
            Context.Respond("'!switch Me <servername>' Switches you to selected server");
            Context.Respond("'!switch List' Displays a list of valid Server names to connect to.");
        }
       
    }
}

