using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// VM 정보
    /// </summary>
    public class VMInfo
    {
        public string Name { get; set; }
        public string VmxPath { get; set; }
        public VMPowerState PowerState { get; set; }
        public List<Snapshot> Snapshots { get; set; } = new List<Snapshot>();

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
