using System.Collections.Generic;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.VMware
{
    /// <summary>
    /// VMware ���� ���� �������̽�
    /// </summary>
    public interface IVMwareService
    {
        /// <summary>
        /// VMware ȣ��Ʈ�� ����
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// ���� ����
        /// </summary>
        void Disconnect();

        /// <summary>
        /// ���� ����
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// VMware에 등록된 모든 VM 목록 조회
        /// </summary>
        Task<List<VMInfo>> GetRegisteredVMsAsync();

        /// <summary>
        /// 현재 실행 중인 VM 목록 조회
        /// </summary>
        Task<List<string>> GetRunningVMsAsync();

        #region VM ����

        /// <summary>
        /// VM ����
        /// </summary>
        Task<bool> OpenVMAsync(string vmxPath);

        /// <summary>
        /// VM ���� �ѱ�
        /// </summary>
        Task<bool> PowerOnAsync(string vmxPath);

        /// <summary>
        /// VM ���� ����
        /// </summary>
        Task<bool> PowerOffAsync(string vmxPath);

        /// <summary>
        /// VM ���� ���� ��ȸ
        /// </summary>
        Task<VMPowerState> GetPowerStateAsync(string vmxPath);

        /// <summary>
        /// VMware Tools �غ� ���
        /// </summary>
        Task<bool> WaitForToolsAsync(string vmxPath, int timeoutSeconds = 300);

        #endregion

        #region ������ ����

        /// <summary>
        /// ������ ��� ��ȸ
        /// </summary>
        Task<List<Snapshot>> GetSnapshotsAsync(string vmxPath);

        /// <summary>
        /// ���������� �ѹ�
        /// </summary>
        Task<bool> RevertToSnapshotAsync(string vmxPath, string snapshotName);

        /// <summary>
        /// ������ ����
        /// </summary>
        Task<bool> CreateSnapshotAsync(string vmxPath, string snapshotName, string description = null);

        /// <summary>
        /// ������ ����
        /// </summary>
        Task<bool> DeleteSnapshotAsync(string vmxPath, string snapshotName);

        #endregion

        #region Guest �۾� (VIX)

        /// <summary>
        /// Guest OS �α���
        /// </summary>
        Task<bool> LoginToGuestAsync(string vmxPath, string username, string password);

        /// <summary>
        /// ȣ��Ʈ �� VM ���� ����
        /// </summary>
        Task<bool> CopyFileToGuestAsync(string vmxPath, string hostPath, string guestPath);

        /// <summary>
        /// VM �� ȣ��Ʈ ���� ����
        /// </summary>
        Task<bool> CopyFileFromGuestAsync(string vmxPath, string guestPath, string hostPath);

        /// <summary>
        /// VM �� ���丮 ����
        /// </summary>
        Task<bool> CreateDirectoryInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM �� ���� ����
        /// </summary>
        Task<bool> DeleteFileInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM �� ���� ���� ���� Ȯ��
        /// </summary>
        Task<bool> FileExistsInGuestAsync(string vmxPath, string guestPath);

        /// <summary>
        /// VM �� ���α׷� ����
        /// </summary>
        Task<GuestProcessResult> RunProgramInGuestAsync(string vmxPath, string programPath, string arguments, int timeoutSeconds = 300);

        /// <summary>
        /// VM �� ��ũ��Ʈ ����
        /// </summary>
        Task<GuestProcessResult> RunScriptInGuestAsync(string vmxPath, string interpreter, string scriptText, int timeoutSeconds = 300);

        /// <summary>
        /// VM ��ũ���� ĸó
        /// </summary>
        Task<bool> CaptureScreenshotAsync(string vmxPath, string hostSavePath);

        #endregion
    }

    /// <summary>
    /// Guest ���μ��� ���� ���
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
