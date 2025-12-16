using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.VMware
{
    /// <summary>
    /// VMware VIX API를 사용한 VM 제어 서비스
    /// VIX SDK 설치 필요: https://developer.vmware.com/web/sdk/7.0/vix
    /// </summary>
    public class VixService : IVMwareService, IDisposable
    {
        // VIX 핸들
        private dynamic _vixHost;
        private readonly Dictionary<string, dynamic> _openedVMs = new Dictionary<string, dynamic>();
        private readonly Dictionary<string, bool> _loggedInVMs = new Dictionary<string, bool>();

        private string _vmwareInstallPath;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public VixService(string vmwareInstallPath = null)
        {
            _vmwareInstallPath = vmwareInstallPath ?? @"C:\Program Files (x86)\VMware\VMware Workstation";
        }

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // VIX COM 객체 생성
                    // 실제 구현 시 VixCOM.VixLib 참조 필요
                    Type vixType = Type.GetTypeFromProgID("VixCOM.VixLib");
                    if (vixType == null)
                    {
                        throw new InvalidOperationException(
                            "VIX COM이 등록되지 않았습니다. VMware VIX SDK를 설치하세요.");
                    }

                    _vixHost = Activator.CreateInstance(vixType);
                    _isConnected = true;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VIX 연결 실패: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            });
        }

        public void Disconnect()
        {
            foreach (var vm in _openedVMs.Values)
            {
                try
                {
                    // VM 핸들 해제
                }
                catch { }
            }

            _openedVMs.Clear();
            _loggedInVMs.Clear();
            _vixHost = null;
            _isConnected = false;
        }

        #endregion

        #region VM 관리

        public async Task<bool> OpenVMAsync(string vmxPath)
        {
            if (!_isConnected) return false;

            return await Task.Run(() =>
            {
                try
                {
                    if (_openedVMs.ContainsKey(vmxPath))
                        return true;

                    // VIX: Host.OpenVM()
                    // 실제 구현 필요
                    System.Diagnostics.Debug.WriteLine($"[VIX] Opening VM: {vmxPath}");

                    // 플레이스홀더 - 실제 VIX 구현 시 교체
                    _openedVMs[vmxPath] = new object();
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VM 열기 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> PowerOnAsync(string vmxPath)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.PowerOn()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Power On: {vmxPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Power On 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> PowerOffAsync(string vmxPath)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.PowerOff()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Power Off: {vmxPath}");
                    _loggedInVMs.Remove(vmxPath);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Power Off 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<VMPowerState> GetPowerStateAsync(string vmxPath)
        {
            if (!await OpenVMAsync(vmxPath)) return VMPowerState.Unknown;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.GetProperties()로 전원 상태 확인
                    return VMPowerState.PoweredOn; // 플레이스홀더
                }
                catch
                {
                    return VMPowerState.Unknown;
                }
            });
        }

        public async Task<bool> WaitForToolsAsync(string vmxPath, int timeoutSeconds = 300)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.WaitForToolsInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Waiting for VMware Tools: {vmxPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Tools 대기 실패: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        #region 스냅샷 관리

        public async Task<List<Snapshot>> GetSnapshotsAsync(string vmxPath)
        {
            var snapshots = new List<Snapshot>();
            if (!await OpenVMAsync(vmxPath)) return snapshots;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.GetRootSnapshot(), Snapshot.GetChild() 등으로 트리 순회
                    System.Diagnostics.Debug.WriteLine($"[VIX] Getting snapshots: {vmxPath}");
                    return snapshots;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스냅샷 조회 실패: {ex.Message}");
                    return snapshots;
                }
            });
        }

        public async Task<bool> RevertToSnapshotAsync(string vmxPath, string snapshotName)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.RevertToSnapshot()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Reverting to snapshot '{snapshotName}': {vmxPath}");
                    _loggedInVMs.Remove(vmxPath);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스냅샷 롤백 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CreateSnapshotAsync(string vmxPath, string snapshotName, string description = null)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.CreateSnapshot()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Creating snapshot '{snapshotName}': {vmxPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스냅샷 생성 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteSnapshotAsync(string vmxPath, string snapshotName)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.RemoveSnapshot()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Deleting snapshot '{snapshotName}': {vmxPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스냅샷 삭제 실패: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        #region Guest 작업

        public async Task<bool> LoginToGuestAsync(string vmxPath, string username, string password)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.LoginInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Guest login as '{username}': {vmxPath}");
                    _loggedInVMs[vmxPath] = true;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Guest 로그인 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CopyFileToGuestAsync(string vmxPath, string hostPath, string guestPath)
        {
            if (!_loggedInVMs.ContainsKey(vmxPath))
            {
                System.Diagnostics.Debug.WriteLine("Guest 로그인이 필요합니다.");
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.CopyFileFromHostToGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Copy to guest: {hostPath} -> {guestPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"파일 복사(→Guest) 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CopyFileFromGuestAsync(string vmxPath, string guestPath, string hostPath)
        {
            if (!_loggedInVMs.ContainsKey(vmxPath))
            {
                System.Diagnostics.Debug.WriteLine("Guest 로그인이 필요합니다.");
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // 호스트 디렉토리 생성
                    var hostDir = Path.GetDirectoryName(hostPath);
                    if (!string.IsNullOrEmpty(hostDir) && !Directory.Exists(hostDir))
                    {
                        Directory.CreateDirectory(hostDir);
                    }

                    // VIX: VM.CopyFileFromGuestToHost()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Copy from guest: {guestPath} -> {hostPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"파일 복사(←Guest) 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CreateDirectoryInGuestAsync(string vmxPath, string guestPath)
        {
            if (!_loggedInVMs.ContainsKey(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.CreateDirectoryInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Create directory in guest: {guestPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"디렉토리 생성 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DeleteFileInGuestAsync(string vmxPath, string guestPath)
        {
            if (!_loggedInVMs.ContainsKey(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.DeleteFileInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Delete in guest: {guestPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"파일 삭제 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> FileExistsInGuestAsync(string vmxPath, string guestPath)
        {
            if (!_loggedInVMs.ContainsKey(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.FileExistsInGuest()
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<GuestProcessResult> RunProgramInGuestAsync(string vmxPath, string programPath, string arguments, int timeoutSeconds = 300)
        {
            var result = new GuestProcessResult();

            if (!_loggedInVMs.ContainsKey(vmxPath))
            {
                result.Success = false;
                result.ErrorMessage = "Guest 로그인이 필요합니다.";
                return result;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.RunProgramInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Run program: {programPath} {arguments}");

                    result.Success = true;
                    result.ExitCode = 0;
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    return result;
                }
            });
        }

        public async Task<GuestProcessResult> RunScriptInGuestAsync(string vmxPath, string interpreter, string scriptText, int timeoutSeconds = 300)
        {
            var result = new GuestProcessResult();

            if (!_loggedInVMs.ContainsKey(vmxPath))
            {
                result.Success = false;
                result.ErrorMessage = "Guest 로그인이 필요합니다.";
                return result;
            }

            return await Task.Run(() =>
            {
                try
                {
                    // VIX: VM.RunScriptInGuest()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Run script ({interpreter}): {scriptText.Substring(0, Math.Min(50, scriptText.Length))}...");

                    result.Success = true;
                    result.ExitCode = 0;
                    return result;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    return result;
                }
            });
        }

        public async Task<bool> CaptureScreenshotAsync(string vmxPath, string hostSavePath)
        {
            if (!await OpenVMAsync(vmxPath)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    // 호스트 디렉토리 생성
                    var hostDir = Path.GetDirectoryName(hostSavePath);
                    if (!string.IsNullOrEmpty(hostDir) && !Directory.Exists(hostDir))
                    {
                        Directory.CreateDirectory(hostDir);
                    }

                    // VIX: VM.CaptureScreenImage()
                    System.Diagnostics.Debug.WriteLine($"[VIX] Capture screenshot: {hostSavePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스크린샷 캡처 실패: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        public void Dispose()
        {
            Disconnect();
        }
    }
}
