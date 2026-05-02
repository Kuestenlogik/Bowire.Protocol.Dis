// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Kuestenlogik.Bowire;
using Kuestenlogik.Bowire.Models;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis;

/// <summary>
/// Bowire protocol plugin for IEEE 1278.1 Distributed Interactive
/// Simulation (DIS). Surfaces the multicast exercise as a discoverable
/// protocol: a short probe on the configured group collects every
/// active entity and each one becomes a service the workbench can
/// subscribe to for a live PDU feed. Mock-replay lives on the
/// <see cref="DisMockEmitter"/> side so captures flow back out the
/// same way real exercises do.
/// </summary>
/// <remarks>
/// <para>
/// Server URL format is <c>dis://group:port</c> (defaults
/// <c>239.1.2.3:3000</c>). Bare <c>host:port</c> is accepted as a
/// CLI-ergonomics shortcut. See <see cref="DisNetworkProbe"/> for
/// the URL parser and multicast observation helpers.
/// </para>
/// <para>
/// The plugin ships in its own repository
/// (<c>Kuestenlogik/Bowire.Protocol.Dis</c>) and NuGet package
/// (<c>Kuestenlogik.Bowire.Protocol.Dis</c>) rather than the main Bowire
/// repo, both to prove out the external-plugin distribution path and
/// to keep DIS-specific dependencies out of users who don't need them.
/// </para>
/// </remarks>
public sealed class BowireDisProtocol : IBowireProtocol
{
    /// <summary>Synthetic service name for the exercise-wide feed.</summary>
    public const string ExerciseServiceName = "Exercise";

    /// <summary>Method name shared by every DIS service — a streaming PDU feed.</summary>
    public const string MonitorMethodName = "monitor";

    /// <inheritdoc />
    public string Name => "DIS";

    /// <inheritdoc />
    public string Id => "dis";

    /// <inheritdoc />
    // Radar / broadcast glyph — DIS has no official logo; matches the
    // "connected nodes broadcasting" aesthetic other broadcast
    // protocols use in the workbench sidebar.
    public string IconSvg => """<svg viewBox="0 0 24 24" fill="none" stroke="#22d3ee" stroke-width="1.5" width="16" height="16" aria-hidden="true"><circle cx="12" cy="12" r="2"/><path d="M6 12a6 6 0 0112 0"/><path d="M3 12a9 9 0 0118 0"/></svg>""";

    /// <inheritdoc />
    public IReadOnlyList<BowirePluginSetting> Settings =>
    [
        new("probeDuration",
            "Discovery probe duration",
            "How long to listen on the multicast group during discovery (seconds)",
            "number", 3),
    ];

    /// <inheritdoc />
    public async Task<List<BowireServiceInfo>> DiscoverAsync(
        string serverUrl, bool showInternalServices, CancellationToken ct = default)
    {
        var endpoint = DisNetworkProbe.TryParse(serverUrl);
        if (endpoint is null) return [];

        // Exercise-wide feed is always present even when no entity
        // PDUs arrive during the probe — the workbench still needs a
        // way to open a raw stream on the group.
        var services = new List<BowireServiceInfo>
        {
            BuildExerciseService(serverUrl, endpoint.Value),
        };

        IReadOnlyList<DisNetworkProbe.ObservedEntity> observed;
        try
        {
            observed = await DisNetworkProbe.ObserveAsync(
                endpoint.Value, TimeSpan.FromSeconds(3), ct);
        }
        catch (OperationCanceledException)
        {
            return services;
        }

        foreach (var entity in observed.OrderBy(e => e.EntityId.Site)
                     .ThenBy(e => e.EntityId.Application)
                     .ThenBy(e => e.EntityId.Entity))
        {
            services.Add(BuildEntityService(serverUrl, endpoint.Value, entity));
        }

        return services;
    }

