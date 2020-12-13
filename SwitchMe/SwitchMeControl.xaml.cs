using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Text.RegularExpressions;
using Torch;

using VRage.Game;
using System.Windows.Input;
using System.Net;
using System.IO;

namespace SwitchMe {

    public partial class SwitchMeControl : UserControl {
        public SwitchMePlugin Plugin { get; }
        public ConfigObjects ConfigObjects = new ConfigObjects();
        public SwitchMeControl() {

            InitializeComponent();
        }

        public SwitchMeControl(SwitchMePlugin plugin) : this() {

            Plugin = plugin;
            DataContext = plugin.Config;
            if (plugin.Config.UseOnlineConfig) {
                control.IsEnabled = false;
            }
            UpdateDataGrid();
        }

        private void UpdateDataGrid() {

            var servers = from f in Plugin.Config.Servers select new { SERVER = f.ServerName, IP = f.ServerIP, PORT = f.ServerPort };

            dgServerList.ItemsSource = servers;
            try {
                var gates = from f in Plugin.Config.Gates select new { GateTarget = f.TargetServerName, GPS = ConfigObjects.ParseConvertXYZObject(f.GateLocation) , Alias = f.GateName, TargetAlias = f.TargetGate, Enabled = f.Enabled.ToString() };
                dgServerGates.ItemsSource = gates;
            }
            catch (System.IndexOutOfRangeException) {
                Plugin.loadFailure = true;
            }
        }

        private void SaveConfig_OnClick(object sender, RoutedEventArgs e) {
            Plugin.Save();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {

            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));

            e.Handled = true;
        }

        private void BtnAddServer_Click(object sender, RoutedEventArgs e) {

            ConfigObjects.Server server = new ConfigObjects.Server();

            server.ServerName = txtServerName.Text;
            server.ServerIP = txtServerIP.Text;
            server.ServerPort = txtServerPort.Text;


            Plugin.Config.Servers.Add(server);

            UpdateDataGrid();

            dgServerList.Items.MoveCurrentToLast();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void BtnDelServer_Click(object sender, RoutedEventArgs e) {

            if (dgServerList.SelectedIndex >= 0) {

                dynamic dataRow = dgServerList.SelectedItem;

                var server = Plugin.Config.Servers.SingleOrDefault(c => c.ServerName == dataRow.SERVER);
                if (server != null)

                    Plugin.Config.Servers.Remove(server);

                UpdateDataGrid();
            }
        }

        private void DgServerList_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            if (dgServerList.SelectedIndex >= 0) {

                dynamic dataRow = dgServerList.SelectedItem;

                txtServerName.Text = dataRow.SERVER;
                txtServerIP.Text = dataRow.IP;
                txtServerPort.Text = dataRow.PORT;
            }
        }

        private void btnAddGate_Click(object sender, RoutedEventArgs e) {
            ConfigObjects.Gate newGate = new ConfigObjects.Gate();
            newGate.TargetServerName = txtGateTarget.Text;
            newGate.GateName = txtGateAlias.Text;
            newGate.TargetGate = txtTargetAlias.Text;
            newGate.GateLocation.X = XCordGate.Text;
            newGate.GateLocation.Y = YCordGate.Text;
            newGate.GateLocation.Z = ZCordGate.Text;

            var comboBoxItem = CbxGateEnabled.Items[CbxGateEnabled.SelectedIndex] as ComboBoxItem;
            if (comboBoxItem != null) {
                newGate.Enabled = bool.Parse(comboBoxItem.Content.ToString());
            }
            Plugin.Config.Gates.Add(newGate);

            UpdateDataGrid();

            dgServerGates.Items.MoveCurrentToLast();
        }

        private void btnDelGate_Click(object sender, RoutedEventArgs e) {
            if (dgServerGates.SelectedIndex >= 0) {

                dynamic dataRowGate = dgServerGates.SelectedItem;
                var gate = Plugin.Config.Gates.SingleOrDefault(c => c.GateName == dataRowGate.GateTarget);
                if (gate != null)
                    Plugin.Config.Gates.Remove(gate);
                

                UpdateDataGrid();
            }
        }

        private void dgServerGates_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (dgServerGates.SelectedIndex >= 0) {

                dynamic dataRowGate = dgServerGates.SelectedItem;

                txtGateTarget.Text = dataRowGate.GateTarget;
            }
        }

        private async void ReloadPlugin_OnClickAsync(object sender, RoutedEventArgs e)
        {
            APIMethods API = new APIMethods(Plugin);
            if (Plugin.Config.UseOnlineConfig)
            {
                var api_response = await API.LoadOnlineConfig();
                if (api_response["responseCode"] == "0")
                {
                    WebClient myWebClient = new WebClient();
                    myWebClient.DownloadFile($"{Plugin.API_URL + api_response["path"]}", Path.Combine(Plugin.StoragePath, "SwitchMeOnline.cfg"));
                    Plugin._config = Persistent<SwitchMeConfig>.Load(Path.Combine(Plugin.StoragePath, "SwitchMeOnline.cfg"));
                    Plugin.Save();
                }
            }
            Plugin.CloseGates();
            Plugin.OpenGates();
        }
    }
}
