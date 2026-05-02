// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// Unit-level checks for <see cref="DisNetworkProbe"/>'s URL parser
/// and address classifier. The wire-level multicast / broadcast
/// receive paths are covered indirectly via the
/// <see cref="BowireDisProtocolTests"/> integration-style tests.
/// </summary>
public sealed class DisNetworkProbeTests
{
    [Fact]
    public void TryParse_StandardMulticastUrl_MapsToMulticastMode()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://239.1.2.3:3000");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Multicast, endpoint!.Value.Mode);
        Assert.Equal(IPAddress.Parse("239.1.2.3"), endpoint.Value.Address);
        Assert.Equal(3000, endpoint.Value.Port);
    }

    [Fact]
    public void TryParse_LimitedBroadcastAddress_MapsToBroadcastMode()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://255.255.255.255:3000");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Broadcast, endpoint!.Value.Mode);
    }

    [Fact]
    public void TryParse_SubnetBroadcastAddress_MapsToBroadcastMode()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://192.168.1.255:3000");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Broadcast, endpoint!.Value.Mode);
    }

    [Fact]
    public void TryParse_BroadcastKeyword_MapsToLimitedBroadcast()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://broadcast:3000");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Broadcast, endpoint!.Value.Mode);
        Assert.Equal(IPAddress.Broadcast, endpoint.Value.Address);
    }

    [Fact]
    public void TryParse_MulticastKeyword_MapsToDefaultGroup()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://multicast");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Multicast, endpoint!.Value.Mode);
        Assert.Equal(IPAddress.Parse(DisNetworkProbe.DefaultMulticastGroup), endpoint.Value.Address);
    }

    [Fact]
    public void TryParse_UnicastAddress_MapsToUnicastMode()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://10.0.0.5:3000");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.TransportMode.Unicast, endpoint!.Value.Mode);
    }

    [Fact]
    public void TryParse_HostOnly_AppliesDefaultPort()
    {
        var endpoint = DisNetworkProbe.TryParse("dis://239.1.2.3");
        Assert.NotNull(endpoint);
        Assert.Equal(DisNetworkProbe.DefaultPort, endpoint!.Value.Port);
    }

    [Fact]
    public void TryParse_NonDisScheme_ReturnsNull()
    {
        Assert.Null(DisNetworkProbe.TryParse("https://example.com"));
    }

    [Fact]
    public void ClassifyAddress_LowestMulticast()
        => Assert.Equal(DisNetworkProbe.TransportMode.Multicast,
            DisNetworkProbe.ClassifyAddress(IPAddress.Parse("224.0.0.1")));

    [Fact]
    public void ClassifyAddress_HighestMulticast()
        => Assert.Equal(DisNetworkProbe.TransportMode.Multicast,
            DisNetworkProbe.ClassifyAddress(IPAddress.Parse("239.255.255.255")));

    [Fact]
    public void ClassifyAddress_AboveMulticastRange_IsUnicast()
        => Assert.Equal(DisNetworkProbe.TransportMode.Unicast,
            DisNetworkProbe.ClassifyAddress(IPAddress.Parse("240.0.0.1")));

    [Fact]
    public void ClassifyAddress_SubnetBroadcast_IsBroadcast()
        => Assert.Equal(DisNetworkProbe.TransportMode.Broadcast,
            DisNetworkProbe.ClassifyAddress(IPAddress.Parse("172.16.5.255")));

    [Fact]
    public void ClassifyAddress_RegularUnicast()
        => Assert.Equal(DisNetworkProbe.TransportMode.Unicast,
            DisNetworkProbe.ClassifyAddress(IPAddress.Parse("10.0.0.1")));
}
