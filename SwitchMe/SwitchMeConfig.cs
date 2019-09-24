﻿using System.Collections.ObjectModel;
using Torch;

namespace SwitchMe
{
    public class SwitchMeConfig : ViewModel
    {
        private bool _enabled = false;
        public bool Enabled { get => _enabled; set => SetValue(ref _enabled, value); }

        private bool _enabledTransfers = false;
        public bool EnabledTransfers { get => _enabledTransfers; set => SetValue(ref _enabledTransfers, value); }

        private bool _enabledMirror = false;
        public bool EnabledMirror { get => _enabledMirror; set => SetValue(ref _enabledMirror, value); }

        private string _localIP = "";
        public string LocalIP { get => _localIP; set => SetValue(ref _localIP, value); }

        private string _ActivationKey = "";
        public string ActivationKey { get => _ActivationKey; set => SetValue(ref _ActivationKey, value); }

        private string _localKey = "";
        public string LocalKey { get => _localKey; set => SetValue(ref _localKey, value); }

        private ObservableCollection<string> _switchServers = new ObservableCollection<string>();
        public ObservableCollection<string> Servers { get => _switchServers; set => SetValue(ref _switchServers, value); }
    }
}
