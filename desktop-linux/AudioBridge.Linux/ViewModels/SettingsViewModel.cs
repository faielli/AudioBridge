using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AudioBridge.Desktop.Models;
using AudioBridge.Desktop.Services;

namespace AudioBridge.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private bool _isLoading;

    public AppSettings AppSettings { get; private set; }

    public ObservableCollection<AudioDeviceInfo> AudioDevices { get; } = [];
    public int[] SampleRateOptions => [44100, 48000];
    public int[] ValidFrameSizes => [10, 20, 40, 60];

    public Func<Task<bool>>? ConfirmResetAsync { get; set; }

    [ObservableProperty]
    private AudioDeviceInfo? _selectedDeviceInfo;

    partial void OnSelectedDeviceInfoChanged(AudioDeviceInfo? value)
    {
        if (value != null && !_isLoading)
            Save();
    }

    [ObservableProperty]
    private int _selectedSampleRate = 48000;

    [ObservableProperty]
    private bool _isStereo = true;

    [ObservableProperty]
    private int _bitrateKbps = 192;

    [ObservableProperty]
    private int _selectedFrameSizeMs = 20;

    [ObservableProperty]
    private int _udpPort = 54320;

    [ObservableProperty]
    private int _tcpControlPort = 54321;

    [ObservableProperty]
    private int _networkBufferMs = 50;

    [ObservableProperty]
    private bool _mdnsEnabled = true;

    [ObservableProperty]
    private string _manualIp = "";

    [ObservableProperty]
    private int _jitterBufferFrames = 3;

    [ObservableProperty]
    private bool _autoStartWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private int _selectedProfile;

    public string[] Profiles => ["Musica", "Film", "Gaming"];

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        AppSettings = settingsService.Load();
        _isLoading = true;
        LoadFromSettings();
        EnumerateAudioDevices();
        _isLoading = false;
        Save();
    }

    private void EnumerateAudioDevices()
    {
        AudioDevices.Clear();
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pactl",
                Arguments = "list sinks",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("pactl non trovato");

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            string currentName = "";
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("Name:"))
                    currentName = t["Name:".Length..].Trim();
                else if (t.StartsWith("Description:") && currentName.Length > 0)
                {
                    AudioDevices.Add(new AudioDeviceInfo
                    {
                        Name = currentName,
                        Description = t["Description:".Length..].Trim()
                    });
                    currentName = "";
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] pactl fallito: {ex.Message}");
        }

        if (AudioDevices.Count == 0)
            AudioDevices.Add(new AudioDeviceInfo { Name = "", Description = "Default (fallback)" });

        var saved = AppSettings.AudioDeviceName;
        var match = AudioDevices.FirstOrDefault(d => d.Name == saved);
        SelectedDeviceInfo = match ?? AudioDevices[0];
    }

    private void LoadFromSettings()
    {
        SelectedSampleRate = AppSettings.SampleRate;
        IsStereo = AppSettings.IsStereo;
        BitrateKbps = AppSettings.Bitrate / 1000;
        SelectedFrameSizeMs = AppSettings.FrameSizeMs;
        SelectedProfile = AppSettings.SelectedProfile;
        UdpPort = AppSettings.UdpPort;
        TcpControlPort = AppSettings.TcpControlPort;
        NetworkBufferMs = AppSettings.NetworkBufferMs;
        MdnsEnabled = AppSettings.MdnsEnabled;
        ManualIp = AppSettings.ManualIp;
        JitterBufferFrames = AppSettings.JitterBufferFrames;
        AutoStartWithWindows = AppSettings.AutoStartWithWindows;
        MinimizeToTray = AppSettings.MinimizeToTray;
    }

    partial void OnSelectedSampleRateChanged(int value) { if (!_isLoading) Save(); }
    partial void OnIsStereoChanged(bool value) { if (!_isLoading) Save(); }
    partial void OnBitrateKbpsChanged(int value) { if (!_isLoading) Save(); }
    partial void OnSelectedFrameSizeMsChanged(int value) { if (!_isLoading) Save(); }
    partial void OnSelectedProfileChanged(int value) { if (!_isLoading) Save(); }
    partial void OnUdpPortChanged(int value) { if (!_isLoading) Save(); }
    partial void OnTcpControlPortChanged(int value) { if (!_isLoading) Save(); }
    partial void OnNetworkBufferMsChanged(int value) { if (!_isLoading) Save(); }
    partial void OnMdnsEnabledChanged(bool value) { if (!_isLoading) Save(); }
    partial void OnManualIpChanged(string value) { if (!_isLoading) Save(); }
    partial void OnJitterBufferFramesChanged(int value) { if (!_isLoading) Save(); }
    partial void OnAutoStartWithWindowsChanged(bool value)
    {
        if (!_isLoading)
        {
            ApplyAutoStart(value);
            Save();
        }
    }

    partial void OnMinimizeToTrayChanged(bool value) { if (!_isLoading) Save(); }

    private void Save()
    {
        AppSettings.AudioDeviceName = SelectedDeviceInfo?.Name ?? "";
        AppSettings.SampleRate = SelectedSampleRate;
        AppSettings.SetStereo(IsStereo);
        AppSettings.Bitrate = BitrateKbps * 1000;
        AppSettings.FrameSizeMs = SelectedFrameSizeMs;
        AppSettings.SelectedProfile = SelectedProfile;
        AppSettings.UdpPort = UdpPort;
        AppSettings.TcpControlPort = TcpControlPort;
        AppSettings.NetworkBufferMs = NetworkBufferMs;
        AppSettings.MdnsEnabled = MdnsEnabled;
        AppSettings.ManualIp = ManualIp;
        AppSettings.JitterBufferFrames = JitterBufferFrames;
        AppSettings.AutoStartWithWindows = AutoStartWithWindows;
        AppSettings.MinimizeToTray = MinimizeToTray;

        _settingsService.Save(AppSettings);
    }

    [RelayCommand]
    private async Task ResetToDefaults()
    {
        if (ConfirmResetAsync != null)
        {
            var confirmed = await ConfirmResetAsync();
            if (!confirmed) return;
        }

        AppSettings = AppSettings.CreateDefault();
        _isLoading = true;
        LoadFromSettings();
        _isLoading = false;
        Save();
        Console.WriteLine("[SettingsViewModel] Reset eseguito");
    }

    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("[Settings] Auto-start su Linux non ancora implementato (Step 6)");
                return;
            }
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (exePath != null)
                    key.SetValue("AudioBridge", $"\"{exePath}\"");
            }
            else
            {
                if (key.GetValue("AudioBridge") != null)
                    key.DeleteValue("AudioBridge");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Errore auto-avvio: {ex.Message}");
        }
    }
}
