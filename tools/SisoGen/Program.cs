// Copyright 2026 Küstenlogik
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using System.Xml.Linq;

namespace Kuestenlogik.Bowire.Protocol.Dis.Tools.SisoGen;

/// <summary>
/// One-shot generator that turns the canonical SISO-REF-010 XML
/// (downloaded from <see href="https://www.sisostandards.org/page/ReferenceDocuments"/>)
/// into C# enums committed under
/// <c>src/Kuestenlogik.Bowire.Protocol.Dis/Enumerations/Generated/</c>.
/// Output overwrites hand-seeded subsets.
/// </summary>
/// <remarks>
/// <para>Invocation:</para>
/// <code>
/// dotnet run --project tools/SisoGen -- --xml path/to/SISO-REF-010.xml
/// </code>
/// <para>
/// Generated files are committed — the generator runs only when SISO
/// publishes a new revision. CI doesn't invoke it.
/// </para>
/// <para>
/// This first version targets the Country enumeration (enum id 29
/// in SISO-REF-010). Additional enums — Entity Kind, Domain, Entity
/// Types, Munition Types, Modulation Systems, ... — are scheduled
/// for a follow-up slice. The architecture here (one
/// <c>EnumSpec</c> per target, each rendered by a shared template)
/// scales to the full ~50 enumerations the DIS spec pulls from SISO.
/// </para>
/// </remarks>
internal sealed class Program
{
    private static int Main(string[] args)
    {
        var xmlPath = GetArg(args, "--xml");
        var outputDir = GetArg(args, "--out") ?? TryFindDefaultOutputDir();

        if (xmlPath is null || outputDir is null)
        {
            PrintUsage();
            return 2;
        }
        if (!File.Exists(xmlPath))
        {
            Console.Error.WriteLine($"xml file not found: {xmlPath}");
            return 2;
        }

        Directory.CreateDirectory(outputDir);

        var doc = XDocument.Load(xmlPath);
        // Enums on this list have NOT been hand-typed elsewhere under
        // Enumerations/. Hand-typed ones (DetonationResult,
        // DeadReckoningAlgorithm, StopFreezeReason, AcknowledgeFlag,
        // ResponseFlag, ServiceTypeRequested, RequiredReliabilityService,
        // DisPduType, DisProtocolVersion, DisProtocolFamily, ForceId)
        // stay under Enumerations/ — we don't double-generate them to
        // avoid drift.
        var specs = new[]
        {
            new EnumSpec("Country", "ushort", 29,
                "SISO-REF-010 uid 29 — DIS country code used in EntityType and related records."),
            new EnumSpec("EntityKind", "byte", 7,
                "SISO-REF-010 uid 7 — top-level classification of an entity (Platform, Munition, Life Form, Environmental, Cultural Feature, Supply, Radio, Expendable, Sensor/Emitter)."),
            new EnumSpec("PlatformDomain", "byte", 8,
                "SISO-REF-010 uid 8 — platform domain code (Land, Air, Surface, Subsurface, Space) used in EntityType.Domain for Kind=1."),
            new EnumSpec("MunitionDomain", "byte", 14,
                "SISO-REF-010 uid 14 — munition domain code used in EntityType.Domain for Kind=2."),
            new EnumSpec("WarheadType", "ushort", 60,
                "SISO-REF-010 uid 60 — warhead type used in MunitionDescriptor.Warhead."),
            new EnumSpec("FuseType", "ushort", 61,
                "SISO-REF-010 uid 61 — fuse type used in MunitionDescriptor.Fuse."),
            new EnumSpec("EmitterName", "ushort", 75,
                "SISO-REF-010 uid 75 — specific emitter-name code for electromagnetic emission systems (radars, jammers). Large table, ~2800 entries."),
            new EnumSpec("EmitterSystemFunction", "byte", 76,
                "SISO-REF-010 uid 76 — high-level function of an emitter system (early warning, air-search, missile acquisition, ...)."),
            new EnumSpec("BeamFunction", "byte", 78,
                "SISO-REF-010 uid 78 — function of an electromagnetic-emission beam (search, acquisition, tracking, illumination, jamming, ...)."),
        };

        foreach (var spec in specs)
        {
            var entries = ReadEntries(doc, spec.SisoEnumId);
            if (entries.Count == 0)
            {
                Console.Error.WriteLine($"warning: no entries found for enum id {spec.SisoEnumId} ({spec.Name}).");
                continue;
            }
            var rendered = Render(spec, entries);
            var path = Path.Combine(outputDir, $"{spec.Name}.g.cs");
            File.WriteAllText(path, rendered);
            Console.WriteLine($"wrote {entries.Count,4} entries → {path}");
        }
        return 0;
    }

