using System;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// VM 스냅샷 정보
    /// </summary>
    public class Snapshot
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string ParentSnapshotName { get; set; }
    }
}
