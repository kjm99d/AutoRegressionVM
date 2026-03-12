using System.Collections.Generic;
using Newtonsoft.Json;
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

        /// <summary>
        /// DPAPI로 암호화된 비밀번호 (JSON 직렬화용)
        /// </summary>
        [JsonProperty("GuestPassword")]
        public string EncryptedGuestPassword
        {
            get => CredentialProtector.Encrypt(_guestPassword);
            set => _guestPassword = CredentialProtector.Decrypt(value);
        }

        private string _guestPassword;

        /// <summary>
        /// 복호화된 비밀번호 (런타임 사용용)
        /// </summary>
        [JsonIgnore]
        public string GuestPassword
        {
            get => _guestPassword;
            set => _guestPassword = value;
        }
    }

    public enum VMPowerState
    {
        Unknown,
        PoweredOff,
        PoweredOn,
        Suspended
    }
}
