// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;
using Microsoft.Extensions.Options;
using OpenDisNet;
using OpenDisNet.Enumerations;
using OpenDisNet.Pdus;

namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

/// <summary>
/// Turns an entity into DIS v7 bytes with OpenDisNet: an Entity State PDU
/// for where it is, a Comment PDU for its key-value pairs.
/// </summary>
internal sealed class PduFactory(IOptions<SandboxOptions> options, TimeProvider time)
{
    public const int MarkingLength = 11;

    /// <summary>Appearance bit 23, "State": deactivated. Same bit for land, air, surface and life forms.</summary>
    public const uint DeactivatedAppearance = 1u << 23;

    private const ushort AllIds = 0xFFFF;

    private readonly SandboxOptions _options = options.Value;

    public byte[] EntityState(SandboxEntity entity, bool deactivated = false)
    {
        var preset = EntityPreset.Get(entity.PresetId);
        var location = GeoConversion.ToEcef(entity.Position);
        var pdu = DisPduBuilder.CreateEntityState(
            new EntityId(_options.SiteId, _options.ApplicationId, entity.Number),
            new EntityType
            {
                EntityKind = (EntityKind)preset.Kind,
                Domain = new Domain { Value = preset.Domain },
                Country = (Country)preset.Country,
                Category = preset.Category,
                SubCategory = preset.Subcategory,
            },
            new Vector3Double { X = location.X, Y = location.Y, Z = location.Z },
            (ForceId)(byte)entity.Force,
            _options.ExerciseId);

        // OpenDisNet leaves the timestamp at 0.
        pdu.Timestamp = Timestamp();

        var orientation = GeoConversion.ToDisOrientation(entity.Position, entity.HeadingDegrees);
        pdu.EntityOrientation = new EulerAngles
        {
            Psi = (float)orientation.Psi,
            Theta = (float)orientation.Theta,
            Phi = (float)orientation.Phi,
        };

        var moving = entity.Following && entity.SpeedMetresPerSecond > 0;
        var velocity = moving
            ? GeoConversion.VelocityToEcef(entity.Position, entity.SpeedMetresPerSecond, entity.HeadingDegrees)
            : default;
        pdu.EntityLinearVelocity = new Vector3Float { X = (float)velocity.X, Y = (float)velocity.Y, Z = (float)velocity.Z };
        pdu.DeadReckoningParameters = new DeadReckoningParameters
        {
            DeadReckoningAlgorithm = moving
                ? DeadReckoningAlgorithm.DrmFpwConstantVelocityLowAccelerationLinearMotionEntity
                : DeadReckoningAlgorithm.StaticNonMovingEntity,
        };

        pdu.EntityAppearance = deactivated ? DeactivatedAppearance : 0u;
        pdu.Marking = new EntityMarking { CharacterSet = EntityMarkingCharacterSet.Ascii, Characters = Marking(entity.Name) };
        return DisSerializer.Serialize(pdu);
    }

    public byte[] Comment(SandboxEntity entity)
    {
        var pdu = new CommentPdu
        {
            ExerciseId = _options.ExerciseId,
            Timestamp = Timestamp(),
            // DIS 7 names a simulation here, not an entity. Site, application
            // and reference number have the entity id's layout, so the entity
            // number goes in the reference number.
            OriginatingId = Simulation(_options.SiteId, _options.ApplicationId, entity.Number),
            ReceivingId = Simulation(AllIds, AllIds, AllIds),
        };

        foreach (var pair in entity.Pairs)
        {
            var value = Encoding.UTF8.GetBytes(PairRules.Text(pair));
            pdu.VariableDatums.Add(new VariableDatum
            {
                VariableDatumId = (VariableRecordType)_options.CommentDatumId,
                VariableDatumLength = (uint)value.Length * 8,
                VariableDatumValue = value,
            });
        }
        return DisSerializer.Serialize(pdu);
    }

    /// <summary>
    /// DIS relative timestamp: time past the full hour in units of
    /// 3600 s / (2^31 − 1), in bits 31..1; bit 0 clear means relative.
    /// </summary>
    public uint Timestamp()
    {
        var now = time.GetUtcNow();
        var sinceHour = now - new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        var units = (uint)(sinceHour.TotalSeconds / 3600.0 * int.MaxValue);
        return units << 1;
    }

    /// <summary>
    /// Exactly 11 bytes: OpenDisNet writes the array as long as it is, and
    /// anything else makes the PDU shorter than an Entity State PDU is.
    /// </summary>
    public static byte[] Marking(string name)
    {
        var bytes = new byte[MarkingLength];
        var i = 0;
        foreach (var rune in name.EnumerateRunes())
        {
            if (i == MarkingLength)
                break;
            bytes[i++] = rune.Value is >= 0x20 and <= 0x7E ? (byte)rune.Value : (byte)'?';
        }
        return bytes;
    }

    /// <summary>The marking as Bowire will show it, for the editor.</summary>
    public static string MarkingText(string name) => Encoding.ASCII.GetString(Marking(name)).TrimEnd('\0');

    private static SimulationIdentifier Simulation(ushort site, ushort application, ushort reference) => new()
    {
        SimulationAddress = new SimulationAddress { Site = site, Application = application },
        ReferenceNumber = reference,
    };
}
