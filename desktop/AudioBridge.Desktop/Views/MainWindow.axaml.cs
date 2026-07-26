using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using AudioBridge.Desktop.Services;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;

namespace AudioBridge.Desktop.Views;

public partial class MainWindow : Window
{
    private TrayIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        var settings = new SettingsService().Load();
        if (settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            ShowTrayIcon();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    private void ShowTrayIcon()
    {
        if (_trayIcon != null) return;

        try
        {
            var assets = AssetLoader.Open(new Uri("avares://AudioBridge.Desktop/Assets/audiobridge.ico"));
            var icon = new WindowIcon(assets);

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem("Mostra AudioBridge");
            showItem.Command = new RelayCommand(RestoreWindow);
            menu.Items.Add(showItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            var exitItem = new NativeMenuItem("Esci");
            exitItem.Command = new RelayCommand(ExitApp);
            menu.Items.Add(exitItem);

            _trayIcon = new TrayIcon
            {
                Icon = icon,
                ToolTipText = "AudioBridge",
                Menu = menu,
                IsVisible = true,
            };

            _trayIcon.Clicked += OnTrayClicked;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrayIcon] Errore creazione: {ex.Message}");
        }
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        Closing -= OnWindowClosing;
        Close();
    }

    private void OnTrayClicked(object? sender, EventArgs e)
    {
        RestoreWindow();
    }
}
