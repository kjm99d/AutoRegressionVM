using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using AutoRegressionVM.Models;

namespace AutoRegressionVM.Services.Notification
{
    /// <summary>
    /// Microsoft Teams �˸� ����
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
                "?? �׽�Ʈ ����",
                "1E90FF",
                new[]
                {
                    ("�ó�����", scenario.Name),
                    ("Steps", $"{scenario.Steps.Count}��"),
                    ("���� �ð�", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                });

            await SendCardAsync(card);
        }

        public async Task SendTestCompletedAsync(ScenarioResult result)
        {
            var color = result.IsSuccess ? "28A745" : "DC3545";
            var title = result.IsSuccess ? "? �׽�Ʈ �Ϸ� - ����" : "? �׽�Ʈ �Ϸ� - ����";

            var card = CreateAdaptiveCard(
                title,
                color,
                new[]
                {
                    ("�ó�����", result.ScenarioName),
                    ("�ҿ�ð�", result.Duration.ToString(@"hh\:mm\:ss")),
                    ("����", $"{result.PassedCount}��"),
                    ("����", $"{result.FailedCount}��"),
                    ("��ŵ", $"{result.SkippedCount}��"),
                    ("����", $"{result.ErrorCount}��")
                });

            await SendCardAsync(card);
        }

        public async Task SendTestFailedAsync(TestResult result)
        {
            var card = CreateAdaptiveCard(
                "? �׽�Ʈ ����",
                "DC3545",
                new[]
                {
                    ("�׽�Ʈ", result.TestStepName),
                    ("VM", result.VMName),
                    ("����", result.ErrorMessage ?? "�� �� ����")
                });

            await SendCardAsync(card);
        }

        public async Task SendErrorAsync(string errorMessage)
        {
            var card = CreateAdaptiveCard(
                "?? ���� �߻�",
                "FFC107",
                new[] { ("�޽���", errorMessage) });

            await SendCardAsync(card);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var card = CreateAdaptiveCard(
                    "?? �˸� �׽�Ʈ",
                    "17A2B8",
                    new[] { ("����", "AutoRegressionVM �˸� �׽�Ʈ �޽����Դϴ�.") });

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
                                System.Diagnostics.Debug.WriteLine($"Teams �˸� ���� ����: {ex.Message}");
                            }
                        }

                        private string SerializeCard(object card)
                        {
                            return JsonConvert.SerializeObject(card);
                        }
                    }
                }
