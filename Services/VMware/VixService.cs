using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.VMware
{
    /// <summary>
    /// VMware vmrun CLI를 사용한 VM 제어 서비스
    /// VMware Workstation Pro에 포함된 vmrun.exe 사용
    /// </summary>
    public class VixService : IVMwareService, IDisposable
    {
        private readonly Dictionary<string, GuestCredentials> _guestCredentials = new Dictionary<string, GuestCredentials>();
        private string _vmrunPath;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public VixService(string vmwareInstallPath = null)
        {
            var basePath = vmwareInstallPath ?? @"C:\Program Files (x86)\VMware\VMware Workstation";
            _vmrunPath = Path.Combine(basePath, "vmrun.exe");
        }

        #region 연결 관리

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(_vmrunPath))
                    {
                        // 다른 경로 시도
                        var altPath = @"C:\Program Files\VMware\VMware Workstation\vmrun.exe";
                        if (File.Exists(altPath))
                        {
                            _vmrunPath = altPath;
                        }
                        else
                        {
                            throw new FileNotFoundException(
                                $"vmrun.exe를 찾을 수 없습니다. VMware Workstation이 설치되어 있는지 확인하세요.\n" +
                                $"확인한 경로:\n- {_vmrunPath}\n- {altPath}");
                        }
                    }

                    // vmrun 버전 확인으로 정상 동작 테스트
                    var result = RunVmrun("", timeoutSeconds: 10);
                    _isConnected = true;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VMware 연결 실패: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            });
        }

        public void Disconnect()
        {
            _guestCredentials.Clear();
            _isConnected = false;
        }

        #endregion

        #region VM 목록 조회

        public async Task<List<VMInfo>> GetRegisteredVMsAsync()
        {
            return await Task.Run(() =>
            {
                var vmList = new List<VMInfo>();

                try
                {
                    // VMware inventory 파일 경로
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var inventoryPath = Path.Combine(appData, "VMware", "inventory.vmls");

                    if (!File.Exists(inventoryPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Inventory 파일을 찾을 수 없습니다: {inventoryPath}");
                        return vmList;
                    }

                    var lines = File.ReadAllLines(inventoryPath);
                    var vmEntries = new Dictionary<string, Dictionary<string, string>>();

                    // inventory.vmls 파싱
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || !trimmed.Contains("="))
                            continue;

                        var eqIndex = trimmed.IndexOf('=');
                        var key = trimmed.Substring(0, eqIndex).Trim();
                        var value = trimmed.Substring(eqIndex + 1).Trim().Trim('"');

                        // vmlistN.property 형식 파싱
                        if (key.StartsWith("vmlist"))
                        {
                            var dotIndex = key.IndexOf('.');
                            if (dotIndex > 0)
                            {
                                var vmKey = key.Substring(0, dotIndex);
                                var propName = key.Substring(dotIndex + 1);

                                if (!vmEntries.ContainsKey(vmKey))
                                    vmEntries[vmKey] = new Dictionary<string, string>();

                                vmEntries[vmKey][propName] = value;
                            }
                        }
                    }

                    // VMInfo 객체 생성
                    foreach (var entry in vmEntries.Values)
                    {
                        if (entry.TryGetValue("config", out var vmxPath) && !string.IsNullOrEmpty(vmxPath))
                        {
                            var vmInfo = new VMInfo
                            {
                                VmxPath = vmxPath,
                                Name = entry.TryGetValue("DisplayName", out var displayName) && !string.IsNullOrEmpty(displayName)
                                    ? displayName
                                    : Path.GetFileNameWithoutExtension(vmxPath)
                            };

                            vmList.Add(vmInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VM 목록 조회 실패: {ex.Message}");
                }

                return vmList;
            });
        }

        public async Task<List<string>> GetRunningVMsAsync()
        {
            return await Task.Run(() =>
            {
                var runningVMs = new List<string>();

                try
                {
                    var result = RunVmrun("list");
                    if (result.ExitCode == 0)
                    {
                        var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            // 첫 줄은 "Total running VMs: N" 형식
                            if (!line.StartsWith("Total") && line.EndsWith(".vmx", StringComparison.OrdinalIgnoreCase))
                            {
                                runningVMs.Add(line.Trim());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"실행 중인 VM 조회 실패: {ex.Message}");
                }

                return runningVMs;
            });
        }

        #endregion

        #region VM 제어

        public async Task<bool> OpenVMAsync(string vmxPath)
        {
            // vmrun은 명시적 Open이 필요 없음
            return await Task.FromResult(File.Exists(vmxPath));
        }

        public async Task<bool> PowerOnAsync(string vmxPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"start \"{vmxPath}\" nogui");
                    return result.ExitCode == 0;
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
            return await Task.Run(() =>
            {
                try
                {
                    // soft: Guest OS 정상 종료 시도
                    var result = RunVmrun($"stop \"{vmxPath}\" soft");
                    _guestCredentials.Remove(vmxPath);
                    return result.ExitCode == 0;
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
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun("list");
                    if (result.Output.Contains(vmxPath))
                    {
                        return VMPowerState.PoweredOn;
                    }
                    return VMPowerState.PoweredOff;
                }
                catch
                {
                    return VMPowerState.Unknown;
                }
            });
        }

        public async Task<bool> WaitForToolsAsync(string vmxPath, int timeoutSeconds = 300)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"checkToolsState \"{vmxPath}\"", timeoutSeconds);
                    // "running"이면 Tools가 준비된 상태
                    return result.Output.Contains("running");
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

            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"listSnapshots \"{vmxPath}\"");
                    if (result.ExitCode == 0)
                    {
                        var lines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            // 첫 줄은 "Total snapshots: N" 형식
                            if (!line.StartsWith("Total"))
                            {
                                snapshots.Add(new Snapshot { Name = line.Trim() });
                            }
                        }
                    }
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
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"revertToSnapshot \"{vmxPath}\" \"{snapshotName}\"", 120);
                    _guestCredentials.Remove(vmxPath);
                    return result.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스냅샷 복원 실패: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> CreateSnapshotAsync(string vmxPath, string snapshotName, string description = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"snapshot \"{vmxPath}\" \"{snapshotName}\"", 120);
                    return result.ExitCode == 0;
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
            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun($"deleteSnapshot \"{vmxPath}\" \"{snapshotName}\"", 120);
                    return result.ExitCode == 0;
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
            return await Task.Run(() =>
            {
                // vmrun은 각 명령에 -gu/-gp 옵션으로 인증
                // 여기서는 자격 증명을 저장해두고 이후 명령에서 사용
                _guestCredentials[vmxPath] = new GuestCredentials { Username = username, Password = password };
                return true;
            });
        }

        public async Task<bool> CopyFileToGuestAsync(string vmxPath, string hostPath, string guestPath)
        {
            if (!_guestCredentials.TryGetValue(vmxPath, out var cred))
            {
                System.Diagnostics.Debug.WriteLine("Guest 로그인이 필요합니다.");
                return false;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"copyFileFromHostToGuest \"{vmxPath}\" \"{hostPath}\" \"{guestPath}\"");
                    return result.ExitCode == 0;
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
            if (!_guestCredentials.TryGetValue(vmxPath, out var cred))
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

                    var result = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"copyFileFromGuestToHost \"{vmxPath}\" \"{guestPath}\" \"{hostPath}\"");
                    return result.ExitCode == 0;
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
            if (!_guestCredentials.TryGetValue(vmxPath, out var cred)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"createDirectoryInGuest \"{vmxPath}\" \"{guestPath}\"");
                    return result.ExitCode == 0;
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
            if (!_guestCredentials.TryGetValue(vmxPath, out var cred)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"deleteFileInGuest \"{vmxPath}\" \"{guestPath}\"");
                    return result.ExitCode == 0;
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
            if (!_guestCredentials.TryGetValue(vmxPath, out var cred)) return false;

            return await Task.Run(() =>
            {
                try
                {
                    var result = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"fileExistsInGuest \"{vmxPath}\" \"{guestPath}\"");
                    return result.ExitCode == 0;
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

            if (!_guestCredentials.TryGetValue(vmxPath, out var cred))
            {
                result.Success = false;
                result.ErrorMessage = "Guest 로그인이 필요합니다.";
                return result;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var vmrunResult = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"runProgramInGuest \"{vmxPath}\" -noWait -activeWindow \"{programPath}\" {arguments}",
                        timeoutSeconds);

                    result.Success = vmrunResult.ExitCode == 0;
                    result.ExitCode = vmrunResult.ExitCode;
                    result.StandardOutput = vmrunResult.Output;
                    result.StandardError = vmrunResult.Error;
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

            if (!_guestCredentials.TryGetValue(vmxPath, out var cred))
            {
                result.Success = false;
                result.ErrorMessage = "Guest 로그인이 필요합니다.";
                return result;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var vmrunResult = RunVmrun(
                        $"-gu \"{cred.Username}\" -gp \"{cred.Password}\" " +
                        $"runScriptInGuest \"{vmxPath}\" \"{interpreter}\" \"{scriptText}\"",
                        timeoutSeconds);

                    result.Success = vmrunResult.ExitCode == 0;
                    result.ExitCode = vmrunResult.ExitCode;
                    result.StandardOutput = vmrunResult.Output;
                    result.StandardError = vmrunResult.Error;
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

                    var result = RunVmrun($"captureScreen \"{vmxPath}\" \"{hostSavePath}\"");
                    return result.ExitCode == 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"스크린샷 캡처 실패: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion

        #region 내부 메서드

        private VmrunResult RunVmrun(string arguments, int timeoutSeconds = 60)
        {
            var result = new VmrunResult();

            var psi = new ProcessStartInfo
            {
                FileName = _vmrunPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = psi })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    process.Kill();
                    throw new TimeoutException($"vmrun 명령이 {timeoutSeconds}초 내에 완료되지 않았습니다.");
                }

                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString();
                result.Error = errorBuilder.ToString();
            }

            return result;
        }

        #endregion

        public void Dispose()
        {
            Disconnect();
        }

        private class GuestCredentials
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private class VmrunResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
    }
}
