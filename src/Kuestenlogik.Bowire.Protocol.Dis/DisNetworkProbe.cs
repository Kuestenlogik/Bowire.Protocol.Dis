// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis;

/// <summary>
/// Shared helper for parsing <c>dis://host:port</c> URLs and for
/// briefly listening on a DIS exercise — multicast group, UDP
/// broadcast, or unicast — to surface the entities currently active.
/// Used by <see cref="BowireDisProtocol"/> for both discovery and
/// live streaming.
/// </summary>
/// <remarks>
/// Transport is derived from the configured IP address: anything in
/// the IPv4 multicast range (224.0.0.0/4) joins that group; any
/// broadcast address (255.255.255.255 or a subnet broadcast like
/// 192.168.1.255) just binds to the port so the OS delivers the
/// broadcast packets; anything else is treated as a unicast listen.
/// All three modes share the same socket-bind flow — only the
/// multicast-group join is conditional.
/// </remarks>
internal static class DisNetworkProbe
{
    /// <summary>Default DIS multicast group when the URL doesn't specify one.</summary>
    public const string DefaultMulticastGroup = "239.1.2.3";

    /// <summary>IPv4 limited-broadcast address — same on every network.</summary>
    public const string LimitedBroadcastAddress = "255.255.255.255";

    /// <summary>Default DIS UDP port when the URL doesn't specify one.</summary>
    public const int DefaultPort = 3000;

    /// <summary>
    /// How the DIS traffic is delivered over UDP. Drives the socket
    /// setup — multicast joins a group, broadcast doesn't.
    /// </summary>
    public enum TransportMode
    {
        /// <summary>IPv4 multicast address (224.0.0.0/4).</summary>
        Multicast,

        /// <summary>Limited (255.255.255.255) or subnet-directed (x.x.x.255) broadcast.</summary>
        Broadcast,

        /// <summary>Unicast listen — binds to the port and receives whatever arrives.</summary>
        Unicast,
    }

    /// <summary>Parsed network coordinates: listen address + port + transport mode.</summary>
    public readonly record struct Endpoint(IPAddress Address, int Port, TransportMode Mode);

