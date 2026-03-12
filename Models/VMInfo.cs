using System.Collections.Generic;
using AutoRegressionVM.Helpers;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// VM 정보
    /// </summary>
    public class VMInfo : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _vmxPath;
        public string VmxPath
        {
            get => _vmxPath;
            set => SetProperty(ref _vmxPath, value);
        }

        private VMPowerState _powerState;
        public VMPowerState PowerState
        {
            get => _powerState;
            set => SetProperty(ref _powerState, value);
        }

        private List<Snapshot> _snapshots = new List<Snapshot>();
        public List<Snapshot> Snapshots
        {
            get => _snapshots;
            set => SetProperty(ref _snapshots, value);
        }

        /// <summary>
        /// Guest OS 로그인 정보
        /// </summary>
        public string GuestUsername { get; set; }
        public string GuestPassword { get; set; }
    }

    public enum VMPowerState
    {
        Unknown,
        PoweredOff,
        PoweredOn,
        Suspended
    }
}
