using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Zonit.Extensions.Ai;

/// <summary>
/// Executes a batch of tool calls produced by the model in a single iteration,
/// honoring <see cref="AiAgentOptions.MaxParallelToolCalls"/>, per-call timeouts
/// and the configured <see cref="ToolExceptionPolicy"/>.
/// </summary>
/// <remarks>
/// Preserves the request order in the resulting <see cref="ToolResult"/> list —
/// providers typically rely on position-based correlation (though we also carry
/// <see cref="PendingToolCall.Id"/> for providers that require explicit IDs).
/// </remarks>
internal sealed class ToolExecutor
{
    private readonly IReadOnlyDictionary<string, ITool> _byName;
    private readonly int _maxParallel;
    private readonly TimeSpan _perCallTimeout;
    private readonly ToolExceptionPolicy _exceptionPolicy;
    private readonly Func<ToolInvocation, CancellationToken, ValueTask<bool>>? _onToolCall;
    private readonly ILogger _logger;

    public ToolExecutor(
        IReadOnlyList<ITool> tools,
        int maxParallel,
        TimeSpan perCallTimeout,
        ToolExceptionPolicy exceptionPolicy,
        Func<ToolInvocation, CancellationToken, ValueTask<bool>>? onToolCall,
        ILogger logger)
    {
        _byName = tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        _maxParallel = Math.Max(1, maxParallel);
        _perCallTimeout = perCallTimeout;
        _exceptionPolicy = exceptionPolicy;
        _onToolCall = onToolCall;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ToolResult>> ExecuteAsync(
        int iteration,
        IReadOnlyList<PendingToolCall> calls,
        List<ToolInvocation> invocationSink,
        CancellationToken cancellationToken)
    {
        if (calls.Count == 0)
            return Array.Empty<ToolResult>();

        // Pre-allocate slots to preserve call order.
        var results = new ToolResult[calls.Count];
        var invocations = new ToolInvocation[calls.Count];

        using var semaphore = new SemaphoreSlim(Math.Min(_maxParallel, calls.Count));
        var tasks = new Task[calls.Count];

        for (var i = 0; i < calls.Count; i++)
        {
            var idx = i;
            var call = calls[i];

            tasks[idx] = Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var (invocation, result) = await ExecuteSingleAsync(iteration, call, cancellationToken).ConfigureAwait(false);
                    invocations[idx] = invocation;
                    results[idx] = result;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken);
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // At least one task failed with ThrowToCaller — collect invocations done so far
            // and surface the first exception.
            for (var i = 0; i < tasks.Length; i++)
            {
                if (invocations[i] is not null)
                    invocationSink.Add(invocations[i]);
            }
            throw;
        }

        invocationSink.AddRange(invocations);
        return results;
    }

    private async Task<(ToolInvocation, ToolResult)> ExecuteSingleAsync(
        int iteration,
        PendingToolCall call,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        if (!_byName.TryGetValue(call.Name, out var tool))
        {
            sw.Stop();
            var error = $"Unknown tool '{call.Name}'. The model referenced a tool that was not exposed to it.";
            _logger.LogWarning("Agent iteration {Iteration}: unknown tool '{Tool}'", iteration, call.Name);

            var output = BuildErrorPayload(error, "UnknownTool");
            return (new ToolInvocation
            {
                Iteration = iteration,
                Name = call.Name,
                Input = call.Arguments,
                Output = null,
                Error = error,
                ErrorType = "UnknownTool",
                Duration = sw.Elapsed,
            },
            new ToolResult
            {
                CallId = call.Id,
                Name = call.Name,
                Output = output,
                IsError = true,
            });
        }

        // Optional hook — allow host to block the call.
        if (_onToolCall is not null)
        {
            var preview = new ToolInvocation
            {
                Iteration = iteration,
                Name = call.Name,
                Input = call.Arguments,
                Duration = TimeSpan.Zero,
            };
            var allow = await _onToolCall(preview, cancellationToken).ConfigureAwait(false);
            if (!allow)
            {
                sw.Stop();
                _logger.LogInformation("Agent iteration {Iteration}: tool '{Tool}' blocked by OnToolCall hook", iteration, call.Name);
                var blockedPayload = BuildErrorPayload("Tool call was blocked by policy.", "Blocked");

                return (new ToolInvocation
                {
                    Iteration = iteration,
                    Name = call.Name,
                    Input = call.Arguments,
                    Output = null,
                    Error = "Blocked by OnToolCall hook.",
                    ErrorType = "Blocked",
                    Duration = sw.Elapsed,
                    Blocked = true,
                },
                new ToolResult
                {
                    CallId = call.Id,
                    Name = call.Name,
                    Output = blockedPayload,
                    IsError = true,
                });
            }
        }

        using var callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_perCallTimeout > TimeSpan.Zero)
            callCts.CancelAfter(_perCallTimeout);

        try
        {
            var output = await tool.InvokeAsync(call.Arguments, callCts.Token).ConfigureAwait(false);
            sw.Stop();

            return (new ToolInvocation
            {
                Iteration = iteration,
                Name = call.Name,
                Input = call.Arguments,
                Output = output,
                Duration = sw.Elapsed,
            },
            new ToolResult
            {
                CallId = call.Id,
                Name = call.Name,
                Output = output,
                IsError = false,
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Agent iteration {Iteration}: tool '{Tool}' timed out after {Timeout}", iteration, call.Name, _perCallTimeout);
            var msg = $"Tool '{call.Name}' timed out after {_perCallTimeout}.";
            var payload = BuildErrorPayload(msg, "Timeout");
            return (new ToolInvocation
            {
                Iteration = iteration,
                Name = call.Name,
                Input = call.Arguments,
                Output = null,
                Error = msg,
                ErrorType = "Timeout",
                Duration = sw.Elapsed,
            },
            new ToolResult
            {
                CallId = call.Id,
                Name = call.Name,
                Output = payload,
                IsError = true,
            });
        }
        catch (Exception ex) when (_exceptionPolicy == ToolExceptionPolicy.ReturnErrorToModel)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Agent iteration {Iteration}: tool '{Tool}' threw {Type}", iteration, call.Name, ex.GetType().FullName);
            var payload = BuildErrorPayload(ex.Message, ex.GetType().FullName ?? "Exception");
            return (new ToolInvocation
            {
                Iteration = iteration,
                Name = call.Name,
                Input = call.Arguments,
                Output = null,
                Error = ex.Message,
                ErrorType = ex.GetType().FullName,
                Duration = sw.Elapsed,
            },
            new ToolResult
            {
                CallId = call.Id,
                Name = call.Name,
                Output = payload,
                IsError = true,
            });
        }
    }

    private static JsonElement BuildErrorPayload(string message, string errorType)
    {
        // Hand-rolled JSON to avoid reflection-based serialization of an
        // anonymous type (AOT-safe).
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("error", message);
            writer.WriteString("errorType", errorType);
            writer.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}
