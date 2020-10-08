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

namespace SwitchMe {

    /// <summary>
    /// Interação lógica para SEDBControl.xaml
    /// </summary>
    public partial class SwitchMeControl : UserControl {
        public SwitchMePlugin Plugin { get; }

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

            var servers = from f in Plugin.Config.Servers select new { SERVER = f.Split(':')[0], IP = f.Split(':')[1], PORT = f.Split(':')[2] };

            dgServerList.ItemsSource = servers;
            try {
                var gates = from f in Plugin.Config.Gates select new { GateTarget = f.Split('/')[0], GPS = f.Split('/')[1], Alias = f.Split('/')[2], TargetAlias = f.Split('/')[3] };
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

            if (txtServerName.Text.Length > 0 && txtServerIP.Text.Length > 0 && txtServerPort.Text.Length > 0) {

                Plugin.Config.Servers.Add(txtServerName.Text + ":" + txtServerIP.Text + ":" + txtServerPort.Text);

                UpdateDataGrid();

                dgServerList.Items.MoveCurrentToLast();
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e) {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void BtnDelServer_Click(object sender, RoutedEventArgs e) {

            if (dgServerList.SelectedIndex >= 0) {

                dynamic dataRow = dgServerList.SelectedItem;

                Plugin.Config.Servers.Remove(dataRow.SERVER + ":" + dataRow.IP + ":" + dataRow.PORT);

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

        private void CheckBox_Checked(object sender, RoutedEventArgs e) {

        }

        private void btnAddGate_Click(object sender, RoutedEventArgs e) {
            string POS = "{X:" + XCordGate.Text + " Y:" + YCordGate.Text + " Z:" + ZCordGate.Text + "}";
            if (POS.Length > 0) {

                Plugin.Config.Gates.Add($"{txtGateTarget.Text}/{POS}/{txtGateAlias.Text}/{txtTargetAlias.Text}");

                UpdateDataGrid();

                dgServerGates.Items.MoveCurrentToLast();
            }
        }

        private void btnDelGate_Click(object sender, RoutedEventArgs e) {
            if (dgServerGates.SelectedIndex >= 0) {

                dynamic dataRowGate = dgServerGates.SelectedItem;

                Plugin.Config.Gates.Remove($"{dataRowGate.GateTarget}/{dataRowGate.GPS}/{dataRowGate.Alias}/{dataRowGate.TargetAlias}");

                UpdateDataGrid();
            }
        }

        private void dgServerGates_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (dgServerGates.SelectedIndex >= 0) {

                dynamic dataRowGate = dgServerGates.SelectedItem;

                txtGateTarget.Text = dataRowGate.GateTarget;
            }
        }
    }
}
