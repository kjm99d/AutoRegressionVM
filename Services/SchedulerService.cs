using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services
{
    /// <summary>
    /// 테스트 스케줄러 서비스
    /// </summary>
    public class SchedulerService : IDisposable
    {
        private readonly Timer _timer;
        private readonly List<ScheduledTask> _tasks = new List<ScheduledTask>();
        private readonly object _lock = new object();
        private bool _isDisposed;

        public event EventHandler<ScheduledTask> TaskTriggered;

        public SchedulerService()
        {
            _timer = new Timer(60000); // 1분마다 체크
            _timer.Elapsed += Timer_Elapsed;
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void AddTask(ScheduledTask task)
        {
            lock (_lock)
            {
                _tasks.Add(task);
            }
        }

        public void RemoveTask(string taskId)
        {
            lock (_lock)
            {
                _tasks.RemoveAll(t => t.Id == taskId);
            }
        }

        public List<ScheduledTask> GetAllTasks()
        {
            lock (_lock)
            {
                return _tasks.ToList();
            }
        }

        public void UpdateTask(ScheduledTask task)
        {
            lock (_lock)
            {
                var index = _tasks.FindIndex(t => t.Id == task.Id);
                if (index >= 0)
                {
                    _tasks[index] = task;
                }
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var tasksToRun = new List<ScheduledTask>();

            lock (_lock)
            {
                foreach (var task in _tasks.Where(t => t.IsEnabled))
                {
                    if (ShouldRunNow(task, now))
                    {
                        tasksToRun.Add(task);
                        task.LastRunTime = now;
                        CalculateNextRunTime(task);
                    }
                }
            }

            foreach (var task in tasksToRun)
            {
                TaskTriggered?.Invoke(this, task);
            }
        }

        private bool ShouldRunNow(ScheduledTask task, DateTime now)
        {
            if (task.NextRunTime.HasValue && task.NextRunTime.Value <= now)
            {
                return true;
            }

            return false;
        }

        private void CalculateNextRunTime(ScheduledTask task)
        {
            var now = DateTime.Now;

            switch (task.ScheduleType)
            {
                case ScheduleType.Once:
                    task.NextRunTime = null; // 한 번 실행 후 비활성화
                    task.IsEnabled = false;
                    break;

                case ScheduleType.Daily:
                    task.NextRunTime = now.Date.AddDays(1).Add(task.RunTime);
                    break;

                case ScheduleType.Weekly:
                    var daysUntilNext = ((int)task.DayOfWeek - (int)now.DayOfWeek + 7) % 7;
                    if (daysUntilNext == 0) daysUntilNext = 7;
                    task.NextRunTime = now.Date.AddDays(daysUntilNext).Add(task.RunTime);
                    break;

                case ScheduleType.Monthly:
                    var nextMonth = now.AddMonths(1);
                    var day = Math.Min(task.DayOfMonth, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
                    task.NextRunTime = new DateTime(nextMonth.Year, nextMonth.Month, day).Add(task.RunTime);
                    break;

                case ScheduleType.Interval:
                    task.NextRunTime = now.Add(task.Interval);
                    break;
            }
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _timer?.Stop();
                _timer?.Dispose();
                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// 스케줄된 작업
    /// </summary>
    public class ScheduledTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string ScenarioId { get; set; }
        public string ScenarioName { get; set; }
        public bool IsEnabled { get; set; } = true;

        public ScheduleType ScheduleType { get; set; }
        public TimeSpan RunTime { get; set; }
        public DayOfWeek DayOfWeek { get; set; }
        public int DayOfMonth { get; set; } = 1;
        public TimeSpan Interval { get; set; }

        public DateTime? NextRunTime { get; set; }
        public DateTime? LastRunTime { get; set; }
    }

    public enum ScheduleType
    {
        Once,       // 한 번만
        Daily,      // 매일
        Weekly,     // 매주
        Monthly,    // 매월
        Interval    // 간격
    }
}
