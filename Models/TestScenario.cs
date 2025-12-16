using System;
using System.Collections.Generic;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// 테스트 시나리오 (테스트 Step들의 모음)
    /// </summary>
    public class TestScenario
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastRunAt { get; set; }

        /// <summary>
        /// 시나리오에 포함된 테스트 Step 목록
        /// </summary>
        public List<TestStep> Steps { get; set; } = new List<TestStep>();

        /// <summary>
        /// 병렬 실행 시 최대 동시 VM 수
        /// </summary>
        public int MaxParallelVMs { get; set; } = 1;

        /// <summary>
        /// 실패 시 계속 진행 여부
        /// </summary>
        public bool ContinueOnFailure { get; set; } = true;
    }
}
