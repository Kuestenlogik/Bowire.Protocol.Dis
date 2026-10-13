// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox;

/// <summary>
/// Everything the sandbox reads from the <c>Sandbox</c> section of
/// <c>appsettings.json</c>. Defaults match the replay sample, so Bowire
/// subscribed to <c>dis://239.1.2.3:3000</c> sees both.
/// </summary>
internal sealed class SandboxOptions
{
    public const string Section = "Sandbox";

    /// <summary>UDP destination, IPv4 <c>address:port</c>; multicast, broadcast or unicast.</summary>
    public string Target { get; set; } = "239.1.2.3:3000";

    public int Ttl { get; set; } = 1;

    public byte ExerciseId { get; set; } = 1;

    public ushort SiteId { get; set; } = 1;

    /// <summary>3001, so the sandbox's entities are told apart from the replay sample's.</summary>
    public ushort ApplicationId { get; set; } = 3001;

    public double HeartbeatSeconds { get; set; } = 5;

    public int FollowMaxUpdatesPerSecond { get; set; } = 10;

    /// <summary>
    /// Datum id of every key-value record. 900000–999999 is unassigned in
    /// SISO-REF-010 v37; there is no range for free use, so this is a
    /// sandbox convention, not a standard.
    /// </summary>
    public uint CommentDatumId { get; set; } = 900000;

    public MapViewOptions InitialView { get; set; } = new();

    /// <summary>Filled from configuration only; the binder appends to this list.</summary>
    public IList<BasemapOption> Basemaps { get; } = [];

    public IPEndPoint TargetEndPoint => IPEndPoint.Parse(Target);
}

internal sealed class MapViewOptions
{
    public double Latitude { get; set; } = 53.55;

    public double Longitude { get; set; } = 9.99;

    public double Zoom { get; set; } = 11;
}

internal sealed class BasemapOption
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary><c>style</c> (MapLibre style URL), <c>raster</c> (XYZ tiles) or <c>blank</c> (a colour).</summary>
    public string Kind { get; set; } = "blank";

    public string? Url { get; set; }

    public IList<string> Tiles { get; } = [];

    public string? Attribution { get; set; }

    public string Color { get; set; } = "#1e1e1e";
}
