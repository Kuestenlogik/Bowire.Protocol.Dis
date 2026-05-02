// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Kuestenlogik.Bowire.Mocking;
using Microsoft.Extensions.Logging;

namespace Kuestenlogik.Bowire.Protocol.Dis;

/// <summary>
/// Plugs into Bowire's mock server via the
/// <see cref="IBowireMockEmitter"/> extension point. When a recording
/// contains steps tagged <c>protocol: "dis"</c>, the emitter opens a
/// UDP socket, joins the configured multicast group, and broadcasts
/// each step's recorded PDU bytes (<c>responseBinary</c>) on a
/// schedule derived from per-step <c>capturedAt</c> timestamps.
/// </summary>
/// <remarks>
/// <para>
/// Network configuration — overridable via recording metadata on the
/// DIS step (first DIS step's metadata wins):
/// </para>
/// <list type="bullet">
///   <item><c>multicast-group</c>: destination IPv4 address (default
///   <c>239.1.2.3</c>). The IEEE 1278 spec leaves the exact group to
///   site configuration; the default matches the range recommended
///   for single-host development use.</item>
///   <item><c>port</c>: UDP port (default <c>3000</c>).</item>
///   <item><c>ttl</c>: multicast TTL (default <c>1</c>, stays on the
///   local subnet).</item>
/// </list>
/// <para>
/// This emitter runs in a background task for the full lifetime of
/// the mock server; it loops the PDU sequence when
/// <see cref="MockEmitterOptions.Loop"/> is set, mirroring the MQTT
/// proactive emitter. Pacing uses per-step <c>capturedAt</c> (ms)
/// rather than per-frame <c>timestampMs</c> because DIS PDUs are
/// captured as discrete unary steps, one PDU per step.
/// </para>
/// </remarks>
public sealed class DisMockEmitter : IBowireMockEmitter
{
    private UdpClient? _socket;
    private IPEndPoint? _destination;
    private CancellationTokenSource? _cts;
    private Task? _schedulerTask;
    private bool _disposed;

    /// <inheritdoc />
    public string Id => "dis";

    /// <inheritdoc />
    public bool CanEmit(BowireRecording recording)
    {
        ArgumentNullException.ThrowIfNull(recording);
        return recording.Steps.Any(IsDisStep);
    }

    /// <inheritdoc />
    public Task StartAsync(
        BowireRecording recording,
        MockEmitterOptions options,
        ILogger logger,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recording);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var disSteps = recording.Steps.Where(IsDisStep).ToList();
        if (disSteps.Count == 0) return Task.CompletedTask;

        var (group, port, ttl) = ReadNetworkConfig(disSteps[0]);
        _socket = new UdpClient(AddressFamily.InterNetwork);
        _socket.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, ttl);
        // Bind to ephemeral port — the mock only sends; doesn't listen.
        _socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        _destination = new IPEndPoint(group, port);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _schedulerTask = Task.Run(() => RunAsync(disSteps, options, logger, _cts.Token), _cts.Token);

        logger.LogInformation(
            "dis-emitter listening → udp://{Group}:{Port} (ttl={Ttl}, pduSteps={Count})",
            group, port, ttl, disSteps.Count);
        return Task.CompletedTask;
    }

    private static bool IsDisStep(BowireRecordingStep s) =>
        string.Equals(s.Protocol, "dis", StringComparison.OrdinalIgnoreCase);

    private static (IPAddress Group, int Port, int Ttl) ReadNetworkConfig(BowireRecordingStep first)
    {
        var metadata = first.Metadata;
        var group = IPAddress.Parse("239.1.2.3");
        var port = 3000;
        var ttl = 1;

        if (metadata is not null)
        {
            if (metadata.TryGetValue("multicast-group", out var groupStr) &&
                IPAddress.TryParse(groupStr, out var parsedGroup))
            {
                group = parsedGroup;
            }
            if (metadata.TryGetValue("port", out var portStr) &&
                int.TryParse(portStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPort))
            {
                port = parsedPort;
            }
            if (metadata.TryGetValue("ttl", out var ttlStr) &&
                int.TryParse(ttlStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTtl))
            {
                ttl = parsedTtl;
            }
        }
        return (group, port, ttl);
    }

    private async Task RunAsync(
        List<BowireRecordingStep> steps,
        MockEmitterOptions options,
        ILogger logger,
        CancellationToken ct)
    {
        if (_socket is null || _destination is null) return;

        var baseCapturedAt = steps[0].CapturedAt;
        var speed = options.ReplaySpeed;

        do
        {
            var scheduleStartTicks = Environment.TickCount64;

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();

                if (speed > 0)
                {
                    var targetOffsetMs = (long)((step.CapturedAt - baseCapturedAt) / speed);
                    var elapsed = Environment.TickCount64 - scheduleStartTicks;
                    var waitMs = targetOffsetMs - elapsed;
                    if (waitMs > 0)
                    {
                        try { await Task.Delay(TimeSpan.FromMilliseconds(waitMs), ct); }
                        catch (OperationCanceledException) { return; }
                    }
                }

                await EmitAsync(step, logger, ct);
            }
        }
        while (options.Loop && !ct.IsCancellationRequested);
    }

    private async Task EmitAsync(BowireRecordingStep step, ILogger logger, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(step.ResponseBinary))
        {
            logger.LogWarning(
                "dis-emitter skipping step '{StepId}': responseBinary is missing. " +
                "DIS recordings need raw PDU bytes captured as responseBinary.",
                step.Id);
            return;
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(step.ResponseBinary);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(
                "dis-emitter skipping step '{StepId}': malformed base64 PDU ({Message}).",
                step.Id, ex.Message);
            return;
        }

        try
        {
            await _socket!.SendAsync(payload, payload.Length, _destination);
            logger.LogInformation(
                "dis-emit(step={StepId}, bytes={Bytes})", step.Id, payload.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "dis-emitter send failed for step '{StepId}'; scheduler continues.", step.Id);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_cts is not null)
        {
            try { await _cts.CancelAsync(); }
            catch (ObjectDisposedException) { /* already torn down */ }
        }
        if (_schedulerTask is not null)
        {
            try { await _schedulerTask; }
            catch (OperationCanceledException) { /* expected */ }
            catch { /* scheduler cleanup is best-effort */ }
        }
        _socket?.Dispose();
        _cts?.Dispose();
    }
}
