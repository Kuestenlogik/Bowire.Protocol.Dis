// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

/// <summary>
/// A wrong setting has to stop the sandbox at start with a sentence that
/// names it — not surface later as a socket exception or an empty map.
/// </summary>
public sealed class SandboxOptionsValidatorTests
{
    private static SandboxOptions Valid()
    {
        var options = new SandboxOptions();
        options.Basemaps.Add(new BasemapOption { Id = "blank", Name = "Leer", Kind = "blank" });
        return options;
    }

    private static string? Failure(SandboxOptions options)
    {
        var result = new SandboxOptionsValidator().Validate(null, options);
        return result.Failed ? result.FailureMessage : null;
    }

    [Fact]
    public void Defaults_WithABasemap_AreValid() => Assert.Null(Failure(Valid()));

    [Theory]
    [InlineData("nope")]
    [InlineData("239.1.2.3")]
    [InlineData("[::1]:3000")]
    public void Target_MustBeAnIpv4EndpointWithPort(string target)
    {
        var options = Valid();
        options.Target = target;
        Assert.Contains("Sandbox:Target", Failure(options), StringComparison.Ordinal);
    }

    [Fact]
    public void Heartbeat_MustBePositive()
    {
        var options = Valid();
        options.HeartbeatSeconds = 0;
        Assert.Contains("Sandbox:HeartbeatSeconds", Failure(options), StringComparison.Ordinal);
    }

    [Fact]
    public void FollowRate_MustBeBetweenOneAndSixty()
    {
        var options = Valid();
        options.FollowMaxUpdatesPerSecond = 0;
        Assert.Contains("Sandbox:FollowMaxUpdatesPerSecond", Failure(options), StringComparison.Ordinal);
    }

    [Fact]
    public void Ttl_MustBeOneToTwoHundredFiftyFive()
    {
        var options = Valid();
        options.Ttl = 0;
        Assert.Contains("Sandbox:Ttl", Failure(options), StringComparison.Ordinal);
    }

    [Fact]
    public void Basemaps_MustNotBeEmpty() =>
        Assert.Contains("Sandbox:Basemaps", Failure(new SandboxOptions()), StringComparison.Ordinal);

    [Fact]
    public void RasterBasemap_NeedsTiles_StyleBasemap_NeedsUrl()
    {
        var options = Valid();
        options.Basemaps.Add(new BasemapOption { Id = "osm", Name = "OSM", Kind = "raster" });
        options.Basemaps.Add(new BasemapOption { Id = "dark", Name = "Dark", Kind = "style" });
        var failure = Failure(options);
        Assert.Contains("'osm'", failure, StringComparison.Ordinal);
        Assert.Contains("'dark'", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void BasemapIds_MustBeUnique()
    {
        var options = Valid();
        options.Basemaps.Add(new BasemapOption { Id = "blank", Name = "Noch mal", Kind = "blank" });
        Assert.Contains("'blank'", Failure(options), StringComparison.Ordinal);
    }

    [Fact]
    public void InitialView_MustBeOnTheGlobe()
    {
        var options = Valid();
        options.InitialView.Latitude = 91;
        Assert.Contains("Sandbox:InitialView", Failure(options), StringComparison.Ordinal);
    }
}
