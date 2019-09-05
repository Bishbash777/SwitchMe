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

                        if (target.Length > 1)
                        {
                            if (existanceCheck == "1")
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
                        //existanceCheck = int.Parse(slotinfo.Substring(0, slotinfo.LastIndexOf(";")));

                        if (target.Length > 1)
                        {
                            if (existanceCheck == "1")
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
                    if (target.Length > 1)
                    {
                        if (existanceCheck == "1")
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
                    if (target.Length > 1)
                    {
                        if (existanceCheck == "1")
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
                    sb.Append("'" + name + "' ");

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
            Context.Respond("'!switch me <servername>' Switches you to selected server");
            Context.Respond("'!switch list' Displays a list of valid Server names to connect to.");
        }
    }
}

