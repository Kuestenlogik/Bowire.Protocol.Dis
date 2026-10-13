// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Kuestenlogik.Bowire.Protocol.Dis.Records;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using PluginComment = Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement.CommentPdu;
using PluginDeadReckoning = Kuestenlogik.Bowire.Protocol.Dis.Enumerations.DeadReckoningAlgorithm;
using PluginEntityState = Kuestenlogik.Bowire.Protocol.Dis.Pdu.EntityStatePdu;
using PluginForce = Kuestenlogik.Bowire.Protocol.Dis.Enumerations.ForceId;
using PluginPduType = Kuestenlogik.Bowire.Protocol.Dis.Enumerations.DisPduType;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

/// <summary>
/// The point of the sandbox: what OpenDisNet encodes, Bowire's decoder
/// reads back field for field. A mismatch here is a bug on one side.
/// </summary>
public sealed class PduFactoryDecoderTests
{
    private const double Deg = Math.PI / 180.0;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 30, 0, TimeSpan.Zero));
    private readonly PduFactory _factory;

    public PduFactoryDecoderTests() => _factory = new PduFactory(Options.Create(new SandboxOptions()), _time);

    private static SandboxEntity Hamburg(
        string name = "Objekt 7", double heading = 90, double speed = 0, bool following = false,
        params KeyValuePair<string, string>[] pairs) =>
        new(7, name, "tank", Force.Opposing, new GeoPosition(53.5511, 9.9937, 12.0), heading, speed, following, pairs);

    [Fact]
    public void EntityState_ReadsBackFieldForField()
    {
        var bytes = _factory.EntityState(Hamburg());
        var pdu = PluginEntityState.Unmarshal(bytes);

        Assert.Equal(144, bytes.Length);
        Assert.Equal(PluginPduType.EntityState, pdu.Header.PduType);
        Assert.Equal((byte)1, pdu.Header.ExerciseId);
        Assert.Equal(bytes.Length, (int)pdu.Header.Length);
        Assert.Equal(new EntityId(1, 3001, 7), pdu.EntityId);
        Assert.Equal(PluginForce.Opposing, pdu.Force);
        Assert.Equal(new EntityType(1, 1, 78, 1, 0, 0, 0), pdu.EntityType);

        var back = GeocentricCoordinate.ToWgs84(pdu.Location);
        Assert.NotNull(back);
        Assert.Equal(53.5511, back!.Value.Latitude, 7);
        Assert.Equal(9.9937, back.Value.Longitude, 7);
        Assert.Equal(12.0, back.Value.Altitude, 2);

        Assert.Equal(99.9937 * Deg, pdu.Orientation.Psi, 5);
        Assert.Equal(0.0, pdu.Orientation.Theta, 5);
        Assert.Equal(-143.5511 * Deg, pdu.Orientation.Phi, 5);

        Assert.Equal(default, pdu.LinearVelocity);
        Assert.Equal(0u, pdu.Appearance);
        Assert.Equal(PluginDeadReckoning.Static, pdu.DeadReckoning.Algorithm);
        Assert.Equal((byte)1, pdu.Marking.CharacterSet);
        Assert.Equal("Objekt 7", pdu.Marking.Marking.TrimEnd('\0'));
    }

    [Fact]
    public void EntityState_WhileFollowing_CarriesVelocityAndConstantVelocityDeadReckoning()
    {
        var entity = Hamburg() with { Position = new GeoPosition(0, 0, 0), HeadingDegrees = 0, SpeedMetresPerSecond = 10, Following = true };

        var pdu = PluginEntityState.Unmarshal(_factory.EntityState(entity));

        Assert.Equal(PluginDeadReckoning.FPW, pdu.DeadReckoning.Algorithm);
        Assert.Equal(0f, pdu.LinearVelocity.X, 4);
        Assert.Equal(0f, pdu.LinearVelocity.Y, 4);
        Assert.Equal(10f, pdu.LinearVelocity.Z, 4);
    }

    [Fact]
    public void EntityState_Deactivated_SetsAppearanceBit23()
    {
        var pdu = PluginEntityState.Unmarshal(_factory.EntityState(Hamburg(), deactivated: true));
        Assert.Equal(1u << 23, pdu.Appearance);
    }

    [Theory]
    [InlineData("Leo", "Leo")]
    [InlineData("Größenwahn-Panzer", "Gr??enwahn-")]
    [InlineData("🚀 Rakete", "? Rakete")]
    [InlineData("Exakt11Zei", "Exakt11Zei")]
    public void Marking_IsAsciiElevenBytesAndReadsBack(string name, string expected)
    {
        Assert.Equal(PduFactory.MarkingLength, PduFactory.Marking(name).Length);
        Assert.Equal(expected, PduFactory.MarkingText(name));

        var pdu = PluginEntityState.Unmarshal(_factory.EntityState(Hamburg(name)));
        Assert.Equal(expected, pdu.Marking.Marking.TrimEnd('\0'));
    }

    [Fact]
    public void Timestamp_IsRelativeAndCountsFromTheFullHour()
    {
        // 12:30:00 is half an hour: 2^31-1 units per hour, halved, in bits 31..1; bit 0 clear.
        var pdu = PluginEntityState.Unmarshal(_factory.EntityState(Hamburg()));
        Assert.Equal(1073741823u << 1, pdu.Header.Timestamp);
        Assert.Equal(0u, pdu.Header.Timestamp & 1u);
    }

    [Fact]
    public void Comment_CarriesOneVariableDatumPerPair()
    {
        var entity = Hamburg(pairs: [new("status", "ready"), new("zustand", "bereit für Einsatz")]);

        var bytes = _factory.Comment(entity);
        var pdu = PluginComment.Unmarshal(bytes);

        Assert.Equal(PluginPduType.Comment, pdu.Header.PduType);
        Assert.Equal(bytes.Length, (int)pdu.Header.Length);
        Assert.Equal(PairRules.CommentPduLength(entity.Pairs), bytes.Length);
        Assert.Equal(new EntityId(1, 3001, 7), pdu.OriginatingEntityId);
        Assert.Equal(new EntityId(0xFFFF, 0xFFFF, 0xFFFF), pdu.ReceivingEntityId);
        Assert.Empty(pdu.FixedDatums ?? []);

        var datums = pdu.VariableDatums!;
        Assert.Equal(2, datums.Count);
        Assert.All(datums, d => Assert.Equal(900000u, d.DatumId));
        Assert.Equal("status=ready", Encoding.UTF8.GetString(datums[0].Value));
        Assert.Equal(12u * 8, datums[0].DatumLengthBits);
        var second = Encoding.UTF8.GetBytes("zustand=bereit für Einsatz");
        Assert.Equal(second, datums[1].Value);
        Assert.Equal((uint)second.Length * 8, datums[1].DatumLengthBits);
    }

    [Fact]
    public void Comment_WithoutPairs_IsTheBare32Bytes()
    {
        var bytes = _factory.Comment(Hamburg());
        var pdu = PluginComment.Unmarshal(bytes);

        Assert.Equal(32, bytes.Length);
        Assert.Empty(pdu.VariableDatums ?? []);
    }

    [Fact]
    public void Comment_UsesTheConfiguredDatumId()
    {
        var factory = new PduFactory(Options.Create(new SandboxOptions { CommentDatumId = 950001 }), _time);
        var pdu = PluginComment.Unmarshal(factory.Comment(Hamburg(pairs: [new("a", "b")])));
        Assert.Equal(950001u, pdu.VariableDatums![0].DatumId);
    }
}
