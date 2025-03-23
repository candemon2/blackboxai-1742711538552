using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace AIAsistani
{
    public class ChatGPTIntegration
    {
        private const string API_URL = "https://api.openai.com/v1/completions";
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public event EventHandler<string> ResponseReceived;
        public event EventHandler<Exception> ErrorOccurred;

        public ChatGPTIntegration(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public class CompletionRequest
        {
            public string Model { get; set; } = "text-davinci-003";
            public int MaxTokens { get; set; } = 150;
            public double Temperature { get; set; } = 0.7;
        }

        public async Task<string> GetResponseAsync(string prompt, CompletionRequest settings = null)
        {
            try
            {
                settings ??= new CompletionRequest();

                var requestBody = new
                {
                    model = settings.Model,
                    prompt = prompt,
                    max_tokens = settings.MaxTokens,
                    temperature = settings.Temperature
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(requestBody),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await _httpClient.PostAsync(API_URL, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API yanıt vermedi: {response.StatusCode} - {responseContent}");
                }

                var completionResponse = JsonConvert.DeserializeObject<CompletionResponse>(responseContent);
                var result = completionResponse?.Choices?.FirstOrDefault()?.Text?.Trim() ?? 
                            "Üzgünüm, yanıt alınamadı.";

                ResponseReceived?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return "Üzgünüm, bir hata oluştu: " + ex.Message;
            }
        }

        public class CompletionResponse
        {
            public List<Choice> Choices { get; set; }
        }

        public class Choice
        {
            public string Text { get; set; }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}