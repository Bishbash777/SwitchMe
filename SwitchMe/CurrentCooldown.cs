﻿using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage.Game.ModAPI;

namespace SwitchMe {

    public class CurrentCooldown {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly SwitchMePlugin Plugin;
        private readonly CommandContext Context;

        public CurrentCooldown(SwitchMePlugin Plugin) {
            this.Plugin = Plugin;
        }

        private long _startTime;
        private readonly long _currentCooldownCommand;

        private string command;

        public CurrentCooldown(long cooldown) {
            _currentCooldownCommand = cooldown;
        }

        public void StartCooldown(string command) {
            this.command = command;
            _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public long GetRemainingSeconds(string command) {

            if (this.command != command)
                return 0;

            long elapsedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _startTime;

            if (elapsedTime >= _currentCooldownCommand)
                return 0;

            return (_currentCooldownCommand - elapsedTime) / 1000;
        }


        public bool CheckConformation(long cost, ulong playerId, string playerName) {

            var confirmationCooldownMap = Plugin.ConfirmationsMapCommand;

            if (confirmationCooldownMap.TryGetValue(playerId, out CurrentCooldown confirmationCooldown)) {

                long remainingSeconds = confirmationCooldown.GetRemainingSeconds(playerName);

                if (remainingSeconds > 0) {
                    confirmationCooldownMap.Remove(playerId);
                    return true;
                }
            }
            confirmationCooldown = CreateNewCooldown(confirmationCooldownMap, playerId, Plugin.CooldownConfirmation);

            Context.Respond("This action will cost " + cost + " Space credits. Are you sure you want to continue? Enter the command again within " + Plugin.CooldownConfirmationSeconds + " seconds to confirm.");
            confirmationCooldown.StartCooldown(playerName);

            return false;
        }

        public static CurrentCooldown CreateNewCooldown(Dictionary<ulong, CurrentCooldown> cooldownMap, ulong playerId, long cooldown) {

            var currentCooldown = new CurrentCooldown(cooldown);

            if (cooldownMap.ContainsKey(playerId))
                cooldownMap[playerId] = currentCooldown;
            else
                cooldownMap.Add(playerId, currentCooldown);

            return currentCooldown;
        }


        public static CurrentCooldown CreateNewProtection(Dictionary<ulong, CurrentCooldown> cooldownMap, ulong steamid, long cooldown) {

            var currentCooldown = new CurrentCooldown(cooldown);

            if (cooldownMap.ContainsKey(steamid))
                cooldownMap[steamid] = currentCooldown;
            else
                cooldownMap.Add(steamid, currentCooldown);

            return currentCooldown;
        }


        public bool Confirm(long cost, ulong steamid) {

            IMyPlayer player = Context.Player;

            long playerId;

            if (player == null) {

                Context.Respond("Console cannot use this command!");
                return false;

            }
            else {
                playerId = player.IdentityId;
            }

            IMyCharacter character = player.Character;

            if (character == null) {
                Context.Respond("You have no Character currently. Make sure to spawn and be out of cockpit!");
                return false;
            }

            var currentCooldownMap = Plugin.CurrentCooldownMapCommand;

            if (currentCooldownMap.TryGetValue(steamid, out CurrentCooldown currentCooldown)) {

                long remainingSeconds = currentCooldown.GetRemainingSeconds(null);

                if (remainingSeconds > 0) {
                    Log.Info("Cooldown for Player " + player.DisplayName + " still running! " + remainingSeconds + " seconds remaining!");
                    Context.Respond("Command is still on cooldown for " + remainingSeconds + " seconds.");
                    return false;
                }

                currentCooldown = CreateNewCooldown(currentCooldownMap, steamid, Plugin.Cooldown);

            }
            else {

                currentCooldown = CreateNewCooldown(currentCooldownMap, steamid, Plugin.Cooldown);
            }

            if (!CheckConformation(cost, steamid, Context.Player.DisplayName))
                return false;

            try {

                Log.Info("Cooldown for Player " + player.DisplayName + " started!");
                currentCooldown.StartCooldown(null);
                return true;
            }
            catch (Exception e) {
                Log.Error(e);
                return false;
            }
        }



        public bool Protect(ulong steamid) {

            IMyPlayer player = Context.Player;

            var currentCooldownMap = Plugin.ProtectionCooldownMap;

            if (currentCooldownMap.TryGetValue(steamid, out CurrentCooldown currentCooldown)) {

                long remainingSeconds = currentCooldown.GetRemainingSeconds(null);

                if (remainingSeconds > 0) {
                    Log.Info("Cooldown for Player " + player.DisplayName + " still running! " + remainingSeconds + " seconds remaining!");
                    return false;
                }

                currentCooldown = CreateNewCooldown(currentCooldownMap, steamid, Plugin.Cooldown);

            }
            else {

                currentCooldown = CreateNewCooldown(currentCooldownMap, steamid, Plugin.Cooldown);
            }

            if (!CheckProtection(steamid, Context.Player.DisplayName))
                return false;

            try {

                Log.Info("Cooldown for Player " + player.DisplayName + " started!");
                currentCooldown.StartCooldown(null);
                return true;
            }
            catch (Exception e) {
                Log.Error(e);
                return false;
            }
        }



        public bool protect(ulong steamid) {

            IMyPlayer player = Context.Player;

            long playerId;

            if (player == null) {

                Context.Respond("Console cannot use this command!");
                return false;

            }
            else {
                playerId = player.IdentityId;
            }

            IMyCharacter character = player.Character;

            var currentCooldownMap = Plugin.ProtectionCooldownMap;

            if (currentCooldownMap.TryGetValue(steamid, out CurrentCooldown currentCooldown)) {

                long remainingSeconds = currentCooldown.GetRemainingSeconds(null);

                if (remainingSeconds > 0) {
                    Log.Warn($"Protected {player.DisplayName} From losing their grid!");
                    return false;
                }

                currentCooldown = CreateNewCooldown(currentCooldownMap, steamid, Plugin.Cooldown);

            }
            else {

                currentCooldown = CreateNewProtection(currentCooldownMap, steamid, Plugin.Cooldown);
            }

            if (!CheckProtection(steamid, Context.Player.DisplayName))
                return false;

            try {

                Log.Info("Cooldown for Player " + player.DisplayName + " started!");
                currentCooldown.StartCooldown(null);
                return true;
            }
            catch (Exception e) {
                Log.Error(e);
                return false;
            }
        }

        public bool CheckProtection(ulong steamid, string playerName) {

            var confirmationCooldownMap = Plugin.ProtectionCooldownMap;

            if (confirmationCooldownMap.TryGetValue(steamid, out CurrentCooldown confirmationCooldown)) {

                long remainingSeconds = confirmationCooldown.GetRemainingSeconds(playerName);

                if (remainingSeconds > 0) {
                    confirmationCooldownMap.Remove(steamid);
                    return true;
                }
            }
            confirmationCooldown = CreateNewProtection(confirmationCooldownMap, steamid, Plugin.CooldownConfirmation);

            //Context.Respond("This action will cost " + cost + " Space credits. Are you sure you want to continue? Enter the command again within " + Plugin.CooldownConfirmationSeconds + " seconds to confirm.");
            confirmationCooldown.StartCooldown(playerName);

            return false;
        }

    }
}