// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuestenlogik.Bowire;
// Force the DIS plugin assembly to load before AddBowire's reflection
// scan runs — without an explicit type reference the JIT only loads
// the plugin DLL on first use, too late for the discovery pass.
_ = typeof(Kuestenlogik.Bowire.Protocol.Dis.BowireDisProtocol);

// Replays the bundled `convoy.bowire-recording.json` capture as a live
// DIS multicast stream so the Bowire workbench's DIS tab has something
// real to subscribe to without a running exercise.
//
//   convoy.bowire-recording.json ──► ConvoyReplayer ──UDP/multicast──► DIS tab
//
// The recording carries three EntityState PDUs of an M1 tank stepping
// ~100 m per update; the replayer paces them by their `capturedAt`
// offsets and loops on EOF so the stream never goes silent.
//
// Browse:
//   1. dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sample
//   2. Open http://localhost:5080/bowire (or run the `bowire` CLI)
//   3. Pick the "DIS" protocol tab, point it at the multicast group +
//      port the replayer logs at startup (default 239.1.2.3:3000), and
//      watch the convoy in the frame pane.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBowire();
builder.Services.AddHostedService<ConvoyReplayer>();

var app = builder.Build();
// Pre-seed the convoy multicast group as a discovered ServerUrl so
// the workbench shows a DIS listener entry the moment the page
// loads — the user doesn't need to type the URL into "Add server
// URL" first.
app.MapBowire(options =>
{
    options.ServerUrls.Add("dis://239.1.2.3:3000");
});

app.Run();

/// <summary>
/// Background service that loads the bundled DIS recording and
/// re-emits every PDU as a UDP datagram on the recording's multicast
/// group, honouring the captured inter-frame cadence and looping on
/// EOF so subscribers always see traffic.
/// </summary>
internal sealed class ConvoyReplayer(
    ILogger<ConvoyReplayer> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    // Default fallbacks if the recording is missing the metadata block —
    // the IEEE 1278 convention used by the DIS plugin elsewhere in this
    // repo.
    private const string DefaultGroup = "239.1.2.3";
    private const int DefaultPort = 3000;
    private const int DefaultTtl = 1;

    // Console-noise damping: log only every Nth emit so the user sees
    // activity but isn't drowned by a 3-PDU loop ticking forever.
    private const int LogEveryNthFrame = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recordingPath = Path.Combine(AppContext.BaseDirectory, "convoy.bowire-recording.json");
        if (!File.Exists(recordingPath))
        {
            logger.LogError(
                "Recording not found at {Path} — sample cannot replay. " +
                "Ensure convoy.bowire-recording.json is copied to the output directory.",
                recordingPath);
            lifetime.StopApplication();
            return;
        }

        Recording recording;
        await using (var stream = File.OpenRead(recordingPath))
        {
            recording = await JsonSerializer.DeserializeAsync(
                stream, RecordingJsonContext.Default.Recording, stoppingToken)
                ?? throw new InvalidOperationException("Recording deserialised to null.");
        }

        var disSteps = recording.Steps
            .Where(s => string.Equals(s.Protocol, "dis", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (disSteps.Count == 0)
        {
            logger.LogError("Recording {Name} contains no DIS steps; nothing to replay.", recording.Name);
            lifetime.StopApplication();
            return;
        }

        var (group, port, ttl) = ReadNetworkConfig(disSteps[0]);
        var firstOffset = disSteps[0].CapturedAt;
        var lastOffset = disSteps[^1].CapturedAt;
        var captureSpanMs = Math.Max(0, lastOffset - firstOffset);

        using var socket = new UdpClient(AddressFamily.InterNetwork);
        // Loopback-on so a single-host run can both emit and observe in
        // the same Bowire process — without this the workbench wouldn't
        // see our own multicast on Windows.
        socket.MulticastLoopback = true;
        socket.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, ttl);
        socket.Client.SetSocketOption(
            SocketOptionLevel.IP, SocketOptionName.IPProtectionLevel,
            (int)IPProtectionLevel.Unrestricted);
        // Bind ephemeral — the replayer is send-only.
        socket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
        var destination = new IPEndPoint(group, port);

        var captureSpan = TimeSpan.FromMilliseconds(captureSpanMs);
        logger.LogInformation(
            "ConvoyReplayer streaming {Count} PDUs from \"{Name}\" → " +
            "udp://{Group}:{Port} (ttl={Ttl}, captureSpan={Span}, looping on EOF). " +
            "Point Bowire's DIS tab at the same group + port to observe.",
            disSteps.Count, recording.Name, group, port, ttl, captureSpan);

        var emitted = 0L;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var loopStartTicks = Environment.TickCount64;

                foreach (var step in disSteps)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    var targetOffsetMs = Math.Max(0, step.CapturedAt - firstOffset);
                    var elapsed = Environment.TickCount64 - loopStartTicks;
                    var waitMs = targetOffsetMs - elapsed;
                    if (waitMs > 0)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(waitMs), stoppingToken);
                    }

                    if (string.IsNullOrEmpty(step.ResponseBinary))
                    {
                        logger.LogWarning(
                            "Skipping step {StepId}: responseBinary is missing.", step.Id);
                        continue;
                    }

                    byte[] payload;
                    try
                    {
                        payload = Convert.FromBase64String(step.ResponseBinary);
                    }
                    catch (FormatException ex)
                    {
                        logger.LogWarning(
                            "Skipping step {StepId}: malformed base64 ({Message}).",
                            step.Id, ex.Message);
                        continue;
                    }

                    await socket.SendAsync(payload, payload.Length, destination);
                    emitted++;

                    if (emitted == 1 || emitted % LogEveryNthFrame == 0)
                    {
                        logger.LogInformation(
                            "convoy-replay #{Count}: step={StepId}, bytes={Bytes}",
                            emitted, step.Id, payload.Length);
                    }
                }

                // EOF: brief breath before looping so a tiny capture
                // (like our 3-PDU convoy) doesn't busy-spin the network.
                if (captureSpanMs == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        logger.LogInformation("ConvoyReplayer stopping after {Count} PDUs.", emitted);
    }

    private static (IPAddress Group, int Port, int Ttl) ReadNetworkConfig(RecordingStep first)
    {
        var group = IPAddress.Parse(DefaultGroup);
        var port = DefaultPort;
        var ttl = DefaultTtl;

        if (first.Metadata is { } metadata)
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
}

/// <summary>Minimal projection of the bowire-recording JSON shape — only
/// the fields the replayer actually needs.</summary>
internal sealed record Recording(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("steps")] IReadOnlyList<RecordingStep> Steps);

internal sealed record RecordingStep(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("capturedAt")] long CapturedAt,
    [property: JsonPropertyName("protocol")] string? Protocol,
    [property: JsonPropertyName("responseBinary")] string? ResponseBinary,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, string>? Metadata);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Recording))]
internal sealed partial class RecordingJsonContext : JsonSerializerContext;
