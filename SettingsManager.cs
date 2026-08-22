using System;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace KmaoLink
{
    /// <summary>
    /// 应用设置
    /// </summary>
    public class AppSettings
    {
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public bool AutoStart { get; set; } = false;
        public bool StartMinimized { get; set; } = true;
        public int RefreshInterval { get; set; } = 5000;
        public string? LastDevice { get; set; } = null; // 最近连接的设备
        public bool AlwaysOnTop { get; set; } = true;   // 窗口置顶
    }

    /// <summary>
    /// 设置管理器 - 使用 JSON 文件存储
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KmaoLink"
        );
        
        private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");
        
        private static AppSettings? _settings;

        /// <summary>
        /// 加载设置
        /// </summary>
        public static AppSettings Load()
        {
            if (_settings != null) return _settings;

            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch
            {
                _settings = new AppSettings();
            }

            return _settings;
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public static void Save()
        {
            if (_settings == null) return;

            try
            {
                if (!Directory.Exists(SettingsDir))
                {
                    Directory.CreateDirectory(SettingsDir);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        /// <summary>
        /// 保存窗口位置
        /// </summary>
        public static void SaveWindowPosition(Point location)
        {
            var settings = Load();
            settings.WindowX = location.X;
            settings.WindowY = location.Y;
            Save();
        }

        /// <summary>
        /// 获取保存的窗口位置
        /// </summary>
        public static Point? GetSavedPosition()
        {
            var settings = Load();
            if (settings.WindowX >= 0 && settings.WindowY >= 0)
            {
                // 验证位置是否在屏幕范围内
                var screen = Screen.PrimaryScreen;
                if (screen != null)
                {
                    var bounds = screen.WorkingArea;
                    if (settings.WindowX < bounds.Right && settings.WindowY < bounds.Bottom)
                    {
                        return new Point(settings.WindowX, settings.WindowY);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 更新设置
        /// </summary>
        public static void Update(Action<AppSettings> updater)
        {
            var settings = Load();
            updater(settings);
            Save();
        }
    }
}
