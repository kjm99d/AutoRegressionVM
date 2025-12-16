using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// Microsoft Teams 알림 서비스
    /// </summary>
    public class TeamsNotificationService : INotificationService
    {
        private readonly string _webhookUrl;
        private readonly HttpClient _httpClient;

        public TeamsNotificationService(string webhookUrl)
        {
            _webhookUrl = webhookUrl;
            _httpClient = new HttpClient();
        }

        public async Task SendTestStartedAsync(TestScenario scenario)
        {
            var card = CreateAdaptiveCard(
                "?? 테스트 시작",
                "1E90FF",
                new[]
                {
                    ("시나리오", scenario.Name),
                    ("Steps", $"{scenario.Steps.Count}개"),
                    ("시작 시간", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                });

            await SendCardAsync(card);
        }

        public async Task SendTestCompletedAsync(ScenarioResult result)
        {
            var color = result.IsSuccess ? "28A745" : "DC3545";
            var title = result.IsSuccess ? "? 테스트 완료 - 성공" : "? 테스트 완료 - 실패";

            var card = CreateAdaptiveCard(
                title,
                color,
                new[]
                {
                    ("시나리오", result.ScenarioName),
                    ("소요시간", result.Duration.ToString(@"hh\:mm\:ss")),
                    ("성공", $"{result.PassedCount}개"),
                    ("실패", $"{result.FailedCount}개"),
                    ("스킵", $"{result.SkippedCount}개"),
                    ("오류", $"{result.ErrorCount}개")
                });

            await SendCardAsync(card);
        }

        public async Task SendTestFailedAsync(TestResult result)
        {
            var card = CreateAdaptiveCard(
                "? 테스트 실패",
                "DC3545",
                new[]
                {
                    ("테스트", result.TestStepName),
                    ("VM", result.VMName),
                    ("오류", result.ErrorMessage ?? "알 수 없음")
                });

            await SendCardAsync(card);
        }

        public async Task SendErrorAsync(string errorMessage)
        {
            var card = CreateAdaptiveCard(
                "?? 오류 발생",
                "FFC107",
                new[] { ("메시지", errorMessage) });

            await SendCardAsync(card);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var card = CreateAdaptiveCard(
                    "?? 알림 테스트",
                    "17A2B8",
                    new[] { ("상태", "AutoRegressionVM 알림 테스트 메시지입니다.") });

                await SendCardAsync(card);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private object CreateAdaptiveCard(string title, string color, (string Name, string Value)[] facts)
        {
            var factsList = new System.Collections.Generic.List<object>();
            foreach (var fact in facts)
            {
                factsList.Add(new { name = fact.Name, value = fact.Value });
            }

            return new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = new
                        {
                            type = "AdaptiveCard",
                            version = "1.2",
                            body = new object[]
                            {
                                new
                                {
                                    type = "TextBlock",
                                    text = title,
                                    weight = "bolder",
                                    size = "medium",
                                    color = "accent"
                                },
                                new
                                {
                                    type = "FactSet",
                                    facts = factsList
                                }
                            }
                        }
                    }
                }
            };
        }

                        private async Task SendCardAsync(object card)
                        {
                            if (string.IsNullOrEmpty(_webhookUrl))
                                return;

                            try
                            {
                                var json = SerializeCard(card);
                                var content = new StringContent(json, Encoding.UTF8, "application/json");

                                var response = await _httpClient.PostAsync(_webhookUrl, content);
                                response.EnsureSuccessStatusCode();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Teams 알림 전송 실패: {ex.Message}");
                            }
                        }

                        private string SerializeCard(object card)
                        {
                            // Teams 웹훅은 간단한 텍스트 메시지로 대체
                            var type = card.GetType();
                            var textProp = type.GetProperty("text");
                            if (textProp != null)
                            {
                                var text = textProp.GetValue(card)?.ToString() ?? "";
                                return $"{{\"text\":\"{EscapeJson(text)}\"}}";
                            }
                            return "{\"text\":\"알림\"}";
                        }

                        private string EscapeJson(string s)
                        {
                            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
                        }
                    }
                }
