using SnipLite.Plugin.Abstractions;

namespace SnipLite.ScrollCapture;

public sealed class ScrollCapturePlugin : IPlugin, ICaptureProvider
{
    private IDisposable? _registration;
    private string? _autoScrollPath;
    private bool _autoScroll = true;
    public string Id => "capture";
    public PluginText Title => new("Scrolling screenshot…", "长截图…", "捲動截圖…");

    public void Initialize(IPluginContext context)
    {
        _autoScrollPath = Path.Combine(context.DataDirectory, "auto-scroll.txt");
        _autoScroll = ReadAutoScroll(_autoScrollPath, context.Host);
        _registration = context.RegisterCaptureProvider(this);
    }

    private static bool ReadAutoScroll(string path, IPluginHost host)
    {
        try { return !bool.TryParse(File.ReadAllText(path), out var enabled) || enabled; }
        catch (FileNotFoundException) { return true; }
        catch (DirectoryNotFoundException) { return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            host.Log("Could not read the automatic scrolling preference.", ex);
            return true;
        }
    }

    private void SaveAutoScroll(bool enabled, IPluginHost host)
    {
        _autoScroll = enabled;
        if (_autoScrollPath is not { } path) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, enabled.ToString());
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            host.Log("Could not save the automatic scrolling preference.", ex);
        }
    }
    public void Dispose() { _registration?.Dispose(); _registration = null; }

    public async Task<PluginImage?> CaptureAsync(IPluginHost host, CancellationToken cancellationToken)
    {
        var bounds = await host.SelectRegionAsync(cancellationToken);
        if (bounds is not Rectangle region) return null;
        await Task.Delay(180, cancellationToken);
        using var session = new ScrollCaptureForm(host, region, cancellationToken, autoScroll: _autoScroll,
            saveAutoScroll: enabled => SaveAutoScroll(enabled, host));
        // A modal form disables the target when it belongs to this process.
        // Keep capturing asynchronously without disabling any desktop window.
        var completion = ObserveCompletion(session);
        session.Show();
        var result = await completion;
        if (cancellationToken.IsCancellationRequested)
        {
            result?.Dispose();
            cancellationToken.ThrowIfCancellationRequested();
        }
        return result;
    }

    internal static Task<PluginImage?> ObserveCompletion(ScrollCaptureForm session)
    {
        var completion = new TaskCompletionSource<PluginImage?>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Modeless Close disposes the form after FormClosed. Transfer ownership before disposal.
        session.FormClosed += (_, _) => completion.TrySetResult(session.TakeResult());
        return completion.Task;
    }
}
