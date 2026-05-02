// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.LiveEntity;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class LiveEntityTests
{
    private static PduHeader HeaderFor(DisPduType pduType) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.LiveEntity, 0);

    [Fact]
    public void Tspi_RoundTrip_PreservesEntityIdAndPayload()
    {
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var original = new TimeSpacePositionInformationPdu(
            HeaderFor(DisPduType.TimeSpacePositionInformation),
            LiveEntityId: new LiveEntityId(Site: 1, Application: 2, Entity: 300),
            Payload: payload);

        var bytes = original.Marshal();
        Assert.Equal(TimeSpacePositionInformationPdu.MinimumWireLength + payload.Length, bytes.Length);
        Assert.Equal((byte)DisPduType.TimeSpacePositionInformation, bytes[2]);
        Assert.Equal((byte)DisProtocolFamily.LiveEntity, bytes[3]);

        var decoded = TimeSpacePositionInformationPdu.Unmarshal(bytes);
        Assert.Equal(new LiveEntityId(1, 2, 300), decoded.LiveEntityId);
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void Appearance_RoundTrip()
    {
        var payload = new byte[] { 0xDE, 0xAD };
        var original = new AppearancePdu(
            HeaderFor(DisPduType.Appearance),
            LiveEntityId: new LiveEntityId(1, 1, 100),
            Payload: payload);
        var decoded = AppearancePdu.Unmarshal(original.Marshal());
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void ArticulatedParts_RoundTrip_WithTypedVariableParameters()
    {
        var parameters = new VariableParameter[]
        {
            new ArticulatedPartParameter(
                ChangeIndicator: 1,
                PartAttachedTo: 0,
                ParameterType: 0x0000_1001,
                ParameterValue: 0x1234_5678_9ABC_DEF0UL),
            new AttachedPartParameter(
                DetachedIndicator: 0,
                PartAttachedTo: 0,
                ParameterType: 1,
                AttachedPartType: new EntityType(1, 1, 225, 1, 1, 0, 0)),
        };
        var original = new ArticulatedPartsPdu(
            HeaderFor(DisPduType.ArticulatedParts),
            LiveEntityId: new LiveEntityId(1, 1, 100),
            VariableParameters: parameters);
        var decoded = ArticulatedPartsPdu.Unmarshal(original.Marshal());
        Assert.Equal(2, decoded.VariableParameters.Count);
        Assert.IsType<ArticulatedPartParameter>(decoded.VariableParameters[0]);
        Assert.IsType<AttachedPartParameter>(decoded.VariableParameters[1]);
    }

    [Fact]
    public void LeFire_RoundTrip()
    {
        var payload = new byte[] { 0xAA };
        var original = new LiveEntityFirePdu(
            HeaderFor(DisPduType.LiveEntityFire),
            LiveEntityId: new LiveEntityId(1, 1, 100),
            Payload: payload);
        var decoded = LiveEntityFirePdu.Unmarshal(original.Marshal());
        Assert.Equal(payload, decoded.Payload);
    }

    [Fact]
    public void LeDetonation_RoundTrip()
    {
        var payload = new byte[] { 0xBB, 0xCC };
        var original = new LiveEntityDetonationPdu(
            HeaderFor(DisPduType.LiveEntityDetonation),
            LiveEntityId: new LiveEntityId(1, 1, 100),
            Payload: payload);
        var decoded = LiveEntityDetonationPdu.Unmarshal(original.Marshal());
        Assert.Equal(payload, decoded.Payload);
    }
}
