using Avalonia.Controls;
using Avalonia.Threading;
using AudioBridge.Desktop.ViewModels;
using System.Threading.Tasks;

namespace AudioBridge.Desktop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, System.EventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.ConfirmResetAsync == null)
            vm.ConfirmResetAsync = ShowResetConfirmation;
    }

    private async Task<bool> ShowResetConfirmation()
    {
        var dialog = new Window
        {
            Title = "Conferma ripristino",
            Width = 380,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = this.Background,
        };

        var result = false;

        var stack = new StackPanel { Spacing = 20, Margin = new(24) };
        stack.Children.Add(new TextBlock
        {
            Classes = { "body" },
            Text = "Vuoi ripristinare tutte le impostazioni ai valori predefiniti? Le modifiche attuali andranno perse.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var btnGrid = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 12,
        };

        var cancelBtn = new Button { Content = "Annulla", Width = 100, Height = 36 };
        cancelBtn.Classes.Add("ghost");
        cancelBtn.Click += (_, _) => { result = false; dialog.Close(); };

        var confirmBtn = new Button { Content = "Conferma", Width = 100, Height = 36 };
        confirmBtn.Classes.Add("warning");
        confirmBtn.Click += (_, _) => { result = true; dialog.Close(); };

        btnGrid.Children.Add(cancelBtn);
        btnGrid.Children.Add(confirmBtn);
        stack.Children.Add(btnGrid);
        dialog.Content = stack;

        await dialog.ShowDialog(this);
        return result;
    }
}
