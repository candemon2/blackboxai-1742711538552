using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Windows;

namespace AIAsistani
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon _trayIcon;
        private bool _isActive = true;

        // Events
        public event EventHandler<bool> StateChanged;
        public event EventHandler CloseRequested;
        public event EventHandler<Exception> ErrorOccurred;

        public bool IsActive
        {
            get => _isActive;
            private set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    StateChanged?.Invoke(this, _isActive);
                    UpdateContextMenu();
                }
            }
        }

        public TrayIconManager()
        {
            try
            {
                InitializeTrayIcon();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                throw new Exception("Sistem tepsisi simgesi oluşturulamadı.", ex);
            }
        }

        private void InitializeTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Visible = true,
                Text = "AI Asistanı"
            };

            // Icon dosyasını yükle
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "assistant_icon.ico");
            if (File.Exists(iconPath))
            {
                _trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Varsayılan sistem ikonu
                _trayIcon.Icon = SystemIcons.Application;
            }

            // Çift tıklama olayını ayarla
            _trayIcon.DoubleClick += TrayIcon_DoubleClick;

            // Bağlam menüsünü oluştur
            UpdateContextMenu();
        }

        private void UpdateContextMenu()
        {
            try
            {
                var menu = new ContextMenuStrip();

                if (IsActive)
                {
                    // Aktif durum menüsü
                    menu.Items.Add("Durdur", null, (s, e) => 
                    {
                        IsActive = false;
                    });
                }
                else
                {
                    // Durdurulmuş durum menüsü
                    menu.Items.Add("Devam Et", null, (s, e) => 
                    {
                        IsActive = true;
                    });
                }

                // Ayırıcı çizgi
                menu.Items.Add(new ToolStripSeparator());

                // Kapat seçeneği
                menu.Items.Add("Kapat", null, (s, e) => 
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                });

                // Menü öğelerinin stilini ayarla
                foreach (ToolStripItem item in menu.Items)
                {
                    if (item is ToolStripMenuItem menuItem)
                    {
                        menuItem.Font = new Font("Segoe UI", 9F);
                        menuItem.ForeColor = System.Drawing.Color.FromArgb(51, 51, 51);
                    }
                }

                // Mevcut menüyü güncelle
                _trayIcon.ContextMenuStrip = menu;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                // Çift tıklamada durumu değiştir
                IsActive = !IsActive;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            try
            {
                _trayIcon.ShowBalloonTip(3000, title, message, icon);
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                    _trayIcon = null;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
            }
        }
    }
}