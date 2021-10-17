using System.Collections.Generic;
using System.Collections.ObjectModel;
using Torch;

namespace SwitchMe {
    public class SwitchMeConfig : ViewModel {

        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }

        private bool _useOnlineConfig = false;
        public bool UseOnlineConfig { get => _useOnlineConfig; set => SetValue(ref _useOnlineConfig, value); }

        private bool _debug = false;
        public bool Debug { get => _debug; set => SetValue(ref _debug, value); }

        private bool _enabledTransfers = false;
        public bool EnabledTransfers { get => _enabledTransfers; set => SetValue(ref _enabledTransfers, value); }

        private bool _enabledJumpgate = false;
        public bool EnabledJumpgate { get => _enabledJumpgate; set => SetValue(ref _enabledJumpgate, value); }

        private bool _randomisedExit = false;
        public bool RandomisedExit { get => _randomisedExit; set => SetValue(ref _randomisedExit, value); }

        private int _gateSize = 50;
        public int GateSize { get => _gateSize; set => SetValue(ref _gateSize, value); }

        private bool _inboundTransfersState = false;
        public bool InboundTransfersState { get => _inboundTransfersState; set => SetValue(ref _inboundTransfersState, value); }

        private string _localIP = "";
        public string LocalIP { get => _localIP; set => SetValue(ref _localIP, value); }

        /*
        private string _ActivationKey = "";
        public string ActivationKey { get => _ActivationKey; set => SetValue(ref _ActivationKey, value); }
        */

        private string _BindingKey = "";
        public string BindingKey { get => _BindingKey; set => SetValue(ref _BindingKey, value); }

        /*
        private bool _econEnabled = false;
        public bool EnableEcon { get => _econEnabled; set => SetValue(ref _econEnabled, value); }

        private bool _perTransfer = false;
        public bool PerTransfer { get => _perTransfer; set => SetValue(ref _perTransfer, value); }

        private bool _perBlock = false;
        public bool PerBlock { get => _perBlock; set => SetValue(ref _perBlock, value); }

        private long _transferCost = 30;
        public long TransferCost { get => _transferCost; set => SetValue(ref _transferCost, value); }
        */

        private bool _enableBlockEnforcement = false;
        public bool EnableBlockEnforcement { get => _enableBlockEnforcement; set => SetValue(ref _enableBlockEnforcement, value); }

        private bool _blockAllow = false;
        public bool BlockAllow { get => _blockAllow; set => SetValue(ref _blockAllow, value); }

        private bool _blockDisllow = false;
        public bool BlockDisallow { get => _blockDisllow; set => SetValue(ref _blockDisllow, value); }

        private string _subTypes = "";
        public string SubTypes { get => _subTypes; set => SetValue(ref _subTypes, value); }

        private int _cooldownInSeconds = 5 * 60; //15 Minutes
        public int ConfirmationInSeconds { get => _confirmationInSeconds; set => SetValue(ref _confirmationInSeconds, value); }

        private int _confirmationInSeconds = 30; //30 Seconds
        public int CooldownInSeconds { get => _cooldownInSeconds; set => SetValue(ref _cooldownInSeconds, value); }

        private List<ConfigObjects.Gate> _gates = new List<ConfigObjects.Gate>();
        public List<ConfigObjects.Gate> Gates { get => _gates; set => SetValue(ref _gates, value); }

        private List<ConfigObjects.Server> _servers = new List<ConfigObjects.Server>();
        public List<ConfigObjects.Server> Servers { get => _servers; set => SetValue(ref _servers, value); }

    }
}
