// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

/// <summary>Keeps every datagram instead of sending it; can be told to fail.</summary>
internal sealed class RecordingSink : IDatagramSink
{
    public List<byte[]> Datagrams { get; } = [];

    public bool Fail { get; set; }

    public void Send(byte[] datagram)
    {
        if (Fail)
            throw new InvalidOperationException("network unreachable");
        Datagrams.Add(datagram);
    }

    /// <summary>PDU type byte of each datagram: 1 Entity State, 22 Comment.</summary>
    public IEnumerable<byte> Types => Datagrams.Select(d => d[2]);
}