    /// <inheritdoc />
    public Task<InvokeResult> InvokeAsync(
        string serverUrl, string service, string method,
        List<string> jsonMessages, bool showInternalServices,
        Dictionary<string, string>? metadata = null, CancellationToken ct = default)
    {
        // DIS is broadcast-only — there is no unary invocation. Use
        // the mock server to replay captured PDU sequences, or open a
        // stream on the monitor method.
        return Task.FromResult(new InvokeResult(
            null, 0,
            "DIS is broadcast-only. Open the monitor stream to observe live PDUs, or use the mock server to replay captured sequences.",
            new Dictionary<string, string>()));
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> InvokeStreamAsync(
        string serverUrl, string service, string method,
        List<string> jsonMessages, bool showInternalServices,
        Dictionary<string, string>? metadata = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var endpoint = DisNetworkProbe.TryParse(serverUrl);
        if (endpoint is null) yield break;

        // Service name that isn't the exercise-wide feed is an
        // entity-scoped subscription — we filter PDUs to that entity.
        EntityId? filter = TryParseEntityServiceName(service);

        using var socket = DisNetworkProbe.CreateListenSocket(endpoint.Value, out var joinedGroup);
        if (socket is null) yield break;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try { result = await socket.ReceiveAsync(ct); }
                catch (OperationCanceledException) { yield break; }
                catch (SocketException) { yield break; }

                var envelope = TryBuildEnvelope(result.Buffer, filter);
                if (envelope is not null) yield return envelope;
            }
        }
        finally
        {
            if (joinedGroup) try { socket.DropMulticastGroup(endpoint.Value.Address); } catch { /* best-effort */ }
        }
    }

    /// <inheritdoc />
    public Task<IBowireChannel?> OpenChannelAsync(
        string serverUrl, string service, string method,
        bool showInternalServices, Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
        => Task.FromResult<IBowireChannel?>(null);

    private static BowireServiceInfo BuildExerciseService(
        string serverUrl, DisNetworkProbe.Endpoint endpoint)
    {
        var methods = new List<BowireMethodInfo>
        {
            BuildMonitorMethod(
                $"dis/{ExerciseServiceName}/{MonitorMethodName}",
                $"Stream every PDU arriving on {endpoint.Address}:{endpoint.Port} as a JSON envelope."),
        };

        return new BowireServiceInfo(ExerciseServiceName, "dis", methods)
        {
            Source = "dis",
            OriginUrl = serverUrl,
            Description = $"Live DIS exercise feed on {endpoint.Address}:{endpoint.Port}. Every PDU from every entity shows up here.",
        };
    }

    private static BowireServiceInfo BuildEntityService(
        string serverUrl,
        DisNetworkProbe.Endpoint endpoint,
        DisNetworkProbe.ObservedEntity entity)
    {
        var serviceName = FormatEntityServiceName(entity.EntityId, entity.Marking);
        var typeLabel = FormatEntityType(entity.EntityType);
        var description =
            $"Entity {FormatEntityId(entity.EntityId)} on {endpoint.Address}:{endpoint.Port}" +
            (string.IsNullOrWhiteSpace(entity.Marking) ? "" : $" — \"{entity.Marking.Trim()}\"") +
            $" ({entity.Force}, {typeLabel}).";

        var methods = new List<BowireMethodInfo>
        {
            BuildMonitorMethod(
                $"dis/{serviceName}/{MonitorMethodName}",
                $"Stream PDUs filtered to entity {FormatEntityId(entity.EntityId)}."),
        };

        return new BowireServiceInfo(serviceName, "dis", methods)
        {
            Source = "dis",
            OriginUrl = serverUrl,
            Description = description,
        };
    }

    private static BowireMethodInfo BuildMonitorMethod(string fullName, string description) =>
        new(
            Name: MonitorMethodName,
            FullName: fullName,
            ClientStreaming: false,
            ServerStreaming: true,
            InputType: BuildMonitorInput(),
            OutputType: BuildMonitorOutput(),
            MethodType: "ServerStreaming")
        {
            Summary = "Live PDU feed",
            Description = description,
        };

    private static BowireMessageInfo BuildMonitorInput() =>
        new("DisMonitorRequest", "dis.MonitorRequest", []);

    private static BowireMessageInfo BuildMonitorOutput() => new(
        "DisPduEnvelope", "dis.PduEnvelope",
        [
            new BowireFieldInfo("pduType", 1, "string", "LABEL_OPTIONAL", false, false, null, null)
            {
                Description = "Human-readable PDU type name (e.g. EntityState, Fire, Detonation).",
            },
            new BowireFieldInfo("pduTypeId", 2, "int32", "LABEL_OPTIONAL", false, false, null, null)
            {
                Description = "IEEE 1278.1 PDU type wire code.",
            },
            new BowireFieldInfo("protocolVersion", 3, "int32", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("exerciseId", 4, "int32", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("length", 5, "int32", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("entityId", 6, "string", "LABEL_OPTIONAL", false, false, null, null)
            {
                Description = "site:app:entity triple for Entity State PDUs; null otherwise.",
            },
            new BowireFieldInfo("marking", 7, "string", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("force", 8, "string", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("entityType", 9, "string", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("bytes", 10, "int32", "LABEL_OPTIONAL", false, false, null, null),
            new BowireFieldInfo("raw", 11, "string", "LABEL_OPTIONAL", false, false, null, null)
            {
                Description = "Base64 of the full PDU bytes. Always present so the UI can hex-dump it.",
            },
        ]);

    internal static string FormatEntityServiceName(EntityId id, string marking)
    {
        // Prefer the ASCII marking — that's the human label the
        // simulation stack attached to the entity. Fall back to the
        // numeric triple when the marking is empty or whitespace.
        var label = string.IsNullOrWhiteSpace(marking)
            ? FormatEntityId(id)
            : $"{marking.Trim()} ({FormatEntityId(id)})";
        return label;
    }

    internal static EntityId? TryParseEntityServiceName(string? service)
    {
        if (string.IsNullOrWhiteSpace(service)) return null;
        if (string.Equals(service, ExerciseServiceName, StringComparison.Ordinal)) return null;

        // Service names embed the entity triple either as the whole
        // name ("1:1:42") or in parentheses after the marking
        // ("T-72 (1:1:42)"). Pull the last "(…)" block and parse that;
        // fall back to parsing the whole string.
        var open = service.LastIndexOf('(');
        var close = service.LastIndexOf(')');
        var candidate = (open >= 0 && close > open)
            ? service.Substring(open + 1, close - open - 1)
            : service;

        var parts = candidate.Split(':');
        if (parts.Length != 3) return null;
        if (!ushort.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var site)) return null;
        if (!ushort.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var app)) return null;
        if (!ushort.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ent)) return null;
        return new EntityId(site, app, ent);
    }

    internal static string FormatEntityId(EntityId id) =>
        string.Create(CultureInfo.InvariantCulture, $"{id.Site}:{id.Application}:{id.Entity}");

    internal static string FormatEntityType(EntityType t) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{t.Kind}.{t.Domain}.{t.Country}.{t.Category}.{t.Subcategory}.{t.Specific}.{t.Extra}");

    internal static string? TryBuildEnvelope(byte[] buffer, EntityId? filter)
    {
        if (buffer.Length < PduHeader.WireLength) return null;

        var protocolVersion = buffer[0];
        var exerciseId = buffer[1];
        var pduTypeId = buffer[2];
        var pduType = (DisPduType)pduTypeId;
        var length = (buffer[8] << 8) | buffer[9];

        string? entityIdString = null;
        string? marking = null;
        string? force = null;
        string? entityType = null;

        if (pduType == DisPduType.EntityState &&
            buffer.Length >= EntityStatePdu.MinimumWireLength)
        {
            EntityStatePdu pdu;
            try { pdu = EntityStatePdu.Unmarshal(buffer); }
            catch { pdu = null!; }
            if (pdu is not null)
            {
                if (filter is not null && !pdu.EntityId.Equals(filter.Value))
                    return null;

                entityIdString = FormatEntityId(pdu.EntityId);
                marking = pdu.Marking.Marking?.Trim();
                force = pdu.Force.ToString();
                entityType = FormatEntityType(pdu.EntityType);
            }
        }
        else if (filter is not null)
        {
            // Non-EntityState PDU on an entity-filtered stream: we
            // don't attempt to route every PDU type by id here, so
            // drop it rather than show unrelated traffic.
            return null;
        }

        var envelope = new
        {
            pduType = pduType.ToString(),
            pduTypeId = (int)pduTypeId,
            protocolVersion = (int)protocolVersion,
            exerciseId = (int)exerciseId,
            length,
            entityId = entityIdString,
            marking,
            force,
            entityType,
            bytes = buffer.Length,
            raw = Convert.ToBase64String(buffer),
        };

        return JsonSerializer.Serialize(envelope);
    }
}
