using System;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// VM ½º³À¼¦ Á¤º¸
    /// </summary>
    public class Snapshot
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string ParentSnapshotName { get; set; }
    }
}
