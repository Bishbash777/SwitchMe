using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using VRage.Game;

namespace SwitchMe {

    /// <summary>
    /// Interação lógica para SEDBControl.xaml
    /// </summary>
    public partial class SwitchMeControl : UserControl {
        private SwitchMePlugin Plugin { get; }

        public SwitchMeControl() {

            InitializeComponent();
        }

        public SwitchMeControl(SwitchMePlugin plugin) : this() {

            Plugin = plugin;
            DataContext = plugin.Config;

            UpdateDataGrid();
        }

        private void UpdateDataGrid() {

            var servers = from f in Plugin.Config.Servers select new { SERVER = f.Split(':')[0], IP = f.Split(':')[1], PORT = f.Split(':')[2] };

            dgServerList.ItemsSource = servers;
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
    }
}
