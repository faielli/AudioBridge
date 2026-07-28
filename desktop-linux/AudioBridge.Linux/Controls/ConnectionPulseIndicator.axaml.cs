using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AudioBridge.Desktop.Controls;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected
}

public partial class ConnectionPulseIndicator : UserControl
{
    public static readonly StyledProperty<ConnectionState> StateProperty =
        AvaloniaProperty.Register<ConnectionPulseIndicator, ConnectionState>(
            nameof(State), ConnectionState.Disconnected, coerce: CoerceState);

    public ConnectionState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private static ConnectionState CoerceState(AvaloniaObject sender, ConnectionState value)
    {
        if (sender is ConnectionPulseIndicator control)
        {
            control.OnStateChanged(value);
        }
        return value;
    }

    public ConnectionPulseIndicator()
    {
        InitializeComponent();
    }

    private void OnStateChanged(ConnectionState newState)
    {
        if (StateTooltip != null)
        {
            StateTooltip.Content = newState switch
            {
                ConnectionState.Disconnected => "Disconnesso",
                ConnectionState.Connecting => "Connessione in corso...",
                ConnectionState.Connected => "Connesso",
                _ => "Sconosciuto"
            };
        }

        // Update visual elements directly using TryGetResource
        if (InnerDot != null && OuterRing != null && MiddleRing != null)
        {
            var disabledBrush = (IBrush)Resources["DisabledBrush"]!;
            var accentPrimary = (IBrush)Resources["AccentPrimaryBrush"]!;
            var accentSecondary = (IBrush)Resources["AccentSecondaryBrush"]!;

            switch (newState)
            {
                case ConnectionState.Disconnected:
                    InnerDot.Fill = disabledBrush;
                    OuterRing.IsVisible = false;
                    MiddleRing.IsVisible = false;
                    break;
                case ConnectionState.Connecting:
                    InnerDot.Fill = accentPrimary;
                    OuterRing.IsVisible = true;
                    MiddleRing.IsVisible = true;
                    OuterRing.Stroke = accentPrimary;
                    MiddleRing.Stroke = accentPrimary;
                    break;
                case ConnectionState.Connected:
                    InnerDot.Fill = accentSecondary;
                    OuterRing.IsVisible = false;
                    MiddleRing.IsVisible = false;
                    break;
            }
        }

        // Update pseudo-classes for any external styles
        PseudoClasses.Set(":disconnected", newState == ConnectionState.Disconnected);
        PseudoClasses.Set(":connecting", newState == ConnectionState.Connecting);
        PseudoClasses.Set(":connected", newState == ConnectionState.Connected);
    }
}