    private record EnumSpec(string Name, string UnderlyingType, int SisoEnumId, string Summary);

    private record Entry(int Value, string Name, string? Description);

    private static List<Entry> ReadEntries(XDocument doc, int sisoEnumId)
    {
        // SISO-REF-010's canonical XML schema wraps each enumeration
        // in <enum uid="..."> (older revisions used id="...") with
        // <enumrow value=".." description=".."/> children. The
        // namespace varies across SISO revisions, so match by
        // local-name / attribute instead of a fixed XNamespace.
        var entries = new List<Entry>();
        var enumElement = doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "enum", StringComparison.Ordinal) &&
                                 MatchesEnumId(e, sisoEnumId));
        if (enumElement is null) return entries;

        foreach (var row in enumElement.Elements().Where(e => e.Name.LocalName == "enumrow"))
        {
            if (!int.TryParse((string?)row.Attribute("value"), out var value)) continue;
            var description = (string?)row.Attribute("description") ?? "";
            var name = SanitizeIdentifier(description);
            if (string.IsNullOrEmpty(name)) continue;
            entries.Add(new Entry(value, name, description));
        }
        return entries;
    }

    private static bool MatchesEnumId(XElement element, int sisoEnumId)
    {
        // Accept both attribute spellings. V2.x schemas (current SISO
        // revisions, V33+) use `uid`; earlier revisions used `id`.
        var uid = (string?)element.Attribute("uid") ?? (string?)element.Attribute("id");
        return int.TryParse(uid, out var parsed) && parsed == sisoEnumId;
    }

    private static string SanitizeIdentifier(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "";
        var sb = new StringBuilder(description.Length);
        var capitalizeNext = true;
        foreach (var ch in description)
        {
            if (ch is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(ch) : ch);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }
        // Prepend underscore when the sanitised name starts with a digit.
        if (sb.Length > 0 && sb[0] is >= '0' and <= '9') sb.Insert(0, '_');
        return sb.ToString();
    }

    private static string Render(EnumSpec spec, List<Entry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("// Generated from SISO-REF-010 by tools/SisoGen. Do not edit by hand.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine();
        sb.AppendLine("namespace Kuestenlogik.Bowire.Protocol.Dis.Enumerations.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// " + System.Security.SecurityElement.Escape(spec.Summary));
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public enum {spec.Name} : {spec.UnderlyingType}");
        sb.AppendLine("{");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.OrderBy(e => e.Value))
        {
            var name = entry.Name;
            // Keep names unique — two entries with the same
            // sanitised identifier get a numeric suffix.
            var baseName = name;
            var counter = 2;
            while (!seen.Add(name)) name = $"{baseName}_{counter++}";

            var summary = entry.Description is null
                ? entry.Name
                : System.Security.SecurityElement.Escape(entry.Description);
            sb.AppendLine($"    /// <summary>{summary}.</summary>");
            sb.AppendLine($"    {name} = {entry.Value},");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    /// <summary>
    /// When the tool runs from a checked-out Bowire.Protocol.Dis
    /// repo (via `dotnet run --project tools/SisoGen`), default the
    /// output directory to the plugin's Generated/ folder so the
    /// maintainer doesn't have to type it every time. Returns null
    /// when we can't find the repo — e.g. the tool is installed
    /// globally — so the caller falls through to the usage prompt.
    /// </summary>
    private static string? TryFindDefaultOutputDir()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("Bowire.Protocol.Dis.slnx").Length > 0)
            {
                return Path.Combine(dir.FullName,
                    "src", "Kuestenlogik.Bowire.Protocol.Dis", "Enumerations", "Generated");
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("usage: siso-gen --xml <SISO-REF-010.xml> --out <dir>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  --xml <path>   Path to the SISO-REF-010 XML document.");
        Console.Error.WriteLine("                 Official: https://www.sisostandards.org/page/ReferenceDocuments (login).");
        Console.Error.WriteLine("                 Mirror:   https://raw.githubusercontent.com/open-dis/opendis7-source-generator/master/xml/SISO/SISO-REF-010.xml");
        Console.Error.WriteLine("  --out <dir>    Target directory for generated C# enums. Optional when");
        Console.Error.WriteLine("                 invoked from inside a Bowire.Protocol.Dis checkout —");
        Console.Error.WriteLine("                 then it defaults to src/Kuestenlogik.Bowire.Protocol.Dis/Enumerations/Generated.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Install as a .NET tool:");
        Console.Error.WriteLine("  dotnet tool install -g Kuestenlogik.Bowire.Protocol.Dis.SisoGen");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  siso-gen --xml SISO-REF-010.xml --out ./src/MyProject/Enums/Generated");
    }
}
