// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu.Emissions;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

public sealed class EmissionsTests
{
    private static PduHeader HeaderFor(DisPduType pduType, ushort length) =>
        PduHeader.ForV6(1, pduType, DisProtocolFamily.DistributedEmissionRegeneration, length);

    [Fact]
    public void ElectromagneticEmission_RoundTrip_WithTypedSystemBeamTrackJam()
    {
        var trackJam = new TrackJamData(
            EntityId: new EntityId(2, 2, 900),
            EmitterNumber: 1,
            BeamNumber: 2);
        var fundamental = new FundamentalParameterData(
            Frequency: 9e9f,
            FrequencyRange: 1e6f,
            EffectiveRadiatedPower: 80f,
            PulseRepetitionFrequency: 1000f,
            PulseWidth: 0.001f);
        var beam = new ElectromagneticEmissionBeam(
            BeamNumber: 1,
            BeamParameterIndex: 42,
            FundamentalParameters: fundamental,
            BeamAzimuthCenter: 0f,
            BeamAzimuthSweep: 0.1f,
            BeamElevationCenter: 0f,
            BeamElevationSweep: 0.05f,
            BeamSweepSync: 0f,
            BeamFunction: 3,
            HighDensityTrackJam: 0,
            JammingModeSequence: 0,
            TrackJamTargets: new[] { trackJam });
        var system = new ElectromagneticEmissionSystem(
            EmitterName: 100, EmitterFunction: 5, EmitterIdNumber: 1,
            Location: new Vector3Float(0.5f, 0f, 1f),
            Beams: new[] { beam });

        var original = new ElectromagneticEmissionPdu(
            HeaderFor(DisPduType.ElectromagneticEmission, 0),
            EmittingEntityId: new EntityId(1, 1, 100),
            EventId: new EventId(1, 1, 7),
            StateUpdateIndicator: 1,
            Systems: new[] { system });

        var bytes = original.Marshal();
        Assert.Equal((byte)DisPduType.ElectromagneticEmission, bytes[2]);

        var decoded = ElectromagneticEmissionPdu.Unmarshal(bytes);
        Assert.Single(decoded.Systems);
        Assert.Equal(100, decoded.Systems[0].EmitterName);
        Assert.Single(decoded.Systems[0].Beams);
        var decBeam = decoded.Systems[0].Beams[0];
        Assert.Equal(fundamental, decBeam.FundamentalParameters);
        Assert.Single(decBeam.TrackJamTargets);
        Assert.Equal(trackJam, decBeam.TrackJamTargets[0]);
    }

    [Fact]
    public void Designator_RoundTrip_PreservesAllFields()
    {
        var original = new DesignatorPdu(
            HeaderFor(DisPduType.Designator, DesignatorPdu.WireLength),
            DesignatingEntityId: new EntityId(1, 1, 100),
            CodeName: 42,
            DesignatedEntityId: new EntityId(1, 1, 200),
            DesignatorCode: 1688,
            DesignatorPower: 50f,
            DesignatorWavelength: 1.064e-6f,
            DesignatorSpotWrtDesignated: new Vector3Float(1f, 0.5f, 0f),
            DesignatorSpotLocation: new Vector3Double(3765000.0, 661000.0, 5108000.0),
            DeadReckoningAlgorithm: DeadReckoningAlgorithm.Static,
            EntityLinearAcceleration: Vector3Float.Zero);

        var bytes = original.Marshal();
        Assert.Equal(DesignatorPdu.WireLength, bytes.Length);
        Assert.Equal((byte)DisPduType.Designator, bytes[2]);

        var decoded = DesignatorPdu.Unmarshal(bytes);
        Assert.Equal(original.DesignatingEntityId, decoded.DesignatingEntityId);
        Assert.Equal(42, decoded.CodeName);
        Assert.Equal(original.DesignatedEntityId, decoded.DesignatedEntityId);
        Assert.Equal(1688, decoded.DesignatorCode);
        Assert.Equal(50f, decoded.DesignatorPower);
        Assert.Equal(1.064e-6f, decoded.DesignatorWavelength);
        Assert.Equal(original.DesignatorSpotWrtDesignated, decoded.DesignatorSpotWrtDesignated);
        Assert.Equal(original.DesignatorSpotLocation, decoded.DesignatorSpotLocation);
        Assert.Equal(DeadReckoningAlgorithm.Static, decoded.DeadReckoningAlgorithm);
    }

