// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

/// <summary>
/// Sends to the configured target. Socket setup as in the replay sample:
/// multicast loopback on, so Bowire on the same machine sees the traffic.
/// </summary>
internal sealed class UdpDatagramSink : IDatagramSink, IDisposable
{
    private readonly UdpClient _client = new(AddressFamily.InterNetwork);
    private readonly IPEndPoint _target;

    public UdpDatagramSink(IOptions<SandboxOptions> options)
    {
        _target = options.Value.TargetEndPoint;
        _client.EnableBroadcast = true;
        _client.MulticastLoopback = true;
        _client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, options.Value.Ttl);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
    }

    public void Send(byte[] datagram) => _client.Send(datagram, datagram.Length, _target);

    public void Dispose() => _client.Dispose();
}
