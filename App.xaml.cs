using System;
using System.Windows;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace AIAsistani
{
    public partial class App : Application
    {
        private TrayIconManager _trayIconManager;
        private SpeechManager _speechManager;
        private VoiceAuthManager _voiceAuthManager;
        private ChatGPTIntegration _chatGPTIntegration;
        private DailyPlanManager _dailyPlanManager;
        private APIManager _apiManager;
        private dynamic _config;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Global hata yönetimi
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

                // Yapılandırmayı yükle
                await LoadConfigurationAsync();

                // Yöneticileri başlat
                await InitializeManagersAsync();

                // Ses kaydı kontrolü
                if (!IsVoiceEnrolled())
                {
                    await ShowEnrollmentWindowAsync();
                }
                else
                {
                    ShowMainWindow();
                }

                // Otomatik başlatmayı ayarla
                SetupAutoStart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uygulama başlatılırken bir hata oluştu: {ex.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private async Task LoadConfigurationAsync()
        {
            try
            {
                string configPath = "config.json";
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("Yapılandırma dosyası bulunamadı. Varsayılan ayarlar kullanılacak.", 
                        "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CreateDefaultConfig(configPath);
                }

                string jsonContent = await File.ReadAllTextAsync(configPath);
                _config = JsonConvert.DeserializeObject(jsonContent);
            }
            catch (Exception ex)
            {
                throw new Exception($"Yapılandırma yüklenirken hata oluştu: {ex.Message}");
            }
        }

        private void CreateDefaultConfig(string path)
        {
            var defaultConfig = new
            {
                autoStart = true,
                greeting = "Günaydın Patron",
                userPreferences = new
                {
                    language = "tr",
                    voice = "Jarvis-esque",
                    theme = "modern"
                },
                voiceAuthentication = new
                {
                    enrolled = false,
                    voiceSample = (string)null
                },
                apiKeys = new
                {
                    weather = "",
                    news = "",
                    gpt = ""
                }
            };

            string jsonContent = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
            File.WriteAllText(path, jsonContent);
        }

        private async Task InitializeManagersAsync()
        {
            try
            {
                _voiceAuthManager = new VoiceAuthManager();
                _speechManager = new SpeechManager(_voiceAuthManager);
                _trayIconManager = new TrayIconManager();
                _chatGPTIntegration = new ChatGPTIntegration(_config.apiKeys.gpt?.ToString());
                _dailyPlanManager = new DailyPlanManager();
                _apiManager = new APIManager(_config.apiKeys);

                // Yöneticileri başlat
                await Task.WhenAll(
                    _speechManager.InitializeAsync(),
                    _voiceAuthManager.InitializeAsync()
                );
            }
            catch (Exception ex)
            {
                throw new Exception($"Yöneticiler başlatılırken hata oluştu: {ex.Message}");
            }
        }

        private bool IsVoiceEnrolled()
        {
            return _config.voiceAuthentication.enrolled;
        }

        private async Task ShowEnrollmentWindowAsync()
        {
            var enrollmentWindow = new EnrollmentWindow(_voiceAuthManager);
            enrollmentWindow.EnrollmentCompleted += async (s, e) =>
            {
                _config.voiceAuthentication.enrolled = true;
                await SaveConfigurationAsync();
                ShowMainWindow();
                enrollmentWindow.Close();
            };
            enrollmentWindow.Show();
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow(
                _trayIconManager,
                _speechManager,
                _voiceAuthManager,
                _chatGPTIntegration,
                _dailyPlanManager,
                _apiManager
            );
            mainWindow.Show();

            // Karşılama mesajını söyle
            _speechManager.Speak(_config.greeting.ToString());
        }

        private async Task SaveConfigurationAsync()
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(_config, Formatting.Indented);
                await File.WriteAllTextAsync("config.json", jsonContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yapılandırma kaydedilirken hata oluştu: {ex.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupAutoStart()
        {
            try
            {
                if (_config.autoStart)
                {
                    string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    string keyName = "AIAsistani";
                    
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                    {
                        if (key != null)
                        {
                            key.SetValue(keyName, appPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Otomatik başlatma ayarlanırken hata oluştu: {ex.Message}", 
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogError("Kritik Hata", e.ExceptionObject as Exception);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogError("UI Thread Hatası", e.Exception);
            e.Handled = true;
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError("Task Hatası", e.Exception);
            e.SetObserved();
        }

        private void LogError(string type, Exception ex)
        {
            try
            {
                string logPath = "error.log";
                string logMessage = $"[{DateTime.Now}] {type}: {ex?.Message}\n{ex?.StackTrace}\n\n";
                File.AppendAllText(logPath, logMessage);

                MessageBox.Show($"Bir hata oluştu ve kaydedildi: {ex?.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Logging failed, show message box as last resort
                MessageBox.Show($"Kritik bir hata oluştu: {ex?.Message}", 
                    "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Yöneticileri temizle
            _trayIconManager?.Dispose();
            _speechManager?.Dispose();
            _voiceAuthManager?.Dispose();

            base.OnExit(e);
        }
    }
}