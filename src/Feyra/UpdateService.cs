using Velopack;
using Velopack.Sources;

namespace Feyra;

internal static class UpdateService
{
    private static int _started;

    public static async Task CheckAndApplyAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0) return;
        try
        {
            var manager = new UpdateManager(new GithubSource("https://github.com/painopro123456-lang/feyra", null, false));
            if (!manager.IsInstalled) return;
            var update = await manager.CheckForUpdatesAsync();
            if (update is null) return;
            await manager.DownloadUpdatesAsync(update);
            manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception ex)
        {
            // Network/feed failures never block startup; the next launch retries.
            System.Diagnostics.Debug.WriteLine($"FEYRA update check failed: {ex}");
        }
    }
}
