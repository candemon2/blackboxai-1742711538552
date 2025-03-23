using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AIAsistani
{
    public class APIManager
    {
        private readonly HttpClient _httpClient;
        private readonly string _weatherApiKey;
        private readonly string _newsApiKey;
        private const string WEATHER_API_BASE = "https://api.openweathermap.org/data/2.5/weather";
        private const string NEWS_API_BASE = "https://newsapi.org/v2/top-headlines";

        public event EventHandler<Exception> ErrorOccurred;

        public APIManager(dynamic apiKeys)
        {
            _httpClient = new HttpClient();
            _weatherApiKey = apiKeys?.weather?.ToString();
            _newsApiKey = apiKeys?.news?.ToString();
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city = "Istanbul")
        {
            try
            {
                if (string.IsNullOrEmpty(_weatherApiKey))
                {
                    return GenerateSampleWeather(city);
                }

                var url = $"{WEATHER_API_BASE}?q={city}&appid={_weatherApiKey}&units=metric&lang=tr";
                var response = await _httpClient.GetStringAsync(url);
                var weatherData = JsonConvert.DeserializeObject<WeatherApiResponse>(response);

                return new WeatherInfo
                {
                    City = city,
                    Temperature = weatherData.Main.Temp,
                    Description = weatherData.Weather[0].Description,
                    Humidity = weatherData.Main.Humidity,
                    WindSpeed = weatherData.Wind.Speed,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return GenerateSampleWeather(city);
            }
        }

        public async Task<List<NewsItem>> GetNewsAsync(string category = "general")
        {
            try
            {
                if (string.IsNullOrEmpty(_newsApiKey))
                {
                    return GenerateSampleNews();
                }

                var url = $"{NEWS_API_BASE}?country=tr&category={category}&apiKey={_newsApiKey}";
                var response = await _httpClient.GetStringAsync(url);
                var newsData = JsonConvert.DeserializeObject<NewsApiResponse>(response);

                return newsData.Articles.Select(a => new NewsItem
                {
                    Title = a.Title,
                    Description = a.Description,
                    Source = a.Source.Name,
                    PublishedAt = a.PublishedAt,
                    Url = a.Url
                }).ToList();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return GenerateSampleNews();
            }
        }

        private WeatherInfo GenerateSampleWeather(string city)
        {
            // Gerçekçi örnek hava durumu verisi
            var random = new Random();
            var temp = random.Next(10, 30);
            var conditions = new[] { "Güneşli", "Parçalı Bulutlu", "Yağmurlu", "Rüzgarlı" };
            var condition = conditions[random.Next(conditions.Length)];

            return new WeatherInfo
            {
                City = city,
                Temperature = temp,
                Description = condition,
                Humidity = random.Next(40, 90),
                WindSpeed = random.Next(5, 20),
                LastUpdated = DateTime.Now
            };
        }

        private List<NewsItem> GenerateSampleNews()
        {
            // Örnek haber verileri
            return new List<NewsItem>
            {
                new NewsItem
                {
                    Title = "Teknoloji Dünyasında Yeni Gelişmeler",
                    Description = "Yapay zeka alanında önemli gelişmeler yaşanıyor...",
                    Source = "Tech Haber",
                    PublishedAt = DateTime.Now.AddHours(-2),
                    Url = "https://example.com/tech-news"
                },
                new NewsItem
                {
                    Title = "Bilim İnsanlarından Önemli Keşif",
                    Description = "Yeni araştırma sonuçları bilim dünyasını heyecanlandırdı...",
                    Source = "Bilim Portalı",
                    PublishedAt = DateTime.Now.AddHours(-3),
                    Url = "https://example.com/science-news"
                },
                new NewsItem
                {
                    Title = "Ekonomide Son Durum",
                    Description = "Piyasalarda son gelişmeler ve uzman yorumları...",
                    Source = "Ekonomi Haberleri",
                    PublishedAt = DateTime.Now.AddHours(-1),
                    Url = "https://example.com/economy-news"
                }
            };
        }

        public string FormatWeatherReport(WeatherInfo weather)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🌍 {weather.City} Hava Durumu Raporu");
            sb.AppendLine($"🌡️ Sıcaklık: {weather.Temperature:F1}°C");
            sb.AppendLine($"☁️ Durum: {weather.Description}");
            sb.AppendLine($"💧 Nem: %{weather.Humidity}");
            sb.AppendLine($"💨 Rüzgar: {weather.WindSpeed} km/s");
            sb.AppendLine($"🕒 Son Güncelleme: {weather.LastUpdated:HH:mm}");
            return sb.ToString();
        }

        public string FormatNewsReport(List<NewsItem> news)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📰 Günün Önemli Haberleri");
            sb.AppendLine();

            foreach (var item in news)
            {
                sb.AppendLine($"📌 {item.Title}");
                sb.AppendLine($"   {item.Description}");
                sb.AppendLine($"   Kaynak: {item.Source}");
                sb.AppendLine($"   Yayın: {item.PublishedAt:HH:mm}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class WeatherInfo
    {
        public string City { get; set; }
        public double Temperature { get; set; }
        public string Description { get; set; }
        public int Humidity { get; set; }
        public double WindSpeed { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class NewsItem
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Source { get; set; }
        public DateTime PublishedAt { get; set; }
        public string Url { get; set; }
    }

    // API Response Models
    public class WeatherApiResponse
    {
        public MainInfo Main { get; set; }
        public List<WeatherCondition> Weather { get; set; }
        public WindInfo Wind { get; set; }

        public class MainInfo
        {
            public double Temp { get; set; }
            public int Humidity { get; set; }
        }

        public class WeatherCondition
        {
            public string Description { get; set; }
        }

        public class WindInfo
        {
            public double Speed { get; set; }
        }
    }

    public class NewsApiResponse
    {
        public List<Article> Articles { get; set; }

        public class Article
        {
            public Source Source { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string Url { get; set; }
            public DateTime PublishedAt { get; set; }
        }

        public class Source
        {
            public string Name { get; set; }
        }
    }
}