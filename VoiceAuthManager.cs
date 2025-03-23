using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;

namespace AIAsistani
{
    public class VoiceAuthManager : IDisposable
    {
        private WaveInEvent _waveIn;
        private MemoryStream _memoryStream;
        private WaveFileWriter _writer;
        private readonly string _voiceSamplePath = "voice_sample.wav";
        private bool _isRecording;
        private List<float> _enrolledVoiceFeatures;
        private const float SIMILARITY_THRESHOLD = 0.85f; // 85% benzerlik eşiği

        // Events
        public event EventHandler<bool> VoiceVerificationComplete;
        public event EventHandler<float> AudioLevelChanged;
        public event EventHandler<Exception> ErrorOccurred;

        public bool IsInitialized { get; private set; }

        public async Task InitializeAsync()
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(44100, 1), // 44.1kHz, Mono
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += WaveIn_DataAvailable;

                if (File.Exists(_voiceSamplePath))
                {
                    await LoadEnrolledVoiceFeaturesAsync();
                }

                IsInitialized = true;
            }
            catch (Exception ex)
            {
                IsInitialized = false;
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Ses yöneticisi başlatılamadı.", ex);
            }
        }

        public async Task StartEnrollmentRecordingAsync()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Ses yöneticisi başlatılmamış.");
            }

            try
            {
                _memoryStream = new MemoryStream();
                _writer = new WaveFileWriter(_memoryStream, _waveIn.WaveFormat);
                _isRecording = true;
                _waveIn.StartRecording();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Ses kaydı başlatılamadı.", ex);
            }
        }

        public async Task StopEnrollmentRecordingAsync()
        {
            if (!_isRecording)
                return;

            try
            {
                _isRecording = false;
                _waveIn.StopRecording();

                _writer.Dispose();
                _writer = null;

                // Kaydedilen sesi analiz et ve özelliklerini çıkar
                byte[] audioData = _memoryStream.ToArray();
                _memoryStream.Dispose();
                _memoryStream = null;

                // Ses örneğini kaydet
                await File.WriteAllBytesAsync(_voiceSamplePath, audioData);

                // Ses özelliklerini çıkar ve sakla
                _enrolledVoiceFeatures = await ExtractVoiceFeaturesAsync(audioData);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Ses kaydı durdurulamadı.", ex);
            }
        }

        public async Task<bool> VerifyVoiceAsync(byte[] audioData)
        {
            try
            {
                if (_enrolledVoiceFeatures == null || _enrolledVoiceFeatures.Count == 0)
                {
                    throw new InvalidOperationException("Kayıtlı ses örneği bulunamadı.");
                }

                var currentFeatures = await ExtractVoiceFeaturesAsync(audioData);
                float similarity = CalculateVoiceSimilarity(_enrolledVoiceFeatures, currentFeatures);

                bool isVerified = similarity >= SIMILARITY_THRESHOLD;
                VoiceVerificationComplete?.Invoke(this, isVerified);

                return isVerified;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Ses doğrulama işlemi başarısız.", ex);
            }
        }

        private async Task LoadEnrolledVoiceFeaturesAsync()
        {
            try
            {
                byte[] audioData = await File.ReadAllBytesAsync(_voiceSamplePath);
                _enrolledVoiceFeatures = await ExtractVoiceFeaturesAsync(audioData);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Kayıtlı ses örneği yüklenemedi.", ex);
            }
        }

        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (_isRecording && _writer != null)
            {
                try
                {
                    _writer.Write(e.Buffer, 0, e.BytesRecorded);

                    // Ses seviyesini hesapla ve bildir
                    float maxVolume = CalculateMaxVolume(e.Buffer, e.BytesRecorded);
                    AudioLevelChanged?.Invoke(this, maxVolume);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private float CalculateMaxVolume(byte[] buffer, int bytesRecorded)
        {
            float max = 0;
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                float sample32 = sample / 32768f;
                max = Math.Max(max, Math.Abs(sample32));
            }
            return max;
        }

        private async Task<List<float>> ExtractVoiceFeaturesAsync(byte[] audioData)
        {
            // Bu basit implementasyonda, ses dalgasının temel özelliklerini çıkarıyoruz
            // Gerçek uygulamada daha gelişmiş ses analizi algoritmaları kullanılmalıdır
            List<float> features = new List<float>();

            try
            {
                using (var stream = new MemoryStream(audioData))
                using (var reader = new WaveFileReader(stream))
                {
                    // Ses dosyasını 100ms'lik parçalara böl
                    int samplesPerSegment = (int)(reader.WaveFormat.SampleRate * 0.1);
                    byte[] buffer = new byte[samplesPerSegment * 2]; // 16-bit samples
                    float[] floatBuffer = new float[samplesPerSegment];

                    while (reader.Position < reader.Length)
                    {
                    int bytesRead = reader.Read(buffer, 0, buffer.Length);
                    int samplesRead = bytesRead / 2;

                    // Convert bytes to float samples
                    for (int i = 0; i < samplesRead; i++)
                    {
                        short sample = (short)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                        floatBuffer[i] = sample / 32768f;
                    }
                        if (samplesRead == 0) break;

                        // Her segment için özellikleri hesapla
                        float energy = floatBuffer.Take(samplesRead).Select(s => s * s).Average();
                        float zeroCrossings = CountZeroCrossings(floatBuffer, samplesRead);
                        
                        features.Add(energy);
                        features.Add(zeroCrossings);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Ses özellikleri çıkarılamadı.", ex);
            }

            return features;
        }

        private int CountZeroCrossings(float[] buffer, int samplesRead)
        {
            int crossings = 0;
            for (int i = 1; i < samplesRead; i++)
            {
                if ((buffer[i] >= 0 && buffer[i - 1] < 0) || 
                    (buffer[i] < 0 && buffer[i - 1] >= 0))
                {
                    crossings++;
                }
            }
            return crossings;
        }

        private float CalculateVoiceSimilarity(List<float> features1, List<float> features2)
        {
            // Basit bir öklid mesafesi hesaplama
            // Gerçek uygulamada daha gelişmiş benzerlik algoritmaları kullanılmalıdır
            if (features1.Count != features2.Count)
                return 0;

            float sumSquaredDiff = 0;
            for (int i = 0; i < features1.Count; i++)
            {
                float diff = features1[i] - features2[i];
                sumSquaredDiff += diff * diff;
            }

            float distance = (float)Math.Sqrt(sumSquaredDiff);
            // Mesafeyi 0-1 aralığında bir benzerlik skoruna dönüştür
            return 1.0f / (1.0f + distance);
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _memoryStream?.Dispose();
            _waveIn?.Dispose();
        }
    }
}