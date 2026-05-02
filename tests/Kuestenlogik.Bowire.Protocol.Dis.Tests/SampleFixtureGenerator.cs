// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Kuestenlogik.Bowire.Protocol.Dis.Enumerations;
using Kuestenlogik.Bowire.Protocol.Dis.Pdu;
using Kuestenlogik.Bowire.Protocol.Dis.Records;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tests;

/// <summary>
/// One-shot fixture generator for the convoy sample recording shipped
/// in <c>samples/convoy.bowire-recording.json</c>. Built from the
/// same <see cref="EntityStatePdu"/> users would compose themselves,
/// so the fixture stays deterministic and regeneratable. Marked with
/// a skip message so the normal test run doesn't clobber the committed
/// sample; un-skip and rerun when the fixture needs to change.
/// </summary>
public sealed class SampleFixtureGenerator
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [Fact(Skip = "Manual regeneration only. Un-skip and run to rewrite samples/convoy.bowire-recording.json, then re-skip before committing.")]
    public void RegenerateConvoySample()
    {
        var baseLocation = new Vector3Double(3_765_000.0, 661_000.0, 5_108_000.0);
        var entityType = new EntityType(
            Kind: 1 /* platform */,
            Domain: 1 /* land */,
            Country: 225 /* USA */,
            Category: 1 /* tank */,
            Subcategory: 1 /* M1 */,
            Specific: 0, Extra: 0);

        var pdus = Enumerable.Range(0, 3).Select(i =>
        {
            var location = new Vector3Double(
                baseLocation.X + (100.0 * i),
                baseLocation.Y + (50.0 * i),
                baseLocation.Z);
            var pdu = new EntityStatePdu(
                Header: PduHeader.ForV6(
                    exerciseId: 1,
                    pduType: DisPduType.EntityState,
                    family: DisProtocolFamily.EntityInformation,
                    length: EntityStatePdu.MinimumWireLength),
                EntityId: new EntityId(1, 1, 1000),
                Force: ForceId.Friendly,
                EntityType: entityType,
                AlternativeEntityType: entityType,
                LinearVelocity: Vector3Float.Zero,
                Location: location,
                Orientation: EulerAngles.Zero,
                Appearance: 0,
                DeadReckoning: DeadReckoningParameters.Default,
                Marking: EntityMarking.Ascii("BOWIRE01"),
                Capabilities: 0);
            return pdu.Marshal();
        }).ToArray();

        var steps = pdus.Select((pdu, i) => new
        {
            id = "pdu_" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
            capturedAt = i * 100L,
            protocol = "dis",
            service = "EntityState",
            method = "Send",
            methodType = "Unary",
            status = "OK",
            responseBinary = Convert.ToBase64String(pdu),
            metadata = i == 0
                ? new Dictionary<string, string>
                {
                    ["multicast-group"] = "239.1.2.3",
                    ["port"] = "3000",
                    ["ttl"] = "1"
                }
                : null
        }).ToArray();

        var recording = new
        {
            id = "rec_dis_convoy",
            name = "dis convoy sample",
            description = "Three Entity State PDUs broadcast on 239.1.2.3:3000, stepping an M1 tank ~100m/update.",
            createdAt = DateTimeOffset.Parse(
                "2026-04-23T12:00:00Z",
                System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeMilliseconds(),
            recordingFormatVersion = 2,
            steps
        };

        var repoRoot = LocateRepoRoot();
        var samplesDir = Path.Combine(repoRoot, "samples");
        Directory.CreateDirectory(samplesDir);
        var outPath = Path.Combine(samplesDir, "convoy.bowire-recording.json");
        File.WriteAllText(outPath, JsonSerializer.Serialize(recording, s_jsonOptions));
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("Bowire.Protocol.Dis.slnx").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Couldn't find Bowire.Protocol.Dis.slnx walking up from " + AppContext.BaseDirectory);
    }
}
