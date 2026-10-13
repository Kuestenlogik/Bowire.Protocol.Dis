// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox;

/// <summary>
/// Checks the <c>Sandbox</c> section at start. Ids are typed (byte,
/// ushort), so the binder already rejects values outside their range.
/// </summary>
internal sealed class SandboxOptionsValidator : IValidateOptions<SandboxOptions>
{
    private static readonly string[] s_kinds = ["style", "raster", "blank"];

    public ValidateOptionsResult Validate(string? name, SandboxOptions options)
    {
        var errors = new List<string>();

        if (!IPEndPoint.TryParse(options.Target, out var endpoint) || endpoint.Port == 0
            || endpoint.AddressFamily != AddressFamily.InterNetwork)
        {
            errors.Add($"Sandbox:Target '{options.Target}' is not an IPv4 endpoint like 239.1.2.3:3000.");
        }
        if (options.Ttl is < 1 or > 255)
            errors.Add($"Sandbox:Ttl {options.Ttl} is outside 1–255.");
        if (!(options.HeartbeatSeconds > 0))
            errors.Add($"Sandbox:HeartbeatSeconds {options.HeartbeatSeconds} must be greater than 0.");
        if (options.FollowMaxUpdatesPerSecond is < 1 or > 60)
            errors.Add($"Sandbox:FollowMaxUpdatesPerSecond {options.FollowMaxUpdatesPerSecond} is outside 1–60.");
        if (options.InitialView.Latitude is < -90 or > 90 || options.InitialView.Longitude is < -180 or > 180)
            errors.Add("Sandbox:InitialView is not a position on the globe.");

        if (options.Basemaps.Count == 0)
            errors.Add("Sandbox:Basemaps is empty; at least the 'blank' fallback is needed.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var basemap in options.Basemaps)
        {
            if (!seen.Add(basemap.Id))
                errors.Add($"Sandbox:Basemaps has the id '{basemap.Id}' twice.");
            if (!s_kinds.Contains(basemap.Kind, StringComparer.Ordinal))
                errors.Add($"Sandbox:Basemaps '{basemap.Id}' has kind '{basemap.Kind}'; expected style, raster or blank.");
            else if (basemap.Kind == "style" && string.IsNullOrWhiteSpace(basemap.Url))
                errors.Add($"Sandbox:Basemaps '{basemap.Id}' is a style without a Url.");
            else if (basemap.Kind == "raster" && basemap.Tiles.Count == 0)
                errors.Add($"Sandbox:Basemaps '{basemap.Id}' is a raster layer without Tiles.");
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
