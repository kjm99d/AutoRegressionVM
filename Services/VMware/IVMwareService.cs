using System.Collections.Generic;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.VMware
{
    /// <summary>
    /// VMware 제어 서비스 인터페이스
    /// </summary>
    public interface IVMwareService
    {
        /// <summary>
        /// VMware 호스트에 연결
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 연결 해제
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 연결 상태
        /// </summary>
        bool IsConnected { get; }

        #region VM 관리

        /// <summary>
        /// VM 열기
        /// </summary>
        Task<bool> OpenVMAsync(string vmxPath);

        /// <summary>
        /// VM 전원 켜기
        /// </summary>
        Task<bool> PowerOnAsync(string vmxPath);

        /// <summary>
        /// VM 전원 끄기
        /// </summary>
        Task<bool> PowerOffAsync(string vmxPath);

        /// <summary>
        /// VM 전원 상태 조회
        /// </summary>
        Task<VMPowerState> GetPowerStateAsync(string vmxPath);

        /// <summary>
        /// VMware Tools 준비 대기
        /// </summary>
        Task<bool> WaitForToolsAsync(string vmxPath, int timeoutSeconds = 300);

        #endregion

        #region 스냅샷 관리

        /// <summary>
        /// 스냅샷 목록 조회
        /// </summary>
        Task<List<Snapshot>> GetSnapshotsAsync(string vmxPath);

        /// <summary>
        /// 스냅샷으로 롤백
        /// </summary>
        Task<bool> RevertToSnapshotAsync(string vmxPath, string snapshotName);

        /// <summary>
        /// 스냅샷 생성
        /// </summary>
        Task<bool> CreateSnapshotAsync(string vmxPath, string snapshotName, string description = null);

        /// <summary>
        /// 스냅샷 삭제
        /// </summary>
        Task<bool> DeleteSnapshotAsync(string vmxPath, string snapshotName);

        #endregion

        #region Guest 작업 (VIX)

        /// <summary>
        /// Guest OS 로그인
        /// </summary>
        Task<bool> LoginToGuestAsync(string vmxPath, string username, string password);

        /// <summary>
        /// 호스트 → VM 파일 복사
        /// </summary>
        Task<bool> CopyFileToGuestAsync(string vmxPath, string hostPath, string guestPath);

        /// <summary>
        /// VM → 호스트 파일 복사
        /// </summary>
        Task<bool> CopyFileFromGuestAsync(string vmxPath, string guestPath, string hostPath);

        /// <summary>
        /// VM 내 디렉토리 생성
        /// </summary>
        Task<bool> CreateDirectoryInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM 내 파일 삭제
        /// </summary>
        Task<bool> DeleteFileInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM 내 파일 존재 여부 확인
        /// </summary>
        Task<bool> FileExistsInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM 내 프로그램 실행
        /// </summary>
        Task<GuestProcessResult> RunProgramInGuestAsync(string vmxPath, string programPath, string arguments, int timeoutSeconds = 300);

        /// <summary>
        /// VM 내 스크립트 실행
        /// </summary>
        Task<GuestProcessResult> RunScriptInGuestAsync(string vmxPath, string interpreter, string scriptText, int timeoutSeconds = 300);

        /// <summary>
        /// VM 스크린샷 캡처
        /// </summary>
        Task<bool> CaptureScreenshotAsync(string vmxPath, string hostSavePath);

        #endregion
    }

    /// <summary>
    /// Guest 프로세스 실행 결과
    /// </summary>
    public class GuestProcessResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
