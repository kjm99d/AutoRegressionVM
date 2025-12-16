using System;
using AutoRegressionVM.Helpers;

namespace AutoRegressionVM.Models
{
    /// <summary>
    /// VM 실행 상태 (병렬 실행 시각화용)
    /// </summary>
    public class VMExecutionStatus : ViewModelBase
    {
        private string _vmName;
        public string VMName
        {
            get => _vmName;
            set => SetProperty(ref _vmName, value);
        }

        private string _stepName;
        public string StepName
        {
            get => _stepName;
            set => SetProperty(ref _stepName, value);
        }

        private VMExecutionPhase _phase;
        public VMExecutionPhase Phase
        {
            get => _phase;
            set => SetProperty(ref _phase, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private DateTime _startTime;
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string PhaseText
        {
            get
            {
                switch (Phase)
                {
                    case VMExecutionPhase.Idle: return "대기";
                    case VMExecutionPhase.RevertingSnapshot: return "스냅샷 복원";
                    case VMExecutionPhase.Booting: return "부팅 중";
                    case VMExecutionPhase.CopyingFiles: return "파일 복사";
                    case VMExecutionPhase.Executing: return "실행 중";
                    case VMExecutionPhase.Collecting: return "결과 수집";
                    case VMExecutionPhase.Completed: return "완료";
                    case VMExecutionPhase.Failed: return "실패";
                    default: return Phase.ToString();
                }
            }
        }
    }

    public enum VMExecutionPhase
    {
        Idle,
        RevertingSnapshot,
        Booting,
        CopyingFiles,
        Executing,
        Collecting,
        Completed,
        Failed
    }
}
