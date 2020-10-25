using NLog;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using Torch.API;


namespace SwitchMe {

    public class APIMethods {
        public static string API_URL = "http://switchplugin.net/api2/";
        public static ITorchBase Torch { get; }
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly SwitchMePlugin Plugin;
        public APIMethods(SwitchMePlugin Plugin) {
            this.Plugin = Plugin;
        }


        public async Task<bool> CheckExistance(string targetIP) {
            utils.webdata.Add("TARGETIP", targetIP);
            var api_reponse = await utils.SendAPIRequestAsync(Plugin.debug);
            return api_reponse["responseCode"] == "0";
        }

        public async Task MarkCompleteAsync(ulong steamid) {
            utils.webdata.Add("PROCESSED", steamid.ToString());
            utils.webdata.Add("STEAMID", steamid.ToString());
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("FUNCTION", "MarkCompleteAsync");
            await utils.SendAPIRequestAsync(Plugin.debug);
        }

        public async Task<string> GetGateAsync(string steamid) {
            utils.webdata.Add("STEAMID", steamid);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
            utils.webdata.Add("FUNCTION", "GetGateAsync");
            var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
            return api_response["gate"];
        }

        public async Task<bool> CheckConnectionAsync(IPlayer player) {
            Log.Warn("Checking inbound conneciton for " + player.SteamId);
            utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("STEAMID", player.SteamId.ToString());
            utils.webdata.Add("FUNCTION", "CheckConnectionAsync");

            var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
            return bool.Parse(api_response["connecting"]);
        }

        public async Task AddConnectionAsync(ulong SteamUserId,string ip,string targetAlias) {
            utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("TARGETIP", ip);
            utils.webdata.Add("TARGETALIAS", targetAlias);
            utils.webdata.Add("STEAMID", SteamUserId.ToString());
            utils.webdata.Add("FUNCION", "AddConnectionAsync");
            await utils.SendAPIRequestAsync(Plugin.debug);
        }
        public async Task AddTransferAsync(string steamid, string ip, string filename, string targetpos, string grid_target) {
            utils.webdata.Add("STEAMID", steamid);
            utils.webdata.Add("TARGETIP", ip);
            utils.webdata.Add("FILENAME", steamid + "-" + grid_target);
            utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
            utils.webdata.Add("TARGETPOS", targetpos);
            utils.webdata.Add("GRIDNAME", grid_target);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("FUNCTION", "AddTransferAsync");
            await utils.SendAPIRequestAsync(Plugin.debug);
        }

        public async Task<bool> CheckKeyAsync(string target) {
            try {

                utils.webdata.Add("TARGETIP", target);
                utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
                utils.webdata.Add("FUNCTION", "CheckKeyAsync");
                var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
                return Plugin.Config.BindingKey == api_response["key"];

            }
            catch (Exception e) {
                return false;
            }
        }

        public async Task<string> CheckSlotsAsync(string targetIP, string NumberOfPlayers) {
            utils.webdata.Add("TARGETIP", targetIP);
            utils.webdata.Add("FUNCTION", "CheckSlotsAsync");
            utils.webdata.Add("PLAYERCOUNT", NumberOfPlayers);

            var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
            return api_response["available"];
        }

        public async Task<bool> CheckInboundAsync(string target) {
            try {
                utils.webdata.Add("TARGETIP", target);
                utils.webdata.Add("FUNCTION", "CheckInboundAsync");
                var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
                return api_response["allow"] == "Y";

            }
            catch (Exception e) {
                Log.Warn("Error: " + e.ToString());
                return false;
            }
        }

        public async Task<bool> CheckStatusAsync(string target) {
            try {
                utils.webdata.Add("TARGETIP", target);
                utils.webdata.Add("FUNCTION", "CheckStatusAsync");
                var api_response = await utils.SendAPIRequestAsync(Plugin.debug);
                return bool.Parse(api_response["online"]);

            }
            catch {
                Log.Warn($"http connection error: Please check you can connect to '{API_URL}'");
            }
            return false;
        }

        public void AttemptHWIDLink() {
            if (!utils.ReservedDicts.Contains("HWIDData")) {
                utils.ReservedDicts.Add("HWIDData");
                utils.HWIDData.Add("FUNCTION", "AttemptHWIDLink");
                utils.HWIDData.Add("HWID", utils.GetMachineId());
                utils.HWIDData.Add("CURRENTIP", Plugin.currentIP());
                utils.SendHWIDData(false);
                utils.ReservedDicts.Remove("HWIDData");
            }
        }

        public async Task RemoveConnectionAsync(ulong player) {
            Log.Warn("Removing conneciton flag for " + player);
            utils.webdata.Add("BINDKEY", Plugin.Config.BindingKey);
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("STEAMID", player.ToString());
            utils.webdata.Add("FUNCTION", "RemoveConnectionAsync");
            await utils.SendAPIRequestAsync(Plugin.debug);
        }

        public async Task<Dictionary<string, string>> LoadOnlineConfig() {
            utils.webdata.Add("FUNCTION", "DownloadConfigAsync");
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("HWID", utils.GetMachineId());
            var api_reponse = await utils.SendAPIRequestAsync(Plugin.debug);
            return api_reponse;
        }

        public async Task<Dictionary<string,string>> FindWebGridAsync(ulong steamid) {
            utils.webdata.Add("STEAMID", steamid.ToString());
            utils.webdata.Add("CURRENTIP", Plugin.currentIP());
            utils.webdata.Add("FUNCTION", "FindWebGridAsync");
            return await utils.SendAPIRequestAsync(Plugin.debug);
        }

        public async Task<bool> CheckServer(IMyPlayer player, string servername, string target) {
            utils.ReservedDicts.Add("CheckServer");

            if (target.Length < 1) {
                Log.Warn("Unknown Server. Please use '!switch list' to see a list of valid servers!");
                utils.NotifyMessage("Unknown Server. Please use '!switch list' to see a list of valid servers!", player.SteamUserId);
                return false;
            }

            /*if (!await CheckExistance(target)) {
                Log.Warn("Cannot communicate with target, please make sure SwitchMe is installed there!");
                utils.NotifyMessage("Cannot communicate with target, please make sure SwitchMe is installed there!", player.SteamUserId);
                return false;
            }*/

            if (!await CheckKeyAsync(target)) {
                Log.Warn("Unauthorised Switch! Please make sure the servers have the same Bind Key!");
                utils.NotifyMessage("Unauthorised Switch! Please make sure the servers have the same Bind Key!", player.SteamUserId, "Red");
                return false;
            }

            ///   Slot checking
            bool slotsAvailable = bool.Parse(await CheckSlotsAsync(target, "1"));
            if (!slotsAvailable && player.PromoteLevel != MyPromoteLevel.Admin) {
                utils.NotifyMessage("Not enough slots free to use gate!", player.SteamUserId);
                return false;
            }
            utils.ReservedDicts.Remove("CheckServer");
            return true;
        }

    }
}