    /// <summary>
    /// Parse <paramref name="serverUrl"/> as <c>dis://host:port</c>
    /// (or bare <c>host:port</c>) and return the listen coordinates +
    /// transport mode. Defaults apply when the URL is incomplete.
    /// Accepts the literal hostnames <c>multicast</c> (→ default
    /// multicast group) and <c>broadcast</c> (→ 255.255.255.255) for
    /// CLI ergonomics. Returns <c>null</c> when the URL doesn't look
    /// like a DIS address at all.
    /// </summary>
    public static Endpoint? TryParse(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return null;

        // Accept bare host:port too for CLI ergonomics.
        var trimmed = serverUrl.TrimStart();
        if (trimmed.StartsWith("dis://", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["dis://".Length..];
        else if (trimmed.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["udp://".Length..];
        else if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            // Some other scheme — not DIS.
            return null;
        }

        var hostPart = trimmed;
        var port = DefaultPort;
        var colon = trimmed.LastIndexOf(':');
        if (colon > 0)
        {
            hostPart = trimmed[..colon];
            var portPart = trimmed[(colon + 1)..].TrimEnd('/');
            if (!int.TryParse(portPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                port = DefaultPort;
            }
        }

        hostPart = hostPart.TrimEnd('/');
        if (string.IsNullOrEmpty(hostPart)) hostPart = DefaultMulticastGroup;

        // "multicast" / "broadcast" keywords map to canonical addresses
        // so users don't have to remember the numeric forms.
        if (hostPart.Equals("multicast", StringComparison.OrdinalIgnoreCase))
            hostPart = DefaultMulticastGroup;
        else if (hostPart.Equals("broadcast", StringComparison.OrdinalIgnoreCase))
            hostPart = LimitedBroadcastAddress;

        if (!IPAddress.TryParse(hostPart, out var address)) return null;

        var mode = ClassifyAddress(address);
        return new Endpoint(address, port, mode);
    }

    /// <summary>Return the transport mode for an IPv4 address.</summary>
    internal static TransportMode ClassifyAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return TransportMode.Unicast;
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return TransportMode.Unicast;

        // 224.0.0.0/4 — IPv4 multicast range per RFC 5771.
        if (bytes[0] >= 224 && bytes[0] <= 239) return TransportMode.Multicast;

        // 255.255.255.255 — limited broadcast (RFC 919). Subnet-directed
        // broadcasts (e.g. 192.168.1.255) we also recognise when the
        // final octet is 255 and the address isn't multicast; not
        // perfect (a classful /24 assumption) but better than forcing
        // users to configure transport mode by hand, and receiving
        // side doesn't actually care since Bind to IPAddress.Any
        // receives both styles regardless.
        if (address.Equals(IPAddress.Broadcast)) return TransportMode.Broadcast;
        if (bytes[3] == 255) return TransportMode.Broadcast;

        return TransportMode.Unicast;
    }

    /// <summary>Snapshot of an entity observed during a probe.</summary>
    /// <param name="EntityId">Full entity id (site / app / entity).</param>
    /// <param name="Marking">ASCII entity marking (the 11-char human label).</param>
    /// <param name="EntityType">7-tuple from the Entity State PDU.</param>
    /// <param name="Force">Force id reported on the most recent update.</param>
    public readonly record struct ObservedEntity(
        EntityId EntityId,
        string Marking,
        EntityType EntityType,
        ForceId Force);

    /// <summary>
    /// Listen on <paramref name="endpoint"/> for <paramref name="duration"/>,
    /// decode every Entity State PDU that arrives, and return the
    /// distinct set of entities observed (keyed by entity id).
    /// Honours the endpoint's <see cref="Endpoint.Mode"/> —
    /// multicast joins a group, broadcast / unicast just bind.
    /// Returns an empty list when nothing arrives or the socket
    /// can't be opened.
    /// </summary>
    public static async Task<IReadOnlyList<ObservedEntity>> ObserveAsync(
        Endpoint endpoint, TimeSpan duration, CancellationToken ct = default)
    {
        using var socket = CreateListenSocket(endpoint, out var joinedGroup);
        if (socket is null) return [];

        var observed = new Dictionary<EntityId, ObservedEntity>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(duration);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try { result = await socket.ReceiveAsync(cts.Token); }
                catch (OperationCanceledException) { break; }
                catch (SocketException) { break; }

                // Inspect the PDU header — only Entity State PDUs
                // carry markings we can surface here.
                if (result.Buffer.Length < PduHeader.WireLength) continue;

                var pduType = (DisPduType)result.Buffer[2];
                if (pduType != DisPduType.EntityState) continue;
                if (result.Buffer.Length < EntityStatePdu.MinimumWireLength) continue;

                EntityStatePdu pdu;
                try { pdu = EntityStatePdu.Unmarshal(result.Buffer); }
                catch { continue; }

                observed[pdu.EntityId] = new ObservedEntity(
                    pdu.EntityId,
                    pdu.Marking.Marking,
                    pdu.EntityType,
                    pdu.Force);
            }
        }
        finally
        {
            if (joinedGroup) try { socket.DropMulticastGroup(endpoint.Address); } catch { /* best-effort */ }
        }

        return observed.Values.ToList();
    }

    /// <summary>
    /// Open a UDP socket ready to receive PDUs for the given endpoint.
    /// Returns <c>null</c> when the OS refuses the bind or join.
    /// Sets <paramref name="joinedGroup"/> so the caller knows whether
    /// to DropMulticastGroup on cleanup.
    /// </summary>
    internal static UdpClient? CreateListenSocket(Endpoint endpoint, out bool joinedGroup)
    {
        joinedGroup = false;
        var socket = new UdpClient(AddressFamily.InterNetwork)
        {
            ExclusiveAddressUse = false,
        };
        socket.Client.SetSocketOption(
            SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        // SO_BROADCAST — required on some platforms for receiving
        // broadcast packets even though it's primarily a sending flag.
        // Setting it unconditionally is harmless.
        if (endpoint.Mode == TransportMode.Broadcast)
        {
            try
            {
                socket.Client.SetSocketOption(
                    SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            }
            catch (SocketException) { /* best-effort */ }
        }

        try
        {
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, endpoint.Port));
            if (endpoint.Mode == TransportMode.Multicast)
            {
                socket.JoinMulticastGroup(endpoint.Address);
                joinedGroup = true;
            }
        }
        catch (SocketException)
        {
            socket.Dispose();
            return null;
        }
        return socket;
    }
}
