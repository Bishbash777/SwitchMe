using System.Collections.ObjectModel;
using Torch;

namespace SwitchMe {
    public class SwitchMeConfig : ViewModel {

        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }

        private bool _enabledTransfers = false;
        public bool EnabledTransfers { get => _enabledTransfers; set => SetValue(ref _enabledTransfers, value); }

        private bool _inboundTransfersState = true;
        public bool InboundTransfersState { get => _inboundTransfersState; set => SetValue(ref _inboundTransfersState, value); }

        private bool _enabledMirror = false;
        public bool EnabledMirror { get => _enabledMirror; set => SetValue(ref _enabledMirror, value); }

        private bool _lockedTransfer = false;
        public bool LockedTransfer { get => _lockedTransfer; set => SetValue(ref _lockedTransfer, value); }

        private string _Xcord = "";
        public string XCord { get => _Xcord; set => SetValue(ref _Xcord, value); }

        private string _ycord = "";
        public string YCord { get => _ycord; set => SetValue(ref _ycord, value); }

        private string _zcord = "";
        public string ZCord { get => _zcord; set => SetValue(ref _zcord, value); }

        private string _localIP = "";
        public string LocalIP { get => _localIP; set => SetValue(ref _localIP, value); }

        private string _ActivationKey = "";
        public string ActivationKey { get => _ActivationKey; set => SetValue(ref _ActivationKey, value); }

        private string _localKey = "";
        public string LocalKey { get => _localKey; set => SetValue(ref _localKey, value); }

        private bool _econEnabled = false;
        public bool EnableEcon { get => _econEnabled; set => SetValue(ref _econEnabled, value); }

        private bool _perTransfer = false;
        public bool PerTransfer { get => _perTransfer; set => SetValue(ref _perTransfer, value); }

        private bool _perBlock = false;
        public bool PerBlock { get => _perBlock; set => SetValue(ref _perBlock, value); }

        private long _transferCost = 30;
        public long TransferCost { get => _transferCost; set => SetValue(ref _transferCost, value); }

        private ObservableCollection<string> _switchServers = new ObservableCollection<string>();
        public ObservableCollection<string> Servers { get => _switchServers; set => SetValue(ref _switchServers, value); }
    }
}
