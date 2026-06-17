using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Zonit.Extensions.Ai.Anthropic;

/// <summary>
/// Default <see cref="IClaudeCliRunner"/> — launches the <c>claude</c> CLI via
/// <see cref="Process"/>. AOT/trim-safe (no reflection).
/// </summary>
/// <remarks>
/// The ambient environment is inherited so the machine's <c>claude login</c> session
/// authenticates the call; <see cref="ClaudeCliInvocation.EnvironmentOverrides"/> layers
/// on explicit auth tokens when configured. On Windows a <c>.cmd</c>/<c>.bat</c> shim
/// (npm install) is launched through <c>cmd.exe /c</c>; a native <c>claude.exe</c> and
/// Unix <c>claude</c> are launched directly.
/// </remarks>
internal sealed class ClaudeCliProcess : IClaudeCliRunner
{
    public async Task<ClaudeProcessResult> RunAsync(ClaudeCliInvocation invocation, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(invocation) };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(invocation.Timeout);
        var token = timeoutCts.Token;

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start Claude CLI process '{invocation.ExecutablePath}'.");

        // Read both pipes concurrently to avoid a full-buffer deadlock.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await WriteAndCloseStdinAsync(process, invocation.StandardInput, token).ConfigureAwait(false);

        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"Claude CLI did not complete within {invocation.Timeout}.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new ClaudeProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout,
            StandardError = stderr,
        };
    }

    public async IAsyncEnumerable<string> StreamLinesAsync(
        ClaudeCliInvocation invocation,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(invocation) };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(invocation.Timeout);
        var token = timeoutCts.Token;

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start Claude CLI process '{invocation.ExecutablePath}'.");

        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await WriteAndCloseStdinAsync(process, invocation.StandardInput, token).ConfigureAwait(false);

            while (await process.StandardOutput.ReadLineAsync(token).ConfigureAwait(false) is { } line)
                yield return line;

            await process.WaitForExitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (!process.HasExited) TryKill(process);
        }

        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"Claude CLI exited with code {process.ExitCode}." +
                (string.IsNullOrWhiteSpace(stderr) ? "" : $" {stderr.Trim()}"));
    }

    private static async Task WriteAndCloseStdinAsync(Process process, string? stdin, CancellationToken token)
    {
        try
        {
            if (!string.IsNullOrEmpty(stdin))
                await process.StandardInput.WriteAsync(stdin.AsMemory(), token).ConfigureAwait(false);
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static ProcessStartInfo CreateStartInfo(ClaudeCliInvocation invocation)
    {
        var psi = new ProcessStartInfo
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8,
        };

        if (!string.IsNullOrEmpty(invocation.WorkingDirectory))
            psi.WorkingDirectory = invocation.WorkingDirectory;

        var ext = Path.GetExtension(invocation.ExecutablePath);
        var isWindowsBatch = OperatingSystem.IsWindows()
            && (ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase));

        if (isWindowsBatch)
        {
            // CreateProcess cannot execute a .cmd/.bat directly — route through the
            // command interpreter. The high-risk payload (the user prompt) travels via
            // a positional arg through ArgumentList; cmd metacharacter escaping is
            // best-effort here, so prefer a native claude.exe when available.
            psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(invocation.ExecutablePath);
        }
        else
        {
            psi.FileName = invocation.ExecutablePath;
        }

        foreach (var arg in invocation.Arguments)
            psi.ArgumentList.Add(arg);

        if (invocation.EnvironmentOverrides is { } env)
        {
            foreach (var kv in env)
            {
                if (kv.Value is null) psi.Environment.Remove(kv.Key);
                else psi.Environment[kv.Key] = kv.Value;
            }
        }

        return psi;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort: the process may have exited between the check and the kill.
        }
    }
}
