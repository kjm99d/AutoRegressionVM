using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AutoRegressionVM.Helpers;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// Slack 알림 서비스
    /// </summary>
    public class SlackNotificationService : INotificationService
    {
        private readonly string _webhookUrl;
        private readonly HttpClient _httpClient;

        public SlackNotificationService(string webhookUrl)
        {
            _webhookUrl = webhookUrl;
            _httpClient = new HttpClient();
        }

        public async Task SendTestStartedAsync(TestScenario scenario)
        {
            var message = new
            {
                text = $"?? *테스트 시작*\n" +
                       $"?? 시나리오: {scenario.Name}\n" +
                       $"?? Steps: {scenario.Steps.Count}개\n" +
                       $"? 시작 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            };

            await SendMessageAsync(message);
        }

        public async Task SendTestCompletedAsync(ScenarioResult result)
        {
            var emoji = result.IsSuccess ? "?" : "?";
            var status = result.IsSuccess ? "성공" : "실패";

            var message = new
            {
                text = $"{emoji} *테스트 완료 - {status}*\n" +
                       $"?? 시나리오: {result.ScenarioName}\n" +
                       $"?? 소요시간: {result.Duration:hh\\:mm\\:ss}\n" +
                       $"? 성공: {result.PassedCount}개\n" +
                       $"? 실패: {result.FailedCount}개\n" +
                       $"?? 스킵: {result.SkippedCount}개\n" +
                       $"?? 오류: {result.ErrorCount}개"
            };

            await SendMessageAsync(message);
        }

        public async Task SendTestFailedAsync(TestResult result)
        {
            var message = new
            {
                text = $"? *테스트 실패*\n" +
                       $"?? 테스트: {result.TestStepName}\n" +
                       $"?? VM: {result.VMName}\n" +
                       $"? 오류: {result.ErrorMessage ?? "알 수 없음"}"
            };

            await SendMessageAsync(message);
        }

        public async Task SendErrorAsync(string errorMessage)
        {
            var message = new
            {
                text = $"?? *오류 발생*\n{errorMessage}"
            };

            await SendMessageAsync(message);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var message = new
                {
                    text = "?? AutoRegressionVM 알림 테스트 메시지입니다."
                };

                await SendMessageAsync(message);
                return true;
            }
            catch
            {
                return false;
            }
        }

                        private async Task SendMessageAsync(object message)
                        {
                            if (string.IsNullOrEmpty(_webhookUrl))
                                return;

                            try
                            {
                                var json = SimpleJsonSerialize(message);
                                var content = new StringContent(json, Encoding.UTF8, "application/json");

                                var response = await _httpClient.PostAsync(_webhookUrl, content);
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Slack 알림 전송 실패: {ex.Message}");
                            }
                        }

                        private string SimpleJsonSerialize(object obj)
                        {
                            // 간단한 익명 객체를 JSON으로 변환
                            var type = obj.GetType();
                            var props = type.GetProperties();
                            var sb = new StringBuilder();
                            sb.Append("{");
                            for (int i = 0; i < props.Length; i++)
                            {
                                if (i > 0) sb.Append(",");
                                var value = props[i].GetValue(obj);
                                sb.Append($"\"{props[i].Name}\":");
                                if (value == null)
                                    sb.Append("null");
                                else if (value is string s)
                                    sb.Append($"\"{EscapeJson(s)}\"");
                                else
                                    sb.Append($"\"{value}\"");
                            }
                            sb.Append("}");
                            return sb.ToString();
                        }

                        private string EscapeJson(string s)
                        {
                            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                        }
                    }
                }
