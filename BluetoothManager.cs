using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WinBt = Windows.Devices.Bluetooth;
using WinEnum = Windows.Devices.Enumeration;
using WinRadio = Windows.Devices.Radios;

namespace KmaoLink
{
    /// <summary>
    /// 蓝牙设备信息
    /// </summary>
    public class BluetoothDevice
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty; // 存 DeviceId
        public bool IsConnected { get; set; }
        public bool IsPaired { get; set; }
        public string DeviceType { get; set; } = string.Empty;
    }

    /// <summary>
    /// 蓝牙管理器
    /// 开关: WinRT Radio API (真正的系统蓝牙开关)
    /// 列表: WinRT DeviceInformation
    /// 删除: WinRT UnpairAsync
    /// 连接/断开: PnP (需管理员)
    /// </summary>
    public class BluetoothManager
    {
        private bool? _radioAvailable = null;

        /// <summary>
        /// 检测 Radio API 是否可用（非打包应用可能无权限）
        /// </summary>
        private async Task<bool> IsRadioApiAvailableAsync()
        {
            if (_radioAvailable.HasValue) return _radioAvailable.Value;
            try
            {
                var access = await WinRadio.Radio.RequestAccessAsync();
                _radioAvailable = access == WinRadio.RadioAccessStatus.Allowed;
            }
            catch
            {
                _radioAvailable = false;
            }
            return _radioAvailable.Value;
        }

        /// <summary>
        /// 获取蓝牙开关状态
        /// </summary>
        public async Task<bool> IsBluetoothEnabledAsync()
        {
            try
            {
                if (await IsRadioApiAvailableAsync())
                {
                    var radios = await WinRadio.Radio.GetRadiosAsync();
                    var bt = radios.FirstOrDefault(r => r.Kind == WinRadio.RadioKind.Bluetooth);
                    if (bt != null) return bt.State == WinRadio.RadioState.On;
                }
            }
            catch { }

            // 回退: 检查蓝牙适配器是否启用
            return await IsAdapterEnabledAsync();
        }

        /// <summary>
        /// 开关蓝牙（真正控制系统蓝牙）
        /// </summary>
        public async Task<bool> SetBluetoothStateAsync(bool enable)
        {
            // 方案1: WinRT Radio API
            try
            {
                if (await IsRadioApiAvailableAsync())
                {
                    var radios = await WinRadio.Radio.GetRadiosAsync();
                    var btRadios = radios.Where(r => r.Kind == WinRadio.RadioKind.Bluetooth).ToList();
                    if (btRadios.Count > 0)
                    {
                        foreach (var r in btRadios)
                            await r.SetStateAsync(enable ? WinRadio.RadioState.On : WinRadio.RadioState.Off);
                        return true;
                    }
                }
            }
            catch { }

            // 方案2: PnP 禁用/启用适配器（需管理员）
            return await SetAdapterEnabledAsync(enable);
        }

        /// <summary>
        /// 获取已配对设备列表
        /// </summary>
        public async Task<List<BluetoothDevice>> GetPairedDevicesAsync()
        {
            var list = new List<BluetoothDevice>();
            try
            {
                var devices = await WinEnum.DeviceInformation.FindAllAsync(
                    WinBt.BluetoothDevice.GetDeviceSelectorFromPairingState(true));

                foreach (var info in devices)
                {
                    bool connected = false;
                    string typeName = "";
                    try
                    {
                        var bt = await WinBt.BluetoothDevice.FromIdAsync(info.Id);
                        if (bt != null)
                        {
                            connected = bt.ConnectionStatus == WinBt.BluetoothConnectionStatus.Connected;
                            typeName = bt.ClassOfDevice?.MajorClass.ToString() ?? "";
                        }
                    }
                    catch { }

                    list.Add(new BluetoothDevice
                    {
                        Name = string.IsNullOrEmpty(info.Name) ? "未知设备" : info.Name,
                        Address = info.Id,
                        IsPaired = true,
                        IsConnected = connected,
                        DeviceType = typeName
                    });
                }
            }
            catch { }

            // 已连接的排前面
            return list.OrderByDescending(d => d.IsConnected).ToList();
        }

        /// <summary>
        /// 删除（取消配对）设备
        /// </summary>
        public async Task<bool> UnpairDeviceAsync(string deviceId)
        {
            try
            {
                var bt = await WinBt.BluetoothDevice.FromIdAsync(deviceId);
                if (bt == null) return false;

                var result = await bt.DeviceInformation.Pairing.UnpairAsync();
                return result.Status == WinEnum.DeviceUnpairingResultStatus.Unpaired ||
                       result.Status == WinEnum.DeviceUnpairingResultStatus.AlreadyUnpaired;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 连接设备（PnP 方式，需管理员）
        /// </summary>
        public async Task<bool> ConnectDeviceAsync(string deviceName)
        {
            return await RunPnpCommand("Enable", deviceName);
        }

        /// <summary>
        /// 断开设备（PnP 方式，需管理员）
        /// </summary>
        public async Task<bool> DisconnectDeviceAsync(string deviceName)
        {
            return await RunPnpCommand("Disable", deviceName);
        }

        #region PnP 后备方案

        private async Task<bool> IsAdapterEnabledAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var p = RunPowerShell(
                        "Get-PnpDevice -Class Bluetooth | Where-Object { $_.FriendlyName -like '*Bluetooth*' -or $_.Class -eq 'Bluetooth' } | Select-Object -First 1 -ExpandProperty Status");
                    return p.Contains("OK");
                }
                catch { return false; }
            });
        }

        private async Task<bool> SetAdapterEnabledAsync(bool enable)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string action = enable ? "Enable" : "Disable";
                    string script = $@"
                        $adapter = Get-PnpDevice | Where-Object {{ $_.Class -eq 'Bluetooth' }} | Select-Object -First 1
                        if ($adapter) {{
                            {action}-PnpDevice -InstanceId $adapter.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
                            'OK'
                        }}";
                    var output = RunPowerShell(script);
                    return output.Contains("OK");
                }
                catch { return false; }
            });
        }

        private async Task<bool> RunPnpCommand(string action, string deviceName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string safeName = deviceName.Replace("'", "''");
                    string script = $@"
                        $device = Get-PnpDevice | Where-Object {{ $_.FriendlyName -eq '{safeName}' -and $_.Class -eq 'Bluetooth' }}
                        if ($device) {{
                            {action}-PnpDevice -InstanceId $device.InstanceId -Confirm:$false -ErrorAction SilentlyContinue
                            'OK'
                        }}";
                    var output = RunPowerShell(script);
                    return output.Contains("OK");
                }
                catch { return false; }
            });
        }

        private string RunPowerShell(string script)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"{script}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            return output;
        }

        #endregion
    }
}
