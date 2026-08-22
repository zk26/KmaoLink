using System;
using System.Reflection;
using Microsoft.Win32;

namespace KmaoLink
{
    /// <summary>
    /// 开机自启管理器
    /// </summary>
    public static class AutoStartManager
    {
        private const string AppName = "KmaoLink";
        private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// 检查是否已设置开机自启
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                if (key != null)
                {
                    var value = key.GetValue(AppName);
                    return value != null;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 设置开机自启
        /// </summary>
        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
                if (key == null) return false;

                if (enable)
                {
                    string appPath = Assembly.GetExecutingAssembly().Location;
                    
                    // 如果是单文件发布，Location 可能为空
                    if (string.IsNullOrEmpty(appPath))
                    {
                        appPath = Environment.ProcessPath ?? string.Empty;
                    }

                    if (!string.IsNullOrEmpty(appPath))
                    {
                        key.SetValue(AppName, $"\"{appPath}\"");
                        return true;
                    }
                }
                else
                {
                    key.DeleteValue(AppName, false);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 切换开机自启状态
        /// </summary>
        public static bool ToggleAutoStart()
        {
            bool currentState = IsAutoStartEnabled();
            return SetAutoStart(!currentState);
        }
    }
}
