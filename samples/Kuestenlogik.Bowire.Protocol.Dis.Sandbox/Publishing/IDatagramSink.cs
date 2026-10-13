// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

/// <summary>Where finished PDUs go. UDP in the app, a list in the tests.</summary>
internal interface IDatagramSink
{
    void Send(byte[] datagram);
}