    [Fact]
    public void UnderwaterAcoustic_RoundTrip_WithTypedShaftApaEmitterSystems()
    {
        var shafts = new[]
        {
            new UnderwaterAcousticShaft(120, 130, 5f),
            new UnderwaterAcousticShaft(140, 130, -5f),
        };
        var apas = new[]
        {
            new UnderwaterAcousticApa(7, 42),
        };
        var beam = new UnderwaterAcousticBeam(
            BeamDataLength: 6,
            BeamIdNumber: 1,
            AcousticBeamParameterIndex: 1,
            ActiveEmissionParameterIndex: 1,
            ScanPattern: 0,
            BeamCenterAzimuth: 0f,
            AzimuthalBeamwidth: 0.1f,
            BeamCenterDepressionElevation: 0f,
            DepressionElevationBeamwidth: 0.05f);
        var emitterSystem = new UnderwaterAcousticEmitterSystem(
            NumberOfBeams: 1,
            AcousticEmitterSystemName: 500,
            AcousticEmitterFunction: 1,
            AcousticEmitterIdNumber: 1,
            EmitterLocation: new Vector3Float(0f, 0f, -3f),
            Beams: new[] { beam });

        var original = new UnderwaterAcousticPdu(
            HeaderFor(DisPduType.UnderwaterAcoustic, 0),
            EmittingEntityId: new EntityId(1, 1, 500),
            EventId: new EventId(1, 1, 11),
            StateChangeIndicator: 0,
            PassiveParameterIndex: 42,
            PropulsionPlantConfiguration: 1,
            Shafts: shafts,
            Apas: apas,
            EmitterSystems: new[] { emitterSystem });

        var decoded = UnderwaterAcousticPdu.Unmarshal(original.Marshal());
        Assert.Equal(2, decoded.Shafts.Count);
        Assert.Equal(shafts[0], decoded.Shafts[0]);
        Assert.Single(decoded.Apas);
        Assert.Equal(apas[0], decoded.Apas[0]);
        Assert.Single(decoded.EmitterSystems);
        Assert.Single(decoded.EmitterSystems[0].Beams);
        Assert.Equal(beam, decoded.EmitterSystems[0].Beams[0]);
    }

    [Fact]
    public void SupplementalEmissionEntityState_RoundTrip_WithPropulsionAndVectoringSystems()
    {
        var propulsion = new[]
        {
            new PropulsionSystem(PowerSetting: 0.8f, EngineRpm: 8000f),
            new PropulsionSystem(PowerSetting: 0.75f, EngineRpm: 7800f),
        };
        var vectoring = new[]
        {
            new VectoringNozzleSystem(HorizontalDeflectionAngle: 0.1f, VerticalDeflectionAngle: -0.05f),
        };

        var original = new SupplementalEmissionEntityStatePdu(
            Header: PduHeader.ForV7(1, DisPduType.SupplementalEmissionEntityState,
                DisProtocolFamily.DistributedEmissionRegeneration, 0),
            OriginatingEntityId: new EntityId(1, 1, 100),
            InfraredSignatureIndex: 10,
            AcousticSignatureIndex: 20,
            RadarCrossSectionSignatureIndex: 30,
            PropulsionSystems: propulsion,
            VectoringNozzleSystems: vectoring);

        var bytes = original.Marshal();
        var expected =
            SupplementalEmissionEntityStatePdu.MinimumWireLength
            + (propulsion.Length * PropulsionSystem.WireLength)
            + (vectoring.Length * VectoringNozzleSystem.WireLength);
        Assert.Equal(expected, bytes.Length);
        Assert.Equal(7, bytes[0]); // V7 header byte

        var decoded = SupplementalEmissionEntityStatePdu.Unmarshal(bytes);
        Assert.Equal(2, decoded.PropulsionSystems!.Count);
        Assert.Equal(0.8f, decoded.PropulsionSystems[0].PowerSetting);
        Assert.Equal(8000f, decoded.PropulsionSystems[0].EngineRpm);
        Assert.Single(decoded.VectoringNozzleSystems!);
        Assert.Equal(0.1f, decoded.VectoringNozzleSystems![0].HorizontalDeflectionAngle);
    }
}
