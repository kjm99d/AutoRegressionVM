using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// 알림 서비스 인터페이스
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// 테스트 시작 알림
        /// </summary>
        Task SendTestStartedAsync(TestScenario scenario);

        /// <summary>
        /// 테스트 완료 알림
        /// </summary>
        Task SendTestCompletedAsync(ScenarioResult result);

        /// <summary>
        /// 테스트 실패 알림
        /// </summary>
        Task SendTestFailedAsync(TestResult result);

        /// <summary>
        /// 오류 알림
        /// </summary>
        Task SendErrorAsync(string errorMessage);

        /// <summary>
        /// 알림 설정 테스트
        /// </summary>
        Task<bool> TestConnectionAsync();
    }
}
