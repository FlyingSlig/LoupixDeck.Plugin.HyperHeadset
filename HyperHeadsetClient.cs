using System.Diagnostics;
using System.Text.Json;

namespace LoupixDeck.Plugin.HyperHeadset;

/// <summary>
/// Runs <c>hyper_headset_cli --json</c> once and turns its output into a
/// <see cref="HeadsetStatus"/>. A read takes a few seconds because the CLI opens the HID
/// device, so callers must not run it on a polled render path.
/// </summary>
internal sealed class HyperHeadsetClient
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(20);

    private readonly string _executable;

    public HyperHeadsetClient(string executable) => _executable = ExpandHome(executable);

    public async Task<HeadsetStatus> ReadAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new(_executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--json");
        // The CLI checks for its udev rules on startup and may prompt; the host process must
        // never block on an interactive prompt.
        startInfo.Environment["HYPERHEADSET_NO_AUTO_UDEV"] = "1";

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return HeadsetStatus.Unavailable($"Could not run '{_executable}': {ex.Message}");
        }

        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ReadTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            return HeadsetStatus.Unavailable("hyper_headset_cli timed out");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string output = await stdout.ConfigureAwait(false);
        string error = await stderr.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(error)
                ? $"hyper_headset_cli exited with code {process.ExitCode}"
                : error.Trim();
            return HeadsetStatus.Unavailable(message);
        }

        // The CLI may print notices to stdout before the JSON document (for example the
        // "Automatic udev rules check disabled" banner). Take the object between the first
        // '{' and the last '}' instead of trusting the whole stream.
        if (!TryExtractJson(output, out string json))
            return HeadsetStatus.Unavailable($"Unexpected hyper_headset_cli output: {Excerpt(output)}");

        try
        {
            return HeadsetStatus.Parse(json);
        }
        catch (JsonException ex)
        {
            return HeadsetStatus.Unavailable($"Unexpected hyper_headset_cli output: {ex.Message}");
        }
    }

    private static bool TryExtractJson(string output, out string json)
    {
        int start = output.IndexOf('{');
        int end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            json = string.Empty;
            return false;
        }

        json = output[start..(end + 1)];
        return true;
    }

    private static string Excerpt(string output)
    {
        string text = output.Trim();
        if (text.Length == 0)
            return "<empty>";
        int newline = text.IndexOf('\n');
        if (newline >= 0)
            text = text[..newline].Trim();
        return text.Length <= 120 ? text : text[..120] + "…";
    }

    private static string ExpandHome(string path)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal))
            return path;

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path == "~" ? home : Path.Combine(home, path[2..]);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process already ended between the check and the kill.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No permission or already gone — nothing useful left to do.
        }
    }
}
