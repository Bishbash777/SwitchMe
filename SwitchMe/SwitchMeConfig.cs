using System.Collections.ObjectModel;
using Torch;

namespace SwitchMe
{
    public class SwitchMeConfig : ViewModel
    {
        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }

        private ObservableCollection<string> _switchServers = new ObservableCollection<string>();
        public ObservableCollection<string> Servers { get => _switchServers; set => SetValue(ref _switchServers, value); }
    }
}
