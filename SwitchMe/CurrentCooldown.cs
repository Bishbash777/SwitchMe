using NLog;
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

        public CurrentCooldown(SwitchMePlugin Plugin, CommandContext Context) {
            this.Plugin = Plugin;
            this.Context = Context;
        }

        private long _startTime;
        private readonly long _currentCooldown;

        private string command;

        public CurrentCooldown(long cooldown) {
            _currentCooldown = cooldown;
        }

        public void StartCooldown(string command) {
            this.command = command;
            _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public long GetRemainingSeconds(string command) {

            if (this.command != command)
                return 0;

            long elapsedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _startTime;

            if (elapsedTime >= _currentCooldown)
                return 0;

            return (_currentCooldown - elapsedTime) / 1000;
        }


        public bool CheckConformation(long cost, long executingPlayerId, long playerId, string playerName, IMyCharacter character) {

            var confirmationCooldownMap = Plugin.ConfirmationsMap;

            if (confirmationCooldownMap.TryGetValue(executingPlayerId, out CurrentCooldown confirmationCooldown)) {

                long remainingSeconds = confirmationCooldown.GetRemainingSeconds(playerName);

                if (remainingSeconds > 0) {
                    confirmationCooldownMap.Remove(executingPlayerId);
                    return true;
                }
            }
            confirmationCooldown = CreateNewCooldown(confirmationCooldownMap, executingPlayerId, Plugin.CooldownConfirmation);

            Context.Respond("This action will cost " + cost + "Space credits. Are you sure you want to continue? Enter the command again within " + Plugin.CooldownConfirmationSeconds + " seconds to confirm.");
            confirmationCooldown.StartCooldown(playerName);

            return false;
        }

        public static CurrentCooldown CreateNewCooldown(Dictionary<long, CurrentCooldown> cooldownMap, long playerId, long cooldown) {

            var currentCooldown = new CurrentCooldown(cooldown);

            if (cooldownMap.ContainsKey(playerId))
                cooldownMap[playerId] = currentCooldown;
            else
                cooldownMap.Add(playerId, currentCooldown);

            return currentCooldown;
        }

        public bool Confirm(long cost) {

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

            var currentCooldownMap = Plugin.CurrentCooldownMap;

            if (currentCooldownMap.TryGetValue(playerId, out CurrentCooldown currentCooldown)) {

                long remainingSeconds = currentCooldown.GetRemainingSeconds(null);

                if (remainingSeconds > 0) {
                    Log.Info("Cooldown for Player " + player.DisplayName + " still running! " + remainingSeconds + " seconds remaining!");
                    Context.Respond("Command is still on cooldown for " + remainingSeconds + " seconds.");
                    return false;
                }

                currentCooldown = CreateNewCooldown(currentCooldownMap, playerId, Plugin.Cooldown);

            }
            else {

                currentCooldown = CreateNewCooldown(currentCooldownMap, playerId, Plugin.Cooldown);
            }

            if (!CheckConformation(cost, playerId, playerId, Context.Player.DisplayName, character))
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
    }
}