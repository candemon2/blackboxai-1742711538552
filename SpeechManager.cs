using System;
using System.Speech.Synthesis;
using System.Speech.Recognition;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using System.IO;

namespace AIAsistani
{
    public class SpeechManager : IDisposable
    {
        private readonly SpeechSynthesizer _synthesizer;
        private readonly SpeechRecognitionEngine _recognizer;
        private readonly VoiceAuthManager _voiceAuthManager;
        private readonly WaveInEvent _waveIn;
        private MemoryStream _audioStream;
        private bool _isListening;
        private readonly object _lockObject = new object();

        // Events
        public event EventHandler<string> SpeechRecognized;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler<bool> ListeningStateChanged;

        public bool IsInitialized { get; private set; }

        public SpeechManager(VoiceAuthManager voiceAuthManager)
        {
            _voiceAuthManager = voiceAuthManager;
            _synthesizer = new SpeechSynthesizer();
            _recognizer = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("tr-TR"));
            
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(44100, 1),
                BufferMilliseconds = 50
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Ses sentezleyiciyi yapılandır
                _synthesizer.SetOutputToDefaultAudioDevice();
                _synthesizer.Rate = 0; // Normal hız
                _synthesizer.Volume = 100;

                // Konuşma tanıma motorunu yapılandır
                ConfigureRecognizer();

                // Ses girişini yapılandır
                _recognizer.SetInputToDefaultAudioDevice();

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Konuşma yöneticisi başlatılamadı.", ex);
            }
        }

        private void ConfigureRecognizer()
        {
            try
            {
                // Temel komutlar için dilbilgisi kuralları oluştur
                var commands = new Choices();
                commands.Add(new string[] {
                    "merhaba",
                    "günaydın",
                    "iyi akşamlar",
                    "hava durumu",
                    "haberleri göster",
                    "günlük planım",
                    "yeni hatırlatma ekle",
                    "hatırlatmaları göster",
                    "durdur",
                    "devam et",
                    "kapat"
                });

                var grammarBuilder = new GrammarBuilder();
                grammarBuilder.Culture = new System.Globalization.CultureInfo("tr-TR");
                grammarBuilder.Append(commands);

                var grammar = new Grammar(grammarBuilder);
                _recognizer.LoadGrammar(grammar);

                // Serbest konuşma için dikte modunu ekle
                _recognizer.LoadGrammar(new DictationGrammar());

                // Olayları bağla
                _recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
                _recognizer.SpeechHypothesized += Recognizer_SpeechHypothesized;
                _recognizer.SpeechRecognitionRejected += Recognizer_SpeechRecognitionRejected;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Konuşma tanıma motoru yapılandırılamadı.", ex);
            }
        }

        public void Speak(string text)
        {
            try
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("Konuşma yöneticisi başlatılmamış.");

                _synthesizer.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void StartListening()
        {
            try
            {
                if (!IsInitialized)
                    throw new InvalidOperationException("Konuşma yöneticisi başlatılmamış.");

                lock (_lockObject)
                {
                    if (!_isListening)
                    {
                        _isListening = true;
                        _audioStream = new MemoryStream();
                        _waveIn.StartRecording();
                        _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                        ListeningStateChanged?.Invoke(this, true);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void StopListening()
        {
            try
            {
                lock (_lockObject)
                {
                    if (_isListening)
                    {
                        _isListening = false;
                        _waveIn.StopRecording();
                        _recognizer.RecognizeAsyncStop();
                        _audioStream?.Dispose();
                        _audioStream = null;
                        ListeningStateChanged?.Invoke(this, false);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private async void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            try
            {
                if (_isListening && _audioStream != null)
                {
                    // Ses verisini kaydet
                    await _audioStream.WriteAsync(e.Buffer, 0, e.BytesRecorded);

                    // Belirli bir süre sonra ses doğrulaması yap
                    if (_audioStream.Length > 44100 * 2) // ~2 saniyelik ses
                    {
                        var audioData = _audioStream.ToArray();
                        bool isAuthorized = await _voiceAuthManager.VerifyVoiceAsync(audioData);

                        if (!isAuthorized)
                        {
                            // Yetkisiz kullanıcı tespit edildi
                            Speak("Uyarı! Yetkisiz kullanıcı tespit edildi.");
                            // Ek güvenlik önlemleri burada uygulanabilir
                        }

                        // Yeni kayıt için stream'i temizle
                        _audioStream.SetLength(0);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private void Recognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            try
            {
                if (e.Result.Confidence > 0.8) // Yüksek güvenilirlik eşiği
                {
                    SpeechRecognized?.Invoke(this, e.Result.Text);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private void Recognizer_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            // Konuşma tanıma devam ediyor
            // Gerekirse UI güncellemesi için kullanılabilir
        }

        private void Recognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            // Konuşma tanıma başarısız oldu
            // Gerekirse kullanıcıya geri bildirim verilebilir
        }

        public void Dispose()
        {
            StopListening();
            _synthesizer?.Dispose();
            _recognizer?.Dispose();
            _waveIn?.Dispose();
            _audioStream?.Dispose();
        }
    }
}