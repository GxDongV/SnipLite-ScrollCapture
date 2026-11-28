using System.Runtime.InteropServices;

namespace SnipLite.ScrollCapture;

/// <summary>Desktop windows shown beside a live capture must paint only inside their own bounds.</summary>
internal abstract class ScrollCaptureOverlay : Form
{
    private const int DropShadowClassStyle = 0x00020000;
    private const int NonClientRenderingPolicy = 2;
    private const int NonClientRenderingDisabled = 1;
    private const int CornerPreference = 33;
    private const int DoNotRound = 1;
    private static readonly bool SupportsCornerPreference = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);
    private bool _cornerUpdateQueued;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle &= ~DropShadowClassStyle;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var policy = NonClientRenderingDisabled;
        DwmSetWindowAttribute(Handle, NonClientRenderingPolicy, in policy, sizeof(int));
        if (SupportsCornerPreference) RemoveNativeCorners();
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        if (!SupportsCornerPreference || !IsHandleCreated || Disposing || _cornerUpdateQueued) return;
        // The host invalidates the form and then reapplies its native window theme.
        // Restore our corner preference after that synchronous theme update.
        _cornerUpdateQueued = true;
        BeginInvoke(() =>
        {
            _cornerUpdateQueued = false;
            if (IsHandleCreated && !Disposing) RemoveNativeCorners();
        });
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _cornerUpdateQueued = false;
        base.OnHandleDestroyed(e);
    }

    private void RemoveNativeCorners()
    {
        if (DwmGetWindowAttribute(Handle, CornerPreference, out int current, sizeof(int)) == 0 && current == DoNotRound)
            return;
        var preference = DoNotRound;
        DwmSetWindowAttribute(Handle, CornerPreference, in preference, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, in int value, int size);
}
