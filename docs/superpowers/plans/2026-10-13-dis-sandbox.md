# DIS-Sandbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ein eigenständiges Blazor-Sample, in dem man Objekte auf einer MapLibre-Karte anlegt, bewegt und mit Key-Value-Paaren versieht, und das jede Änderung als DIS-v7-PDU (`EntityState`, `Comment`) per UDP sendet, damit man Bowires DIS-Tab von Hand antreiben kann.

**Architecture:** Ein `EntityStore` (Singleton) hält die Objekte und meldet Änderungen per Event. Ein `DisPublisher` (Hosted Service) setzt jede Änderung über eine `PduFactory` mit OpenDisNet in PDUs um und gibt sie an eine `IDatagramSink` (UDP). Die Blazor-Oberfläche (Interactive Server) spricht über ein dünnes JS-Modul mit MapLibre und schreibt nur in den Store. `GeoConversion` ist die eigene, vom Plugin unabhängige Geometrie.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server), MapLibre GL JS 5.18.0 (unpkg), OpenDisNet 1.2.0, xUnit v3 auf Microsoft.Testing.Platform, Microsoft.Extensions.TimeProvider.Testing.

**Spec:** `docs/superpowers/specs/2026-10-13-dis-sandbox-design.md`. Ticket: Kuestenlogik/Bowire.Protocol.Dis#67.

## Global Constraints

- Repo: `C:\Projekte\Kuestenlogik\Bowire.Protocol.Dis`. Alle Pfade unten sind relativ dazu.
- `Directory.Build.props` gilt für alle Projekte: `net10.0`, `LangVersion 14.0`, `Nullable enable`, `AnalysisLevel latest-all`, `EnforceCodeStyleInBuild true`, **`TreatWarningsAsErrors true`**. Jeder Build muss ohne Warnung durchgehen. Analyzer-Befunde werden in dem Schritt behoben, in dem sie auftreten. Unterdrückt wird nur, was der Plan ausdrücklich unterdrückt.
- Central Package Management: Versionen stehen nur in `Directory.Packages.props`, `PackageReference` ohne `Version`.
- Die Sandbox referenziert **OpenDisNet 1.2.0 und nicht das Plugin-Projekt**. Nur das Testprojekt referenziert beide: Es vergleicht den fremden Encoder mit Bowires Decoder.
- Jede neue `.cs`-Datei beginnt mit:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0
  ```
- OpenDisNet schreibt `EntityMarking.Characters` so lang, wie das Array ist. **Das Marking muss genau 11 Byte haben.** Sonst wird die PDU 141 statt 144 Byte lang, und der Decoder des Plugins wirft (im Spike am 2026-10-13 nachgewiesen).
- OpenDisNet lässt `Timestamp` auf 0. Die Sandbox setzt ihn selbst (relativ, Bit 0 = 0).
- Comment-PDU: In DIS 7 ist `OriginatingId` ein `SimulationIdentifier` (Site, Application, ReferenceNumber). Auf dem Draht ist das dasselbe Layout wie eine Entity-ID, die Entity-Nummer geht in `ReferenceNumber`.
- Defaults (aus der Spec): Target `239.1.2.3:3000`, Ttl 1, ExerciseId 1, SiteId 1, ApplicationId 3001, HeartbeatSeconds 5, FollowMaxUpdatesPerSecond 10, CommentDatumId 900000, InitialView 53.55/9.99/Zoom 11, Comment-PDU höchstens 1400 Byte, Deactivated = Appearance-Bit 23.
- Entity-Typen: Kampfpanzer 1.1.78.1.0, LKW 1.1.78.7.0, Hubschrauber 1.2.78.21.0 (500 m), Flugzeug 1.2.78.4.0 (500 m), Schiff 1.3.78.50.0, Person 3.1.78.11.0.
- UI-Sprache Deutsch. README Englisch, wie das vorhandene Sample.
- Tests laufen unter Microsoft.Testing.Platform: `dotnet test --project <csproj>`. **Keine VSTest-Flags** (`--nologo`, `-v q`, `--logger`). Die gehen an die xUnit-Exe und enden als „0 tests, exit 5“. Filtern mit `--filter-class <voll qualifizierter Klassenname>`.
- Commits: Identität `thomas.stegemann@kuestenlogik.com`, kein Co-Author-Trailer, Stil des Repos (`feat(sandbox): …`, deutsch). Es wird direkt auf `main` committet, wie im Repo üblich. Gepusht wird am Ende (Task 8).

## Review Focus

1. **Folgen über den Antimeridian** (179.9995° → −179.9995°): Erwartet werden etwa 111 m/s nach Osten, nicht 40.000 km/s nach Westen. Test in Task 3 (`Move_AcrossTheAntimeridian_IsAShortHopEast`).
2. **Zwei Folge-Bewegungen im selben Zeitpunkt** (verstrichene Zeit 0): Geschwindigkeit und Kurs bleiben, wie sie waren, kein `NaN`/`Infinity` in der PDU. Test in Task 3 (`Move_WithNoElapsedTime_KeepsSpeedAndHeading`).
3. **Schlüssel, die sich nicht als `key=value` senden lassen** (leer, mit `=`, doppelt): Abgelehnt mit Meldung, nichts gesendet. Test in Task 3 (`TrySetPairs_RejectsKeysThatCannotBeSent`).
4. **Kein Netz bzw. Senden schlägt dauerhaft fehl:** Weder `OnChanged` noch `Tick` werfen, der Fehlerzähler steigt, der Heartbeat läuft weiter. Test in Task 5 (`SinkFailures_AreCountedAndNeverEscape`).
5. **Objekt wird gelöscht, während eine Folge-Bewegung noch aussteht:** Nach dem Deaktivieren geht für dieses Objekt nichts mehr raus. Test in Task 5 (`RemovingAFollowedEntity_DropsItsPendingMove`).

---

## Dateistruktur

```
samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/
  Kuestenlogik.Bowire.Protocol.Dis.Sandbox.csproj   Web-SDK, OpenDisNet, InternalsVisibleTo Tests
  Program.cs                                        DI, Blazor, Hosted Service
  appsettings.json                                  Abschnitt "Sandbox" mit Defaults + Basemaps
  Properties/launchSettings.json                    http://localhost:5185
  SandboxOptions.cs                                 Optionen + MapView + BasemapOption
  SandboxOptionsValidator.cs                        Prüfung beim Start
  Geo/GeoConversion.cs                              WGS84 <-> ECEF, Kurs <-> DIS-Euler, Geschwindigkeit
  Entities/EntityPreset.cs                          die sechs Typ-Voreinstellungen
  Entities/SandboxEntity.cs                         Force, SandboxEntity, EntityChange(Kind)
  Entities/PairRules.cs                             Gültigkeit + Wire-Länge der Key-Value-Paare
  Entities/EntityStore.cs                           Objekte, Nummern, Kurs/Geschwindigkeit beim Folgen
  Publishing/PduFactory.cs                          SandboxEntity -> EntityState-/Comment-Bytes (OpenDisNet)
  Publishing/IDatagramSink.cs                       Senke für Datagramme
  Publishing/UdpDatagramSink.cs                     UDP/Multicast
  Publishing/PublisherStatus.cs                     Zähler, letzter Fehler, Kodier-Fehler je Objekt
  Publishing/DisPublisher.cs                        Änderung -> PDUs, Folge-Drosselung, Heartbeat
  Components/App.razor, Routes.razor, _Imports.razor
  Components/Layout/MainLayout.razor
  Components/StatusBar.razor
  Components/MapView.razor                          JS-Interop-Hülle um sandbox-map.js
  Components/EntityList.razor
  Components/EntityEditor.razor
  Components/Pages/Home.razor
  wwwroot/app.css
  wwwroot/sandbox-map.js                            MapLibre: Basemaps, Marker, Drag, Maus folgen
  README.md
tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/
  Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.csproj
  GlobalUsings.cs
  SandboxOptionsValidatorTests.cs
  GeoConversionTests.cs
  EntityStoreTests.cs
  PduFactoryDecoderTests.cs
  DisPublisherTests.cs
  RecordingSink.cs
Bowire.Protocol.Dis.slnx                            + beide Projekte
Directory.Packages.props                            + OpenDisNet, + TimeProvider.Testing
README.md                                           Abschnitt Samples um die Sandbox ergänzen
```

---

### Task 1: Projektgerüst, Optionen und ihre Prüfung

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `Bowire.Protocol.Dis.slnx`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.csproj`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Program.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/appsettings.json`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Properties/launchSettings.json`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/SandboxOptions.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/SandboxOptionsValidator.cs`
- Create: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.csproj`
- Create: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/GlobalUsings.cs`
- Test: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/SandboxOptionsValidatorTests.cs`

**Interfaces:**
- Produces: `SandboxOptions` (namespace `Kuestenlogik.Bowire.Protocol.Dis.Sandbox`) mit `Section`, `Target`, `Ttl`, `ExerciseId` (byte), `SiteId`/`ApplicationId` (ushort), `HeartbeatSeconds` (double), `FollowMaxUpdatesPerSecond` (int), `CommentDatumId` (uint), `InitialView` (`MapView`: `Latitude`, `Longitude`, `Zoom`), `Basemaps` (`IList<BasemapOption>`: `Id`, `Name`, `Kind` = `"style"|"raster"|"blank"`, `Url`, `Tiles`, `Attribution`, `Color`), `TargetEndPoint` (`IPEndPoint`). `SandboxOptionsValidator : IValidateOptions<SandboxOptions>`.

- [ ] **Step 1: Pakete in `Directory.Packages.props` eintragen**

  In der vorhandenen `<ItemGroup>`, unter dem Eintrag `Kuestenlogik.Bowire`:
  ```xml
      <!-- DIS sandbox sample: an independent DIS v7 encoder, so the sandbox
           tests Bowire's decoder against code it does not share. -->
      <PackageVersion Include="OpenDisNet" Version="1.2.0" />
  ```
  Unter `<!-- Test-only -->`:
  ```xml
      <PackageVersion Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.8.0" />
  ```

- [ ] **Step 2: Sandbox-Projekt anlegen**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.csproj`:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk.Web">
    <PropertyGroup>
      <IsPackable>false</IsPackable>
      <!-- Sample-app code, not a library: CA1515 (make types internal) does
           not apply to an app, CA1873 (defer logger arguments) is noise at
           this log volume, and CA1056 (URI properties should be Uri) would
           turn tile templates like {z}/{x}/{y} into escaped Uris. -->
      <NoWarn>$(NoWarn);CA1515;CA1873;CA1056</NoWarn>
    </PropertyGroup>
    <ItemGroup>
      <!-- Deliberately not the plugin: the sandbox encodes with an
           independent library so its tests exercise Bowire's decoder. -->
      <PackageReference Include="OpenDisNet" />
    </ItemGroup>
    <ItemGroup>
      <InternalsVisibleTo Include="Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests" />
    </ItemGroup>
  </Project>
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Properties/launchSettings.json`:
  ```json
  {
    "profiles": {
      "http": {
        "commandName": "Project",
        "launchBrowser": true,
        "applicationUrl": "http://localhost:5185",
        "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
      }
    }
  }
  ```

- [ ] **Step 3: Optionen schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/SandboxOptions.cs`:
  ```csharp
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

      public MapView InitialView { get; set; } = new();

      /// <summary>Filled from configuration only; the binder appends to this list.</summary>
      public IList<BasemapOption> Basemaps { get; } = [];

      public IPEndPoint TargetEndPoint => IPEndPoint.Parse(Target);
  }

  internal sealed class MapView
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
  ```

- [ ] **Step 4: Testprojekt anlegen**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.csproj`:
  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
      <IsPackable>false</IsPackable>
      <IsTestProject>true</IsTestProject>
    </PropertyGroup>
    <ItemGroup>
      <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>
    <ItemGroup>
      <PackageReference Include="Microsoft.NET.Test.Sdk" />
      <PackageReference Include="xunit.v3" />
      <PackageReference Include="Kuestenlogik.Bowire" />
      <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" />
      <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" />
    </ItemGroup>
    <ItemGroup>
      <ProjectReference Include="..\..\samples\Kuestenlogik.Bowire.Protocol.Dis.Sandbox\Kuestenlogik.Bowire.Protocol.Dis.Sandbox.csproj" />
      <!-- Bowire's decoder: what the sandbox's PDUs are checked against. -->
      <ProjectReference Include="..\..\src\Kuestenlogik.Bowire.Protocol.Dis\Kuestenlogik.Bowire.Protocol.Dis.csproj" />
    </ItemGroup>
  </Project>
  ```

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/GlobalUsings.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  global using Xunit;
  ```

  In `Bowire.Protocol.Dis.slnx` in den Ordner `/tests/`:
  ```xml
      <Project Path="tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.csproj" />
  ```
  und in den Ordner `/samples/`:
  ```xml
      <Project Path="samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.csproj" />
  ```

- [ ] **Step 5: Failing Test schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/SandboxOptionsValidatorTests.cs`:
  ```csharp
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
  ```

- [ ] **Step 6: Test laufen lassen, er muss scheitern**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests`
  Expected: Build-Fehler `CS0246: … 'SandboxOptionsValidator' could not be found`.

- [ ] **Step 7: Validator und minimales `Program.cs` schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/SandboxOptionsValidator.cs`:
  ```csharp
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
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Program.cs` (wird in Task 6 erweitert):
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox;
  using Microsoft.Extensions.Options;

  var builder = WebApplication.CreateBuilder(args);

  builder.Services.AddOptions<SandboxOptions>()
      .Bind(builder.Configuration.GetSection(SandboxOptions.Section))
      .ValidateOnStart();
  builder.Services.AddSingleton<IValidateOptions<SandboxOptions>, SandboxOptionsValidator>();

  var app = builder.Build();
  app.Run();
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/appsettings.json` (Basemaps aus `Terracle.Map/Services/BasemapOptions.cs`):
  ```json
  {
    "Logging": {
      "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
    },
    "AllowedHosts": "*",
    "Sandbox": {
      "Target": "239.1.2.3:3000",
      "Ttl": 1,
      "ExerciseId": 1,
      "SiteId": 1,
      "ApplicationId": 3001,
      "HeartbeatSeconds": 5,
      "FollowMaxUpdatesPerSecond": 10,
      "CommentDatumId": 900000,
      "InitialView": { "Latitude": 53.55, "Longitude": 9.99, "Zoom": 11 },
      "Basemaps": [
        { "Id": "dark", "Name": "Dark", "Kind": "style", "Url": "https://tiles.openfreemap.org/styles/dark" },
        { "Id": "positron", "Name": "Positron", "Kind": "style", "Url": "https://tiles.openfreemap.org/styles/positron" },
        { "Id": "liberty", "Name": "Liberty", "Kind": "style", "Url": "https://tiles.openfreemap.org/styles/liberty" },
        {
          "Id": "osm", "Name": "OpenStreetMap", "Kind": "raster",
          "Tiles": [ "https://tile.openstreetmap.org/{z}/{x}/{y}.png" ],
          "Attribution": "© <a href='https://www.openstreetmap.org/copyright'>OpenStreetMap</a> contributors"
        },
        {
          "Id": "satellite", "Name": "Satellit", "Kind": "raster",
          "Tiles": [ "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}" ],
          "Attribution": "© Esri, Maxar, Earthstar Geographics"
        },
        {
          "Id": "topo", "Name": "Topografisch", "Kind": "raster",
          "Tiles": [ "https://server.arcgisonline.com/ArcGIS/rest/services/World_Topo_Map/MapServer/tile/{z}/{y}/{x}" ],
          "Attribution": "© Esri, HERE, Garmin, OpenStreetMap contributors"
        },
        { "Id": "blank", "Name": "Leer", "Kind": "blank", "Color": "#1e1e1e" }
      ]
    }
  }
  ```

- [ ] **Step 8: Tests laufen lassen, sie müssen bestehen**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests`
  Expected: alle Tests von `SandboxOptionsValidatorTests` grün, 0 Warnungen.

- [ ] **Step 9: Ganze Solution bauen**

  Run: `dotnet build Bowire.Protocol.Dis.slnx -c Release`
  Expected: `0 Warnung(en)`, `0 Fehler`.

- [ ] **Step 10: Commit**

  ```bash
  git add Directory.Packages.props Bowire.Protocol.Dis.slnx samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests
  git commit -m "feat(sandbox): Projektgeruest und Konfiguration der DIS-Sandbox (#67)"
  ```

---

### Task 2: `GeoConversion`

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Geo/GeoConversion.cs`
- Test: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/GeoConversionTests.cs`

**Interfaces:**
- Produces (namespace `Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo`):
  - `readonly record struct GeoPosition(double Latitude, double Longitude, double Altitude)`: Grad, Grad, Meter über dem Ellipsoid.
  - `readonly record struct Ecef(double X, double Y, double Z)`
  - `readonly record struct DisOrientation(double Psi, double Theta, double Phi)`: Radiant.
  - `static class GeoConversion` mit `Ecef ToEcef(GeoPosition)`, `GeoPosition FromEcef(Ecef)`, `DisOrientation ToDisOrientation(GeoPosition at, double headingDegrees)`, `double HeadingFromDisOrientation(GeoPosition at, DisOrientation)` (0–360), `Ecef VelocityToEcef(GeoPosition at, double speedMetresPerSecond, double headingDegrees)`, `double NormalizeDegrees(double)` (0–360).

- [ ] **Step 1: Failing Test schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/GeoConversionTests.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Records;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

  /// <summary>
  /// The sandbox's own geometry. Reference values are independent of both
  /// the sandbox and the plugin; the plugin's converter is used only as a
  /// second opinion on the way back.
  /// </summary>
  public sealed class GeoConversionTests
  {
      private const double Deg = Math.PI / 180.0;
      private static readonly GeoPosition s_hamburg = new(53.5511, 9.9937, 12.0);

      [Fact]
      public void Equator_PrimeMeridian_IsTheSemiMajorAxis()
      {
          var e = GeoConversion.ToEcef(new GeoPosition(0, 0, 0));
          Assert.Equal(6_378_137.0, e.X, 3);
          Assert.Equal(0.0, e.Y, 3);
          Assert.Equal(0.0, e.Z, 3);
      }

      [Fact]
      public void NorthPole_IsTheSemiMinorAxis()
      {
          var e = GeoConversion.ToEcef(new GeoPosition(90, 0, 0));
          Assert.Equal(0.0, e.X, 3);
          Assert.Equal(6_356_752.314245, e.Z, 3);
      }

      [Fact]
      public void Hamburg_RoundTrips()
      {
          var back = GeoConversion.FromEcef(GeoConversion.ToEcef(s_hamburg));
          Assert.Equal(s_hamburg.Latitude, back.Latitude, 9);
          Assert.Equal(s_hamburg.Longitude, back.Longitude, 9);
          Assert.Equal(s_hamburg.Altitude, back.Altitude, 3);
      }

      [Fact]
      public void Hamburg_AgreesWithThePluginsConverter()
      {
          var e = GeoConversion.ToEcef(s_hamburg);
          var plugin = GeocentricCoordinate.ToWgs84(new Vector3Double(e.X, e.Y, e.Z));
          Assert.NotNull(plugin);
          Assert.Equal(s_hamburg.Latitude, plugin!.Value.Latitude, 8);
          Assert.Equal(s_hamburg.Longitude, plugin.Value.Longitude, 8);
          Assert.Equal(s_hamburg.Altitude, plugin.Value.Altitude, 2);
      }

      [Fact]
      public void HeadingEast_InHamburg_GivesTheExpectedEulerAngles()
      {
          // Body x points east: ψ = 90° + longitude, θ = 0, φ = −(90° + latitude).
          var o = GeoConversion.ToDisOrientation(s_hamburg, 90);
          Assert.Equal(99.9937 * Deg, o.Psi, 9);
          Assert.Equal(0.0, o.Theta, 9);
          Assert.Equal(-143.5511 * Deg, o.Phi, 9);
      }

      [Theory]
      [InlineData(0)]
      [InlineData(45)]
      [InlineData(90)]
      [InlineData(180)]
      [InlineData(270)]
      [InlineData(359.5)]
      public void Heading_RoundTripsThroughDisOrientation(double heading)
      {
          var o = GeoConversion.ToDisOrientation(s_hamburg, heading);
          Assert.Equal(heading, GeoConversion.HeadingFromDisOrientation(s_hamburg, o), 6);
      }

      [Fact]
      public void Velocity_NorthAtTheOrigin_PointsAlongZ()
      {
          var v = GeoConversion.VelocityToEcef(new GeoPosition(0, 0, 0), 10, 0);
          Assert.Equal(0.0, v.X, 9);
          Assert.Equal(0.0, v.Y, 9);
          Assert.Equal(10.0, v.Z, 9);
      }

      [Fact]
      public void Velocity_EastAtTheOrigin_PointsAlongY()
      {
          var v = GeoConversion.VelocityToEcef(new GeoPosition(0, 0, 0), 10, 90);
          Assert.Equal(0.0, v.X, 9);
          Assert.Equal(10.0, v.Y, 9);
          Assert.Equal(0.0, v.Z, 9);
      }

      [Theory]
      [InlineData(-90, 270)]
      [InlineData(360, 0)]
      [InlineData(725, 5)]
      public void NormalizeDegrees_WrapsIntoZeroTo360(double input, double expected) =>
          Assert.Equal(expected, GeoConversion.NormalizeDegrees(input), 9);
  }
  ```

- [ ] **Step 2: Test laufen lassen, er muss scheitern**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.GeoConversionTests`
  Expected: Build-Fehler `CS0234/CS0246`, `Geo`/`GeoConversion` nicht gefunden.

- [ ] **Step 3: Implementierung schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Geo/GeoConversion.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

  /// <summary>A WGS84 position: degrees, degrees, metres above the ellipsoid.</summary>
  internal readonly record struct GeoPosition(double Latitude, double Longitude, double Altitude);

  /// <summary>Earth-centred, earth-fixed coordinates in metres: DIS world coordinates.</summary>
  internal readonly record struct Ecef(double X, double Y, double Z);

  /// <summary>DIS Euler angles ψ/θ/φ in radians, relative to world coordinates.</summary>
  internal readonly record struct DisOrientation(double Psi, double Theta, double Phi);

  /// <summary>
  /// The geometry the sandbox needs to speak DIS. Written here rather than
  /// taken from the plugin, so that the tests compare two implementations.
  /// </summary>
  internal static class GeoConversion
  {
      private const double SemiMajorAxis = 6_378_137.0;
      private const double Flattening = 1.0 / 298.257223563;
      private const double EccentricitySquared = Flattening * (2.0 - Flattening);
      private const double DegToRad = Math.PI / 180.0;

      public static Ecef ToEcef(GeoPosition p)
      {
          var (sinLat, cosLat) = Math.SinCos(p.Latitude * DegToRad);
          var (sinLon, cosLon) = Math.SinCos(p.Longitude * DegToRad);
          var n = SemiMajorAxis / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));
          return new Ecef(
              (n + p.Altitude) * cosLat * cosLon,
              (n + p.Altitude) * cosLat * sinLon,
              ((n * (1.0 - EccentricitySquared)) + p.Altitude) * sinLat);
      }

      public static GeoPosition FromEcef(Ecef e)
      {
          var lon = Math.Atan2(e.Y, e.X);
          var p = Math.Sqrt((e.X * e.X) + (e.Y * e.Y));
          if (p < 1e-6)
          {
              var semiMinorAxis = SemiMajorAxis * (1.0 - Flattening);
              return new GeoPosition(e.Z >= 0 ? 90.0 : -90.0, 0.0, Math.Abs(e.Z) - semiMinorAxis);
          }

          var lat = Math.Atan2(e.Z, p * (1.0 - EccentricitySquared));
          for (var i = 0; i < 6; i++)
          {
              var n = RadiusOfCurvature(lat);
              var h = (p / Math.Cos(lat)) - n;
              lat = Math.Atan2(e.Z, p * (1.0 - (EccentricitySquared * n / (n + h))));
          }
          var alt = (p / Math.Cos(lat)) - RadiusOfCurvature(lat);
          return new GeoPosition(lat / DegToRad, lon / DegToRad, alt);
      }

      public static DisOrientation ToDisOrientation(GeoPosition at, double headingDegrees)
      {
          var bodyToWorld = Multiply(NedToWorld(at), Rotation(headingDegrees * DegToRad, 0.0, 0.0));
          return new DisOrientation(
              Math.Atan2(bodyToWorld[1][0], bodyToWorld[0][0]),
              Math.Asin(Math.Clamp(-bodyToWorld[2][0], -1.0, 1.0)),
              Math.Atan2(bodyToWorld[2][1], bodyToWorld[2][2]));
      }

      public static double HeadingFromDisOrientation(GeoPosition at, DisOrientation orientation)
      {
          var bodyToWorld = Rotation(orientation.Psi, orientation.Theta, orientation.Phi);
          var bodyToNed = Multiply(Transpose(NedToWorld(at)), bodyToWorld);
          return NormalizeDegrees(Math.Atan2(bodyToNed[1][0], bodyToNed[0][0]) / DegToRad);
      }

      public static Ecef VelocityToEcef(GeoPosition at, double speedMetresPerSecond, double headingDegrees)
      {
          var (sinHeading, cosHeading) = Math.SinCos(headingDegrees * DegToRad);
          var ned = NedToWorld(at);
          var north = speedMetresPerSecond * cosHeading;
          var east = speedMetresPerSecond * sinHeading;
          return new Ecef(
              (ned[0][0] * north) + (ned[0][1] * east),
              (ned[1][0] * north) + (ned[1][1] * east),
              (ned[2][0] * north) + (ned[2][1] * east));
      }

      public static double NormalizeDegrees(double degrees)
      {
          var d = degrees % 360.0;
          return d < 0 ? d + 360.0 : d;
      }

      private static double RadiusOfCurvature(double latitudeRadians)
      {
          var sinLat = Math.Sin(latitudeRadians);
          return SemiMajorAxis / Math.Sqrt(1.0 - (EccentricitySquared * sinLat * sinLat));
      }

      // Columns: north, east and down at `at`, expressed in world coordinates.
      private static double[][] NedToWorld(GeoPosition at)
      {
          var (sinLat, cosLat) = Math.SinCos(at.Latitude * DegToRad);
          var (sinLon, cosLon) = Math.SinCos(at.Longitude * DegToRad);
          return
          [
              [-sinLat * cosLon, -sinLon, -cosLat * cosLon],
              [-sinLat * sinLon, cosLon, -cosLat * sinLon],
              [cosLat, 0.0, -sinLat],
          ];
      }

      // Z-Y-X rotation (yaw, pitch, roll) from body to the reference frame.
      private static double[][] Rotation(double yaw, double pitch, double roll)
      {
          var (sy, cy) = Math.SinCos(yaw);
          var (sp, cp) = Math.SinCos(pitch);
          var (sr, cr) = Math.SinCos(roll);
          return
          [
              [cy * cp, (cy * sp * sr) - (sy * cr), (cy * sp * cr) + (sy * sr)],
              [sy * cp, (sy * sp * sr) + (cy * cr), (sy * sp * cr) - (cy * sr)],
              [-sp, cp * sr, cp * cr],
          ];
      }

      private static double[][] Multiply(double[][] a, double[][] b)
      {
          var result = new double[3][];
          for (var i = 0; i < 3; i++)
          {
              result[i] = new double[3];
              for (var j = 0; j < 3; j++)
                  result[i][j] = (a[i][0] * b[0][j]) + (a[i][1] * b[1][j]) + (a[i][2] * b[2][j]);
          }
          return result;
      }

      private static double[][] Transpose(double[][] m) =>
      [
          [m[0][0], m[1][0], m[2][0]],
          [m[0][1], m[1][1], m[2][1]],
          [m[0][2], m[1][2], m[2][2]],
      ];
  }
  ```

- [ ] **Step 4: Tests laufen lassen, sie müssen bestehen**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.GeoConversionTests`
  Expected: alle grün. Scheitert `HeadingEast_InHamburg_…` nur im Vorzeichen von φ, ist die Spaltenreihenfolge in `NedToWorld` vertauscht. Die Matrix muss Nord, Ost, Unten als Spalten haben.

- [ ] **Step 5: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Geo tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/GeoConversionTests.cs
  git commit -m "feat(sandbox): eigene Geometrie fuer Position, Lage und Geschwindigkeit (#67)"
  ```

---

### Task 3: Objekte: Voreinstellungen, Paar-Regeln, `EntityStore`

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/EntityPreset.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/SandboxEntity.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/PairRules.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/EntityStore.cs`
- Test: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/EntityStoreTests.cs`

**Interfaces:**
- Consumes: `GeoPosition`, `GeoConversion.NormalizeDegrees` (Task 2).
- Produces (namespace `Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities`):
  - `sealed record EntityPreset(string Id, string Label, byte Kind, byte Domain, ushort Country, byte Category, byte Subcategory, double DefaultAltitude)` mit `static IReadOnlyList<EntityPreset> All` und `static EntityPreset Get(string id)` (wirft `ArgumentException`).
  - `enum Force : byte { Friendly = 1, Opposing = 2, Neutral = 3 }`
  - `sealed record SandboxEntity(ushort Number, string Name, string PresetId, Force Force, GeoPosition Position, double HeadingDegrees, double SpeedMetresPerSecond, bool Following, IReadOnlyList<KeyValuePair<string, string>> Pairs)`
  - `enum EntityChangeKind { Added, Moved, Updated, PairsChanged, Removed }`, `sealed record EntityChange(EntityChangeKind Kind, SandboxEntity Entity)`
  - `static class PairRules`: `const int MaxCommentPduBytes = 1400`, `int CommentPduLength(IReadOnlyList<KeyValuePair<string,string>>)`, `string Text(KeyValuePair<string,string>)` (→ `"key=value"`), `string? Validate(IReadOnlyList<KeyValuePair<string,string>>)`.
  - `sealed class EntityStore(TimeProvider time)`: `event Action<EntityChange>? Changed`, `IReadOnlyList<SandboxEntity> Snapshot()` (nach Nummer), `SandboxEntity? Get(ushort)`, `SandboxEntity Add(string presetId, Force force, double latitude, double longitude)`, `bool Move(ushort number, double latitude, double longitude, bool following)`, `bool StopFollowing(ushort number)`, `bool Update(ushort number, string name, string presetId, Force force, double altitude)`, `bool TrySetPairs(ushort number, IReadOnlyList<KeyValuePair<string,string>> pairs, out string? error)`, `bool Remove(ushort number)`.

- [ ] **Step 1: Failing Test schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/EntityStoreTests.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
  using Microsoft.Extensions.Time.Testing;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

  public sealed class EntityStoreTests
  {
      // 0.001° of latitude at the equator, on the mean earth radius.
      private const double MillidegreeMetres = 111.19508;

      private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
      private readonly List<EntityChange> _changes = [];
      private readonly EntityStore _store;

      public EntityStoreTests()
      {
          _store = new EntityStore(_time);
          _store.Changed += _changes.Add;
      }

      private static KeyValuePair<string, string> P(string key, string value) => new(key, value);

      [Fact]
      public void Add_NumbersFromOne_NamesAndPlacesFromThePreset()
      {
          var a = _store.Add("tank", Force.Friendly, 53.5, 10.0);
          var b = _store.Add("helicopter", Force.Opposing, 53.6, 10.1);

          Assert.Equal((ushort)1, a.Number);
          Assert.Equal((ushort)2, b.Number);
          Assert.Equal("Objekt 2", b.Name);
          Assert.Equal(0.0, a.Position.Altitude);
          Assert.Equal(500.0, b.Position.Altitude);
          Assert.Equal([EntityChangeKind.Added, EntityChangeKind.Added], _changes.Select(c => c.Kind));
      }

      [Fact]
      public void Add_UnknownPreset_Throws() =>
          Assert.Throws<ArgumentException>(() => _store.Add("submarine", Force.Friendly, 0, 0));

      [Fact]
      public void Remove_NeverReusesTheNumber()
      {
          _store.Add("tank", Force.Friendly, 0, 0);
          var second = _store.Add("tank", Force.Friendly, 0, 0);
          Assert.True(_store.Remove(second.Number));

          var third = _store.Add("tank", Force.Friendly, 0, 0);

          Assert.Equal((ushort)3, third.Number);
          Assert.Equal(EntityChangeKind.Removed, _changes[2].Kind);
          Assert.Equal(second.Number, _changes[2].Entity.Number);
      }

      [Fact]
      public void Move_WithoutFollowing_StandsStill()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);

          Assert.True(_store.Move(e.Number, 0.01, 0.02, following: false));

          var moved = _store.Get(e.Number)!;
          Assert.Equal(0.01, moved.Position.Latitude);
          Assert.Equal(0.02, moved.Position.Longitude);
          Assert.Equal(0.0, moved.SpeedMetresPerSecond);
          Assert.False(moved.Following);
          Assert.Equal(EntityChangeKind.Moved, _changes[^1].Kind);
      }

      [Fact]
      public void Move_Following_DerivesHeadingAndSpeedFromTheLastTwoPositions()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Move(e.Number, 0, 0, following: true);
          _time.Advance(TimeSpan.FromSeconds(1));

          _store.Move(e.Number, 0.001, 0, following: true);

          var moved = _store.Get(e.Number)!;
          Assert.True(moved.Following);
          Assert.Equal(0.0, moved.HeadingDegrees, 6);
          Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
      }

      [Fact]
      public void Move_AcrossTheAntimeridian_IsAShortHopEast()
      {
          var e = _store.Add("ship", Force.Neutral, 0, 179.9995);
          _store.Move(e.Number, 0, 179.9995, following: true);
          _time.Advance(TimeSpan.FromSeconds(1));

          _store.Move(e.Number, 0, -179.9995, following: true);

          var moved = _store.Get(e.Number)!;
          Assert.Equal(90.0, moved.HeadingDegrees, 6);
          Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
      }

      [Fact]
      public void Move_WithNoElapsedTime_KeepsSpeedAndHeading()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Move(e.Number, 0, 0, following: true);
          _time.Advance(TimeSpan.FromSeconds(1));
          _store.Move(e.Number, 0.001, 0, following: true);

          _store.Move(e.Number, 0.002, 0.001, following: true);   // same instant

          var moved = _store.Get(e.Number)!;
          Assert.True(double.IsFinite(moved.SpeedMetresPerSecond));
          Assert.Equal(MillidegreeMetres, moved.SpeedMetresPerSecond, 2);
          Assert.Equal(0.0, moved.HeadingDegrees, 6);
      }

      [Fact]
      public void StopFollowing_ComesToRest()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Move(e.Number, 0, 0, following: true);
          _time.Advance(TimeSpan.FromSeconds(1));
          _store.Move(e.Number, 0.001, 0, following: true);

          Assert.True(_store.StopFollowing(e.Number));

          var stopped = _store.Get(e.Number)!;
          Assert.False(stopped.Following);
          Assert.Equal(0.0, stopped.SpeedMetresPerSecond);
          Assert.Equal(EntityChangeKind.Moved, _changes[^1].Kind);
      }

      [Fact]
      public void Update_ChangesNameTypeForceAndAltitude()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);

          Assert.True(_store.Update(e.Number, "  Leo  ", "truck", Force.Neutral, 25));

          var updated = _store.Get(e.Number)!;
          Assert.Equal("Leo", updated.Name);
          Assert.Equal("truck", updated.PresetId);
          Assert.Equal(Force.Neutral, updated.Force);
          Assert.Equal(25.0, updated.Position.Altitude);
          Assert.Equal(EntityChangeKind.Updated, _changes[^1].Kind);
      }

      [Fact]
      public void TrySetPairs_StoresACopyAndRaisesPairsChanged()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          var pairs = new List<KeyValuePair<string, string>> { P("status", "ready"), P("fuel", "80") };

          Assert.True(_store.TrySetPairs(e.Number, pairs, out var error));
          pairs.Clear();

          Assert.Null(error);
          Assert.Equal([P("status", "ready"), P("fuel", "80")], _store.Get(e.Number)!.Pairs);
          Assert.Equal(EntityChangeKind.PairsChanged, _changes[^1].Kind);
      }

      [Theory]
      [InlineData("", "x")]
      [InlineData("  ", "x")]
      [InlineData("a=b", "x")]
      public void TrySetPairs_RejectsKeysThatCannotBeSent(string key, string value)
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _changes.Clear();

          Assert.False(_store.TrySetPairs(e.Number, [P(key, value)], out var error));

          Assert.NotNull(error);
          Assert.Empty(_changes);
      }

      [Fact]
      public void TrySetPairs_RejectsDuplicateKeys()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);

          Assert.False(_store.TrySetPairs(e.Number, [P("status", "a"), P("status", "b")], out var error));
          Assert.Contains("status", error, StringComparison.Ordinal);
      }

      [Fact]
      public void TrySetPairs_RejectsWhatWouldNotFitInOneDatagram()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);

          Assert.False(_store.TrySetPairs(e.Number, [P("text", new string('x', 1400))], out var error));
          Assert.Contains("1400", error, StringComparison.Ordinal);
      }

      [Fact]
      public void UnknownNumber_ChangesNothingAndRaisesNothing()
      {
          Assert.False(_store.Move(42, 0, 0, following: false));
          Assert.False(_store.StopFollowing(42));
          Assert.False(_store.Update(42, "x", "tank", Force.Friendly, 0));
          Assert.False(_store.TrySetPairs(42, [], out _));
          Assert.False(_store.Remove(42));
          Assert.Empty(_changes);
      }

      [Fact]
      public void CommentPduLength_MatchesTheWireLayout()
      {
          // 32 bytes of header and counts; each record 8 bytes plus its value padded to 8.
          Assert.Equal(32, PairRules.CommentPduLength([]));
          Assert.Equal(56, PairRules.CommentPduLength([P("status", "ready")]));          // 12 → 16
          Assert.Equal(48, PairRules.CommentPduLength([P("k", "123456")]));             // 8 → 8
          Assert.Equal(56, PairRules.CommentPduLength([P("ä", "1234567")]));            // 10 UTF-8 bytes → 16
      }
  }
  ```

- [ ] **Step 2: Test laufen lassen, er muss scheitern**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.EntityStoreTests`
  Expected: Build-Fehler, `Entities`/`EntityStore` nicht gefunden.

- [ ] **Step 3: Implementierung schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/EntityPreset.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

  /// <summary>
  /// A type you can pick instead of typing SISO codes. Codes per
  /// SISO-REF-010 v37; country 78 is Germany.
  /// </summary>
  internal sealed record EntityPreset(
      string Id, string Label, byte Kind, byte Domain, ushort Country,
      byte Category, byte Subcategory, double DefaultAltitude)
  {
      public static IReadOnlyList<EntityPreset> All { get; } =
      [
          new("tank", "Kampfpanzer", 1, 1, 78, 1, 0, 0),          // Platform, Land, Tank
          new("truck", "LKW", 1, 1, 78, 7, 0, 0),                 // Platform, Land, Large Wheeled Utility Vehicle
          new("helicopter", "Hubschrauber", 1, 2, 78, 21, 0, 500), // Platform, Air, Utility Helicopter
          new("aircraft", "Flugzeug", 1, 2, 78, 4, 0, 500),        // Platform, Air, Cargo/Tanker
          new("ship", "Schiff", 1, 3, 78, 50, 0, 0),               // Platform, Surface, Frigate (including Corvette)
          new("person", "Person", 3, 1, 78, 11, 0, 0),             // Life form, Land, Army
      ];

      public static EntityPreset Get(string id) =>
          All.FirstOrDefault(p => p.Id == id)
          ?? throw new ArgumentException($"Unknown entity preset '{id}'.", nameof(id));
  }
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/SandboxEntity.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

  /// <summary>DIS force ids the sandbox offers; values are the wire values.</summary>
  internal enum Force : byte
  {
      Friendly = 1,
      Opposing = 2,
      Neutral = 3,
  }

  /// <summary>One object on the map, as it stands. Immutable; the store swaps it.</summary>
  internal sealed record SandboxEntity(
      ushort Number,
      string Name,
      string PresetId,
      Force Force,
      GeoPosition Position,
      double HeadingDegrees,
      double SpeedMetresPerSecond,
      bool Following,
      IReadOnlyList<KeyValuePair<string, string>> Pairs);

  internal enum EntityChangeKind
  {
      Added,
      Moved,
      Updated,
      PairsChanged,
      Removed,
  }

  /// <summary>What changed, with the entity after the change (for Removed: as it was last).</summary>
  internal sealed record EntityChange(EntityChangeKind Kind, SandboxEntity Entity);
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/PairRules.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using System.Text;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

  /// <summary>
  /// Which key-value pairs can be sent, and how large the Comment PDU that
  /// carries them gets. The length is checked against what the encoder
  /// actually writes in the publisher's tests.
  /// </summary>
  internal static class PairRules
  {
      /// <summary>Keeps every Comment PDU in one UDP datagram without fragmentation.</summary>
      public const int MaxCommentPduBytes = 1400;

      // PDU header 12, originating and receiving id 6 + 6, two record counts 4 + 4.
      private const int CommentFixedBytes = 32;

      // Datum id 4, datum length 4.
      private const int DatumHeaderBytes = 8;

      public static string Text(KeyValuePair<string, string> pair) => $"{pair.Key}={pair.Value}";

      public static int CommentPduLength(IReadOnlyList<KeyValuePair<string, string>> pairs)
      {
          var total = CommentFixedBytes;
          foreach (var pair in pairs)
          {
              var valueBytes = Encoding.UTF8.GetByteCount(Text(pair));
              total += DatumHeaderBytes + ((valueBytes + 7) / 8 * 8);
          }
          return total;
      }

      /// <summary>Null when the pairs can be sent; otherwise why not, in German for the editor.</summary>
      public static string? Validate(IReadOnlyList<KeyValuePair<string, string>> pairs)
      {
          var seen = new HashSet<string>(StringComparer.Ordinal);
          foreach (var (key, _) in pairs)
          {
              if (string.IsNullOrWhiteSpace(key))
                  return "Ein Schlüssel ist leer.";
              if (key.Contains('=', StringComparison.Ordinal))
                  return $"Der Schlüssel '{key}' enthält '='. Gesendet wird key=value, ein Empfänger könnte Schlüssel und Wert nicht mehr trennen.";
              if (!seen.Add(key))
                  return $"Der Schlüssel '{key}' kommt doppelt vor.";
          }

          var length = CommentPduLength(pairs);
          return length > MaxCommentPduBytes
              ? $"Die Comment-PDU wäre {length} Byte groß, erlaubt sind {MaxCommentPduBytes}."
              : null;
      }
  }
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities/EntityStore.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;

  /// <summary>
  /// The objects on the map. Knows neither the map nor DIS: it changes
  /// entities and says so through <see cref="Changed"/>, raised outside the
  /// lock so a handler may read the store again.
  /// </summary>
  internal sealed class EntityStore(TimeProvider time)
  {
      private const double MeanEarthRadius = 6_371_008.8;
      private const double DegToRad = Math.PI / 180.0;

      private readonly Lock _gate = new();
      private readonly Dictionary<ushort, SandboxEntity> _entities = [];
      private readonly Dictionary<ushort, long> _lastFollowMove = [];

      // Only ever counts up, so a number is never handed out twice in a
      // session: Bowire would take a new object for the old one.
      private int _nextNumber = 1;

      public event Action<EntityChange>? Changed;

      public IReadOnlyList<SandboxEntity> Snapshot()
      {
          lock (_gate)
              return [.. _entities.Values.OrderBy(e => e.Number)];
      }

      public SandboxEntity? Get(ushort number)
      {
          lock (_gate)
              return _entities.GetValueOrDefault(number);
      }

      public SandboxEntity Add(string presetId, Force force, double latitude, double longitude)
      {
          var preset = EntityPreset.Get(presetId);
          SandboxEntity entity;
          lock (_gate)
          {
              if (_nextNumber > ushort.MaxValue)
                  throw new InvalidOperationException("Alle 65535 Entity-Nummern dieser Sitzung sind vergeben.");
              var number = (ushort)_nextNumber++;
              entity = new SandboxEntity(
                  number, $"Objekt {number}", preset.Id, force,
                  new GeoPosition(latitude, longitude, preset.DefaultAltitude),
                  HeadingDegrees: 0, SpeedMetresPerSecond: 0, Following: false, Pairs: []);
              _entities[number] = entity;
          }
          Raise(EntityChangeKind.Added, entity);
          return entity;
      }

      /// <summary>
      /// Moves an entity. While following the mouse, heading and speed come
      /// from this and the previous follow move; otherwise it stands still.
      /// </summary>
      public bool Move(ushort number, double latitude, double longitude, bool following)
      {
          var now = time.GetTimestamp();
          var moved = Mutate(number, e =>
          {
              var heading = e.HeadingDegrees;
              var speed = 0.0;
              if (following)
              {
                  speed = e.SpeedMetresPerSecond;
                  if (_lastFollowMove.TryGetValue(number, out var last))
                  {
                      var elapsed = time.GetElapsedTime(last, now).TotalSeconds;
                      var (distance, bearing) = Displacement(e.Position, latitude, longitude);
                      // Two moves at the same instant say nothing about speed.
                      if (elapsed > 0 && distance > 0.01)
                      {
                          speed = distance / elapsed;
                          heading = bearing;
                      }
                  }
                  _lastFollowMove[number] = now;
              }
              else
              {
                  _lastFollowMove.Remove(number);
              }

              return e with
              {
                  Position = e.Position with { Latitude = latitude, Longitude = longitude },
                  HeadingDegrees = heading,
                  SpeedMetresPerSecond = speed,
                  Following = following,
              };
          });
          return Raise(EntityChangeKind.Moved, moved);
      }

      public bool StopFollowing(ushort number)
      {
          var stopped = Mutate(number, e =>
          {
              _lastFollowMove.Remove(number);
              return e with { Following = false, SpeedMetresPerSecond = 0 };
          });
          return Raise(EntityChangeKind.Moved, stopped);
      }

      public bool Update(ushort number, string name, string presetId, Force force, double altitude)
      {
          ArgumentException.ThrowIfNullOrWhiteSpace(name);
          var preset = EntityPreset.Get(presetId);
          var updated = Mutate(number, e => e with
          {
              Name = name.Trim(),
              PresetId = preset.Id,
              Force = force,
              Position = e.Position with { Altitude = altitude },
          });
          return Raise(EntityChangeKind.Updated, updated);
      }

      public bool TrySetPairs(ushort number, IReadOnlyList<KeyValuePair<string, string>> pairs, out string? error)
      {
          error = PairRules.Validate(pairs);
          if (error is not null)
              return false;

          KeyValuePair<string, string>[] copy = [.. pairs];
          var updated = Mutate(number, e => e with { Pairs = copy });
          if (updated is null)
          {
              error = $"Objekt {number} gibt es nicht.";
              return false;
          }
          return Raise(EntityChangeKind.PairsChanged, updated);
      }

      public bool Remove(ushort number)
      {
          SandboxEntity? removed;
          lock (_gate)
          {
              if (_entities.Remove(number, out removed))
                  _lastFollowMove.Remove(number);
          }
          return Raise(EntityChangeKind.Removed, removed);
      }

      private SandboxEntity? Mutate(ushort number, Func<SandboxEntity, SandboxEntity> change)
      {
          lock (_gate)
          {
              if (!_entities.TryGetValue(number, out var current))
                  return null;
              var next = change(current);
              _entities[number] = next;
              return next;
          }
      }

      private bool Raise(EntityChangeKind kind, SandboxEntity? entity)
      {
          if (entity is null)
              return false;
          Changed?.Invoke(new EntityChange(kind, entity));
          return true;
      }

      // Short hops only (mouse moves); equirectangular is exact enough and
      // the longitude difference is wrapped so the antimeridian is a hop.
      private static (double Distance, double Bearing) Displacement(GeoPosition from, double latitude, double longitude)
      {
          var dLon = longitude - from.Longitude;
          if (dLon > 180) dLon -= 360;
          else if (dLon < -180) dLon += 360;

          var meanLat = (from.Latitude + latitude) / 2.0 * DegToRad;
          var east = dLon * DegToRad * Math.Cos(meanLat) * MeanEarthRadius;
          var north = (latitude - from.Latitude) * DegToRad * MeanEarthRadius;
          var bearing = GeoConversion.NormalizeDegrees(Math.Atan2(east, north) / DegToRad);
          return (Math.Sqrt((east * east) + (north * north)), bearing);
      }
  }
  ```

- [ ] **Step 4: Tests laufen lassen, sie müssen bestehen**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.EntityStoreTests`
  Expected: alle grün.

- [ ] **Step 5: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Entities tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/EntityStoreTests.cs
  git commit -m "feat(sandbox): Objekte mit Nummern, Typen, Bewegung und Key-Value-Paaren (#67)"
  ```

---

### Task 4: `PduFactory`, geprüft gegen Bowires Decoder

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/PduFactory.cs`
- Test: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/PduFactoryDecoderTests.cs`

**Interfaces:**
- Consumes: `SandboxOptions` (Task 1), `GeoConversion` (Task 2), `SandboxEntity`, `EntityPreset`, `PairRules`, `Force` (Task 3).
- Produces (namespace `Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing`): `sealed class PduFactory(IOptions<SandboxOptions> options, TimeProvider time)` mit `const int MarkingLength = 11`, `const uint DeactivatedAppearance = 1u << 23`, `byte[] EntityState(SandboxEntity entity, bool deactivated = false)`, `byte[] Comment(SandboxEntity entity)`, `uint Timestamp()`, `static byte[] Marking(string name)`, `static string MarkingText(string name)`.

- [ ] **Step 1: Failing Test schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/PduFactoryDecoderTests.cs`:
  ```csharp
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
  ```

- [ ] **Step 2: Test laufen lassen, er muss scheitern**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.PduFactoryDecoderTests`
  Expected: Build-Fehler, `Publishing`/`PduFactory` nicht gefunden.

- [ ] **Step 3: Implementierung schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/PduFactory.cs`:
  ```csharp
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
  ```

- [ ] **Step 4: Tests laufen lassen, sie müssen bestehen**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.PduFactoryDecoderTests`
  Expected: alle grün. **Wichtig:** Scheitert ein Feldvergleich, nicht den Test an die Ausgabe anpassen. Erst klären, welche Seite vom Standard abweicht (IEEE 1278.1-2012 bzw. SISO-REF-010). Liegt der Fehler im Plugin, wird er als eigenes Ticket im Repo gemeldet und der Test bleibt rot markiert (`Skip = "…#NN"`), bis das Ticket erledigt ist.

- [ ] **Step 5: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/PduFactory.cs tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/PduFactoryDecoderTests.cs
  git commit -m "feat(sandbox): EntityState- und Comment-PDUs mit OpenDisNet, gegen Bowires Decoder geprueft (#67)"
  ```

---

### Task 5: Senden: `DisPublisher`, Senke, Status

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/IDatagramSink.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/UdpDatagramSink.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/PublisherStatus.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/DisPublisher.cs`
- Create: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/RecordingSink.cs`
- Test: `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/DisPublisherTests.cs`

**Interfaces:**
- Consumes: `EntityStore`, `EntityChange`, `EntityChangeKind` (Task 3), `PduFactory` (Task 4), `SandboxOptions` (Task 1).
- Produces (namespace `…Sandbox.Publishing`):
  - `interface IDatagramSink { void Send(byte[] datagram); }`
  - `sealed class UdpDatagramSink(IOptions<SandboxOptions>) : IDatagramSink, IDisposable`
  - `sealed class PublisherStatus`: `string Target`, `long Sent`, `long Errors`, `string? LastError`, `DateTimeOffset? LastErrorAt`, `IReadOnlyDictionary<ushort, string> EncodeFailures`, `event Action? Changed`, `RecordSent()`, `RecordSendError(Exception, DateTimeOffset)`, `MarkEncodeFailed(ushort, string)`, `ClearEncodeFailure(ushort)`. Konstruktor `PublisherStatus(IOptions<SandboxOptions>)`.
  - `sealed class DisPublisher : BackgroundService` mit Konstruktor `(EntityStore, PduFactory, IDatagramSink, PublisherStatus, IOptions<SandboxOptions>, TimeProvider, ILogger<DisPublisher>)`, `internal void Tick()`. Der Konstruktor abonniert `EntityStore.Changed`, `Dispose` meldet ab.

- [ ] **Step 1: Test-Senke schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/RecordingSink.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

  /// <summary>Keeps every datagram instead of sending it; can be told to fail.</summary>
  internal sealed class RecordingSink : IDatagramSink
  {
      public List<byte[]> Datagrams { get; } = [];

      public bool Fail { get; set; }

      public void Send(byte[] datagram)
      {
          if (Fail)
              throw new InvalidOperationException("network unreachable");
          Datagrams.Add(datagram);
      }

      /// <summary>PDU type byte of each datagram: 1 Entity State, 22 Comment.</summary>
      public IEnumerable<byte> Types => Datagrams.Select(d => d[2]);
  }
  ```

- [ ] **Step 2: Failing Test schreiben**

  `tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests/DisPublisherTests.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Records;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;
  using Microsoft.Extensions.Logging.Abstractions;
  using Microsoft.Extensions.Options;
  using Microsoft.Extensions.Time.Testing;
  using PluginComment = Kuestenlogik.Bowire.Protocol.Dis.Pdu.SimulationManagement.CommentPdu;
  using PluginDeadReckoning = Kuestenlogik.Bowire.Protocol.Dis.Enumerations.DeadReckoningAlgorithm;
  using PluginEntityState = Kuestenlogik.Bowire.Protocol.Dis.Pdu.EntityStatePdu;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests;

  public sealed class DisPublisherTests : IDisposable
  {
      private const byte EntityStateType = 1;
      private const byte CommentType = 22;

      private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
      private readonly RecordingSink _sink = new();
      private readonly EntityStore _store;
      private readonly PublisherStatus _status;
      private readonly DisPublisher _publisher;

      public DisPublisherTests()
      {
          var options = Options.Create(new SandboxOptions());
          _store = new EntityStore(_time);
          _status = new PublisherStatus(options);
          _publisher = new DisPublisher(
              _store, new PduFactory(options, _time), _sink, _status, options, _time, NullLogger<DisPublisher>.Instance);
      }

      public void Dispose() => _publisher.Dispose();

      private static double Latitude(byte[] datagram) =>
          GeocentricCoordinate.ToWgs84(PluginEntityState.Unmarshal(datagram).Location)!.Value.Latitude;

      [Fact]
      public void Add_Update_AndStandingMoves_SendAnEntityStateAtOnce()
      {
          var e = _store.Add("tank", Force.Friendly, 53.5, 10.0);
          _store.Update(e.Number, "Leo", "tank", Force.Friendly, 0);
          _store.Move(e.Number, 53.6, 10.0, following: false);

          Assert.Equal([EntityStateType, EntityStateType, EntityStateType], _sink.Types);
          Assert.Equal(53.6, Latitude(_sink.Datagrams[^1]), 7);
          Assert.Equal(3, _status.Sent);
      }

      [Fact]
      public void PairsChanged_SendsAComment()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _sink.Datagrams.Clear();

          _store.TrySetPairs(e.Number, [new("status", "ready")], out _);

          Assert.Equal([CommentType], _sink.Types);
          Assert.Single(PluginComment.Unmarshal(_sink.Datagrams[0]).VariableDatums!);
      }

      [Fact]
      public void Remove_SendsADeactivatedEntityState()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _sink.Datagrams.Clear();

          _store.Remove(e.Number);

          var pdu = PluginEntityState.Unmarshal(Assert.Single(_sink.Datagrams));
          Assert.Equal(PduFactory.DeactivatedAppearance, pdu.Appearance);
      }

      [Fact]
      public void FollowMoves_WaitForTheTick_AndOnlyTheLatestGoesOut()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _sink.Datagrams.Clear();

          for (var i = 1; i <= 5; i++)
          {
              _time.Advance(TimeSpan.FromMilliseconds(10));
              _store.Move(e.Number, i * 0.0001, 0, following: true);
          }
          Assert.Empty(_sink.Datagrams);

          _publisher.Tick();

          var datagram = Assert.Single(_sink.Datagrams);
          Assert.Equal(0.0005, Latitude(datagram), 7);
          Assert.Equal(PluginDeadReckoning.FPW, PluginEntityState.Unmarshal(datagram).DeadReckoning.Algorithm);

          _publisher.Tick();
          Assert.Single(_sink.Datagrams);
      }

      [Fact]
      public void StopFollowing_SendsTheRestingStateAtOnce()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Move(e.Number, 0, 0, following: true);
          _time.Advance(TimeSpan.FromSeconds(1));
          _store.Move(e.Number, 0.001, 0, following: true);
          _sink.Datagrams.Clear();

          _store.StopFollowing(e.Number);

          var pdu = PluginEntityState.Unmarshal(Assert.Single(_sink.Datagrams));
          Assert.Equal(PluginDeadReckoning.Static, pdu.DeadReckoning.Algorithm);
          Assert.Equal(default, pdu.LinearVelocity);
      }

      [Fact]
      public void Heartbeat_EveryFiveSeconds_SendsEveryEntity_AndCommentsOnlyWhereThereArePairs()
      {
          var withPairs = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Add("truck", Force.Friendly, 0, 0);
          _store.TrySetPairs(withPairs.Number, [new("status", "ready")], out _);
          _sink.Datagrams.Clear();

          _time.Advance(TimeSpan.FromSeconds(4.9));
          _publisher.Tick();
          Assert.Empty(_sink.Datagrams);

          _time.Advance(TimeSpan.FromSeconds(0.1));
          _publisher.Tick();
          Assert.Equal([EntityStateType, CommentType, EntityStateType], _sink.Types);

          _sink.Datagrams.Clear();
          _publisher.Tick();
          Assert.Empty(_sink.Datagrams);
      }

      [Fact]
      public void ClearingThePairs_SendsOneEmptyComment_AndTheHeartbeatThenSkipsIt()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.TrySetPairs(e.Number, [new("status", "ready")], out _);
          _sink.Datagrams.Clear();

          _store.TrySetPairs(e.Number, [], out _);

          var empty = PluginComment.Unmarshal(Assert.Single(_sink.Datagrams));
          Assert.Empty(empty.VariableDatums ?? []);

          _sink.Datagrams.Clear();
          _time.Advance(TimeSpan.FromSeconds(5));
          _publisher.Tick();
          Assert.Equal([EntityStateType], _sink.Types);
      }

      [Fact]
      public void SinkFailures_AreCountedAndNeverEscape()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _sink.Datagrams.Clear();
          _sink.Fail = true;

          _store.Move(e.Number, 1, 1, following: false);
          _time.Advance(TimeSpan.FromSeconds(5));
          _publisher.Tick();
          _time.Advance(TimeSpan.FromSeconds(5));
          _publisher.Tick();

          Assert.Equal(3, _status.Errors);
          Assert.Equal("network unreachable", _status.LastError);
          Assert.Equal(_time.GetUtcNow(), _status.LastErrorAt);

          _sink.Fail = false;
          _time.Advance(TimeSpan.FromSeconds(5));
          _publisher.Tick();
          Assert.Single(_sink.Datagrams);
      }

      [Fact]
      public void RemovingAFollowedEntity_DropsItsPendingMove()
      {
          var e = _store.Add("tank", Force.Friendly, 0, 0);
          _store.Move(e.Number, 0.001, 0, following: true);
          _store.Remove(e.Number);
          _sink.Datagrams.Clear();

          _publisher.Tick();
          _time.Advance(TimeSpan.FromSeconds(5));
          _publisher.Tick();

          Assert.Empty(_sink.Datagrams);
      }

      [Fact]
      public void Dispose_StopsListeningToTheStore()
      {
          _publisher.Dispose();

          _store.Add("tank", Force.Friendly, 0, 0);

          Assert.Empty(_sink.Datagrams);
      }
  }
  ```

- [ ] **Step 3: Test laufen lassen, er muss scheitern**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests --filter-class Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests.DisPublisherTests`
  Expected: Build-Fehler, `IDatagramSink`/`DisPublisher`/`PublisherStatus` nicht gefunden.

- [ ] **Step 4: Implementierung schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/IDatagramSink.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

  /// <summary>Where finished PDUs go. UDP in the app, a list in the tests.</summary>
  internal interface IDatagramSink
  {
      void Send(byte[] datagram);
  }
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/UdpDatagramSink.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using System.Net;
  using System.Net.Sockets;
  using Microsoft.Extensions.Options;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

  /// <summary>
  /// Sends to the configured target. Socket setup as in the replay sample:
  /// multicast loopback on, so Bowire on the same machine sees the traffic.
  /// </summary>
  internal sealed class UdpDatagramSink : IDatagramSink, IDisposable
  {
      private readonly UdpClient _client = new(AddressFamily.InterNetwork);
      private readonly IPEndPoint _target;

      public UdpDatagramSink(IOptions<SandboxOptions> options)
      {
          _target = options.Value.TargetEndPoint;
          _client.EnableBroadcast = true;
          _client.MulticastLoopback = true;
          _client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, options.Value.Ttl);
          _client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
      }

      public void Send(byte[] datagram) => _client.Send(datagram, datagram.Length, _target);

      public void Dispose() => _client.Dispose();
  }
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/PublisherStatus.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using System.Collections.Concurrent;
  using Microsoft.Extensions.Options;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

  /// <summary>What the status bar and the entity list show about sending.</summary>
  internal sealed class PublisherStatus(IOptions<SandboxOptions> options)
  {
      private readonly ConcurrentDictionary<ushort, string> _encodeFailures = new();
      private long _sent;
      private long _errors;

      public string Target { get; } = options.Value.Target;

      public long Sent => Interlocked.Read(ref _sent);

      public long Errors => Interlocked.Read(ref _errors);

      public string? LastError { get; private set; }

      public DateTimeOffset? LastErrorAt { get; private set; }

      public IReadOnlyDictionary<ushort, string> EncodeFailures => _encodeFailures;

      public event Action? Changed;

      public void RecordSent()
      {
          Interlocked.Increment(ref _sent);
          Changed?.Invoke();
      }

      public void RecordSendError(Exception error, DateTimeOffset at)
      {
          Interlocked.Increment(ref _errors);
          LastError = error.Message;
          LastErrorAt = at;
          Changed?.Invoke();
      }

      public void MarkEncodeFailed(ushort number, string message)
      {
          _encodeFailures[number] = message;
          Changed?.Invoke();
      }

      public void ClearEncodeFailure(ushort number)
      {
          if (_encodeFailures.TryRemove(number, out _))
              Changed?.Invoke();
      }
  }
  ```

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing/DisPublisher.cs`:
  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
  using Microsoft.Extensions.Options;

  namespace Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;

  /// <summary>
  /// Puts every change in the store on the wire. Most changes go out at
  /// once; moves while following the mouse wait for the next tick, so at most
  /// <see cref="SandboxOptions.FollowMaxUpdatesPerSecond"/> go out per entity.
  /// The tick also sends the heartbeat, so a receiver that joins late has the
  /// full picture after one interval. Nothing that fails while sending
  /// stops the UI or the heartbeat.
  /// </summary>
  internal sealed class DisPublisher : BackgroundService
  {
      private readonly EntityStore _store;
      private readonly PduFactory _factory;
      private readonly IDatagramSink _sink;
      private readonly PublisherStatus _status;
      private readonly TimeProvider _time;
      private readonly ILogger<DisPublisher> _logger;
      private readonly TimeSpan _tickInterval;
      private readonly TimeSpan _heartbeatInterval;
      private readonly Lock _gate = new();
      private readonly HashSet<ushort> _pendingFollowMoves = [];
      private DateTimeOffset _nextHeartbeat;

      public DisPublisher(
          EntityStore store, PduFactory factory, IDatagramSink sink, PublisherStatus status,
          IOptions<SandboxOptions> options, TimeProvider time, ILogger<DisPublisher> logger)
      {
          _store = store;
          _factory = factory;
          _sink = sink;
          _status = status;
          _time = time;
          _logger = logger;
          _tickInterval = TimeSpan.FromSeconds(1.0 / options.Value.FollowMaxUpdatesPerSecond);
          _heartbeatInterval = TimeSpan.FromSeconds(options.Value.HeartbeatSeconds);
          _nextHeartbeat = time.GetUtcNow() + _heartbeatInterval;
          _store.Changed += OnChanged;
      }

      public override void Dispose()
      {
          _store.Changed -= OnChanged;
          base.Dispose();
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          _logger.LogInformation("DIS sandbox sending to udp://{Target}.", _status.Target);
          using var timer = new PeriodicTimer(_tickInterval, _time);
          try
          {
              while (await timer.WaitForNextTickAsync(stoppingToken))
                  Tick();
          }
          catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
          {
              // Shutting down.
          }
      }

      /// <summary>Sends pending follow moves, and the heartbeat when it is due.</summary>
      internal void Tick()
      {
          ushort[] due;
          lock (_gate)
          {
              due = [.. _pendingFollowMoves];
              _pendingFollowMoves.Clear();
          }
          foreach (var number in due)
          {
              if (_store.Get(number) is { } entity)
                  SendEntityState(entity, deactivated: false);
          }

          var now = _time.GetUtcNow();
          if (now < _nextHeartbeat)
              return;
          _nextHeartbeat = now + _heartbeatInterval;
          foreach (var entity in _store.Snapshot())
          {
              SendEntityState(entity, deactivated: false);
              if (entity.Pairs.Count > 0)
                  SendComment(entity);
          }
      }

      private void OnChanged(EntityChange change)
      {
          var entity = change.Entity;
          switch (change.Kind)
          {
              case EntityChangeKind.Moved when entity.Following:
                  lock (_gate)
                      _pendingFollowMoves.Add(entity.Number);
                  break;
              case EntityChangeKind.Added or EntityChangeKind.Moved or EntityChangeKind.Updated:
                  SendEntityState(entity, deactivated: false);
                  break;
              case EntityChangeKind.PairsChanged:
                  // Sent even when empty: the receiver has to see the pairs go.
                  SendComment(entity);
                  break;
              case EntityChangeKind.Removed:
                  lock (_gate)
                      _pendingFollowMoves.Remove(entity.Number);
                  SendEntityState(entity, deactivated: true);
                  break;
          }
      }

      private void SendEntityState(SandboxEntity entity, bool deactivated) =>
          Send(entity.Number, () => _factory.EntityState(entity, deactivated));

      private void SendComment(SandboxEntity entity) =>
          Send(entity.Number, () => _factory.Comment(entity));

      private void Send(ushort number, Func<byte[]> encode)
      {
          byte[] datagram;
          try
          {
              datagram = encode();
              _status.ClearEncodeFailure(number);
          }
          catch (Exception ex)
          {
              _logger.LogError(ex, "Could not encode a PDU for entity {Number}.", number);
              _status.MarkEncodeFailed(number, ex.Message);
              return;
          }

          try
          {
              _sink.Send(datagram);
              _status.RecordSent();
          }
          catch (Exception ex)
          {
              _logger.LogWarning("Sending to udp://{Target} failed: {Message}", _status.Target, ex.Message);
              _status.RecordSendError(ex, _time.GetUtcNow());
          }
      }
  }
  ```

- [ ] **Step 5: Tests laufen lassen, sie müssen bestehen**

  Run: `dotnet test --project tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests`
  Expected: alle Tests des Projekts grün.

- [ ] **Step 6: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Publishing tests/Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Tests
  git commit -m "feat(sandbox): Publisher mit Folge-Drosselung, Heartbeat und Fehlerzaehler (#67)"
  ```

---

### Task 6: Blazor-Hülle, DI und Statuszeile

**Files:**
- Modify: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Program.cs`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/App.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/Routes.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/_Imports.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/Layout/MainLayout.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/StatusBar.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/Pages/Home.razor` (vorläufig)
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/wwwroot/app.css`

**Interfaces:**
- Consumes: alle Dienste aus Task 1–5.
- Produces: eine startfähige App unter `http://localhost:5185` mit Statuszeile. Task 7 ersetzt `Home.razor`.

- [ ] **Step 1: `Program.cs` vervollständigen**

  ```csharp
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0

  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Components;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities;
  using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing;
  using Microsoft.Extensions.Options;

  // A map on which you place and move objects; every change goes out as DIS
  // (Entity State, Comment) so Bowire's DIS tab has something to show that
  // you control. See README.md.

  var builder = WebApplication.CreateBuilder(args);

  builder.Services.AddOptions<SandboxOptions>()
      .Bind(builder.Configuration.GetSection(SandboxOptions.Section))
      .ValidateOnStart();
  builder.Services.AddSingleton<IValidateOptions<SandboxOptions>, SandboxOptionsValidator>();

  builder.Services.AddSingleton(TimeProvider.System);
  builder.Services.AddSingleton<EntityStore>();
  builder.Services.AddSingleton<PduFactory>();
  builder.Services.AddSingleton<PublisherStatus>();
  builder.Services.AddSingleton<IDatagramSink, UdpDatagramSink>();
  builder.Services.AddSingleton<DisPublisher>();
  builder.Services.AddHostedService(sp => sp.GetRequiredService<DisPublisher>());

  builder.Services.AddRazorComponents().AddInteractiveServerComponents();

  var app = builder.Build();

  app.UseAntiforgery();
  app.MapStaticAssets();
  app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

  app.Run();
  ```

- [ ] **Step 2: Komponenten-Gerüst anlegen**

  `Components/_Imports.razor`:
  ```razor
  @using Microsoft.AspNetCore.Components.Routing
  @using Microsoft.AspNetCore.Components.Web
  @using static Microsoft.AspNetCore.Components.Web.RenderMode
  @using Microsoft.Extensions.Options
  @using Microsoft.JSInterop
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Components
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Components.Layout
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Entities
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Geo
  @using Kuestenlogik.Bowire.Protocol.Dis.Sandbox.Publishing
  ```

  `Components/App.razor`:
  ```razor
  <!DOCTYPE html>
  <html lang="de">
  <head>
      <meta charset="utf-8" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <base href="/" />
      <title>DIS-Sandbox</title>
      <link rel="stylesheet" href="https://unpkg.com/maplibre-gl@5.18.0/dist/maplibre-gl.css" />
      <link rel="stylesheet" href="@Assets["app.css"]" />
      <ImportMap />
      <HeadOutlet @rendermode="InteractiveServer" />
  </head>
  <body>
      <Routes @rendermode="InteractiveServer" />
      <script src="https://unpkg.com/maplibre-gl@5.18.0/dist/maplibre-gl.js"></script>
      <script src="_framework/blazor.web.js"></script>
  </body>
  </html>
  ```

  `Components/Routes.razor`:
  ```razor
  <Router AppAssembly="typeof(Program).Assembly">
      <Found Context="routeData">
          <RouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
      </Found>
  </Router>
  ```

  `Components/Layout/MainLayout.razor`:
  ```razor
  @inherits LayoutComponentBase

  <main class="shell">@Body</main>
  ```

  `Components/StatusBar.razor`:
  ```razor
  @implements IDisposable
  @inject PublisherStatus Status

  <header class="status-bar @(Status.Errors > 0 ? "has-errors" : "")">
      <span>Ziel <code>udp://@Status.Target</code></span>
      <span>gesendet @Status.Sent</span>
      <span>Fehler @Status.Errors</span>
      @if (Status.LastError is { } error)
      {
          <span class="last-error" title="@Status.LastErrorAt">zuletzt: @error</span>
      }
  </header>

  @code {
      protected override void OnInitialized() => Status.Changed += OnStatusChanged;

      private void OnStatusChanged() => _ = InvokeAsync(StateHasChanged);

      public void Dispose() => Status.Changed -= OnStatusChanged;
  }
  ```

  `Components/Pages/Home.razor` (vorläufig, Task 7 ersetzt ihn):
  ```razor
  @page "/"

  <StatusBar />
  ```

  `wwwroot/app.css`:
  ```css
  :root {
      --bg: #15171a;
      --panel: #1f2226;
      --line: #33373d;
      --text: #e6e6e6;
      --muted: #9aa0a6;
      --accent: #3b82f6;
      --error: #ef4444;
      color-scheme: dark;
  }
  html, body { margin: 0; height: 100%; background: var(--bg); color: var(--text); font: 14px/1.4 system-ui, sans-serif; }
  .shell { display: flex; flex-direction: column; height: 100vh; }
  .status-bar { display: flex; gap: 1.5rem; padding: .4rem .8rem; background: var(--panel); border-bottom: 1px solid var(--line); }
  .status-bar.has-errors { border-bottom-color: var(--error); }
  .status-bar .last-error { color: var(--error); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .workspace { flex: 1; display: flex; min-height: 0; }
  .map { flex: 1; min-width: 0; }
  .sidebar { width: 380px; overflow-y: auto; padding: .8rem; background: var(--panel); border-left: 1px solid var(--line); display: flex; flex-direction: column; gap: .8rem; }
  .toolbar { display: flex; gap: .5rem; flex-wrap: wrap; }
  button, select, input { background: var(--bg); color: var(--text); border: 1px solid var(--line); border-radius: 4px; padding: .3rem .5rem; font: inherit; }
  button.active, button:hover { border-color: var(--accent); }
  button.danger:hover { border-color: var(--error); color: var(--error); }
  .hint { color: var(--muted); margin: 0; }
  .error { color: var(--error); margin: 0; }
  table { width: 100%; border-collapse: collapse; }
  td, th { padding: .25rem .3rem; border-bottom: 1px solid var(--line); text-align: left; }
  tr.selected { background: #26303d; }
  tbody tr { cursor: pointer; }
  .dot { display: inline-block; width: .7rem; height: .7rem; border-radius: 50%; }
  .force-1 { background: #3b82f6; } .force-2 { background: #ef4444; } .force-3 { background: #22c55e; }
  .editor label { display: block; margin-bottom: .5rem; }
  .editor input, .editor select { width: 100%; box-sizing: border-box; }
  .pairs td input { width: 100%; box-sizing: border-box; }
  .entity-marker { display: flex; align-items: center; gap: .3rem; cursor: pointer; }
  .entity-marker .dot { width: 14px; height: 14px; border: 2px solid #fff; box-shadow: 0 0 2px #000; }
  .entity-marker .label { background: rgba(0,0,0,.65); color: #fff; padding: 0 .3rem; border-radius: 3px; font-size: 12px; white-space: nowrap; }
  .entity-marker.selected .dot { outline: 3px solid #facc15; }
  ```

- [ ] **Step 3: Bauen**

  Run: `dotnet build Bowire.Protocol.Dis.slnx -c Release`
  Expected: 0 Warnungen, 0 Fehler.

- [ ] **Step 4: Starten und prüfen**

  Run (Hintergrund): `dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox --launch-profile http`
  Prüfen: `curl -s http://localhost:5185/ | grep -c "udp://239.1.2.3:3000"` liefert `1`. Im Log steht `DIS sandbox sending to udp://239.1.2.3:3000.` Danach den Prozess beenden.
  Gegenprobe Validierung: `dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox -- --Sandbox:Target=nope` bricht beim Start mit `Sandbox:Target 'nope' is not an IPv4 endpoint` ab.

- [ ] **Step 5: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox
  git commit -m "feat(sandbox): Blazor-Huelle mit Statuszeile, Publisher als Hosted Service (#67)"
  ```

---

### Task 7: Karte, Objektliste und Editor

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/wwwroot/sandbox-map.js`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/MapView.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/EntityList.razor`
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/EntityEditor.razor`
- Modify: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/Components/Pages/Home.razor` (ersetzen)

**Interfaces:**
- Consumes: `EntityStore`, `EntityPreset`, `PairRules`, `PduFactory.MarkingText`, `PublisherStatus`, `SandboxOptions`.
- Produces: `MapView` mit Parametern `Ready` (`EventCallback`), `MapClicked` (`EventCallback<MapPoint>`), `MarkerMoved` (`EventCallback<MarkerMove>`), `MarkerSelected` (`EventCallback<ushort>`), `FollowEnded` (`EventCallback<MarkerMove>`) und Methoden `UpsertAsync(SandboxEntity, bool selected)`, `RemoveAsync(ushort)`, `SetBasemapAsync(string)`, `StartFollowAsync(ushort)`, `StopFollowAsync()`. Records `MapPoint(double Latitude, double Longitude)` und `MarkerMove(ushort Number, double Latitude, double Longitude, bool Following)` stehen im `@code` von `MapView` als `public sealed record` und werden als `MapView.MapPoint`/`MapView.MarkerMove` angesprochen.

UI-Komponenten haben keine automatischen Tests (Spec, Abschnitt Tests). Die Verifikation passiert in Step 6 von Hand bzw. mit dem Playwright-MCP.

- [ ] **Step 1: JS-Modul schreiben**

  `wwwroot/sandbox-map.js`:
  ```js
  // Copyright 2026 Küstenlogik
  // SPDX-License-Identifier: Apache-2.0
  //
  // Thin MapLibre layer. It knows markers, not entities: it reports clicks
  // and moves to Blazor and draws what Blazor tells it to.

  const forceColours = { 1: '#3b82f6', 2: '#ef4444', 3: '#22c55e' };

  let map = null;
  let dotnet = null;
  let basemaps = [];
  let followIntervalMs = 100;
  let following = null;
  let lastFollowSend = 0;
  const markers = new Map();

  function styleFor(basemap) {
    if (basemap.kind === 'style') return basemap.url;
    if (basemap.kind === 'raster') {
      return {
        version: 8,
        sources: { base: { type: 'raster', tiles: basemap.tiles, tileSize: 256, attribution: basemap.attribution ?? '' } },
        layers: [{ id: 'base', type: 'raster', source: 'base' }],
      };
    }
    return { version: 8, sources: {}, layers: [{ id: 'background', type: 'background', paint: { 'background-color': basemap.color } }] };
  }

  function endFollow(report) {
    if (following === null) return;
    const id = following;
    const marker = markers.get(id);
    following = null;
    map.getCanvas().style.cursor = '';
    if (!marker) return;
    marker.getElement().style.pointerEvents = '';
    marker.setDraggable(true);
    if (report) {
      const p = marker.getLngLat();
      dotnet.invokeMethodAsync('OnFollowEnded', id, p.lat, p.lng);
    }
  }

  function onKeyDown(e) {
    if (e.key === 'Escape') endFollow(true);
  }

  export function init(element, dotnetRef, options) {
    dotnet = dotnetRef;
    basemaps = options.basemaps;
    followIntervalMs = 1000 / options.followMaxUpdatesPerSecond;
    map = new maplibregl.Map({
      container: element,
      style: styleFor(basemaps[0]),
      center: [options.longitude, options.latitude],
      zoom: options.zoom,
    });
    map.addControl(new maplibregl.NavigationControl(), 'top-left');

    map.on('click', e => {
      if (following !== null) {
        markers.get(following)?.setLngLat(e.lngLat);
        endFollow(true);
        return;
      }
      dotnet.invokeMethodAsync('OnMapClick', e.lngLat.lat, e.lngLat.lng);
    });

    map.on('mousemove', e => {
      if (following === null) return;
      markers.get(following)?.setLngLat(e.lngLat);
      const now = performance.now();
      if (now - lastFollowSend < followIntervalMs) return;
      lastFollowSend = now;
      dotnet.invokeMethodAsync('OnMarkerMoved', following, e.lngLat.lat, e.lngLat.lng, true);
    });

    window.addEventListener('keydown', onKeyDown);
  }

  export function setBasemap(id) {
    const basemap = basemaps.find(b => b.id === id);
    // Markers are DOM elements on top of the map and survive a style change.
    if (basemap) map.setStyle(styleFor(basemap));
  }

  export function upsertMarker(m) {
    let marker = markers.get(m.id);
    if (!marker) {
      const el = document.createElement('div');
      el.className = 'entity-marker';
      el.innerHTML = '<span class="dot"></span><span class="label"></span>';
      el.addEventListener('click', ev => {
        ev.stopPropagation();
        dotnet.invokeMethodAsync('OnMarkerSelected', m.id);
      });
      marker = new maplibregl.Marker({ element: el, draggable: true, anchor: 'left' })
        .setLngLat([m.longitude, m.latitude])
        .addTo(map);
      marker.on('dragend', () => {
        const p = marker.getLngLat();
        dotnet.invokeMethodAsync('OnMarkerMoved', m.id, p.lat, p.lng, false);
      });
      markers.set(m.id, marker);
    }
    const el = marker.getElement();
    el.querySelector('.dot').style.background = forceColours[m.force] ?? '#888';
    el.querySelector('.label').textContent = m.name;
    el.classList.toggle('selected', m.selected);
    // While following, the mouse owns the position; Blazor's echo lags behind it.
    if (following !== m.id) marker.setLngLat([m.longitude, m.latitude]);
  }

  export function removeMarker(id) {
    if (following === id) endFollow(false);
    markers.get(id)?.remove();
    markers.delete(id);
  }

  export function startFollow(id) {
    endFollow(true);
    const marker = markers.get(id);
    if (!marker) return;
    following = id;
    lastFollowSend = 0;
    marker.setDraggable(false);
    // Let clicks and moves through to the map: the marker sits under the cursor.
    marker.getElement().style.pointerEvents = 'none';
    map.getCanvas().style.cursor = 'crosshair';
  }

  export function stopFollow() {
    endFollow(false);
  }

  export function dispose() {
    window.removeEventListener('keydown', onKeyDown);
    following = null;
    markers.clear();
    map?.remove();
    map = null;
  }
  ```

- [ ] **Step 2: `MapView.razor` schreiben**

  ```razor
  @implements IAsyncDisposable
  @inject IJSRuntime JS
  @inject IOptions<SandboxOptions> Options

  <div class="map" @ref="_element"></div>

  @code {
      public sealed record MapPoint(double Latitude, double Longitude);

      public sealed record MarkerMove(ushort Number, double Latitude, double Longitude, bool Following);

      private ElementReference _element;
      private IJSObjectReference? _module;
      private DotNetObjectReference<MapView>? _self;

      [Parameter] public EventCallback Ready { get; set; }
      [Parameter] public EventCallback<MapPoint> MapClicked { get; set; }
      [Parameter] public EventCallback<MarkerMove> MarkerMoved { get; set; }
      [Parameter] public EventCallback<ushort> MarkerSelected { get; set; }
      [Parameter] public EventCallback<MarkerMove> FollowEnded { get; set; }

      public bool IsReady => _module is not null;

      protected override async Task OnAfterRenderAsync(bool firstRender)
      {
          if (!firstRender)
              return;
          var options = Options.Value;
          _module = await JS.InvokeAsync<IJSObjectReference>("import", "./sandbox-map.js");
          _self = DotNetObjectReference.Create(this);
          await _module.InvokeVoidAsync("init", _element, _self, new
          {
              basemaps = options.Basemaps.Select(b => new
              {
                  id = b.Id, name = b.Name, kind = b.Kind, url = b.Url,
                  tiles = b.Tiles, attribution = b.Attribution, color = b.Color,
              }),
              latitude = options.InitialView.Latitude,
              longitude = options.InitialView.Longitude,
              zoom = options.InitialView.Zoom,
              followMaxUpdatesPerSecond = options.FollowMaxUpdatesPerSecond,
          });
          await Ready.InvokeAsync();
      }

      public async ValueTask UpsertAsync(SandboxEntity entity, bool selected)
      {
          if (_module is null)
              return;
          await _module.InvokeVoidAsync("upsertMarker", new
          {
              id = entity.Number,
              name = entity.Name,
              force = (int)entity.Force,
              latitude = entity.Position.Latitude,
              longitude = entity.Position.Longitude,
              selected,
          });
      }

      public async ValueTask RemoveAsync(ushort number)
      {
          if (_module is not null)
              await _module.InvokeVoidAsync("removeMarker", number);
      }

      public async ValueTask SetBasemapAsync(string id)
      {
          if (_module is not null)
              await _module.InvokeVoidAsync("setBasemap", id);
      }

      public async ValueTask StartFollowAsync(ushort number)
      {
          if (_module is not null)
              await _module.InvokeVoidAsync("startFollow", number);
      }

      public async ValueTask StopFollowAsync()
      {
          if (_module is not null)
              await _module.InvokeVoidAsync("stopFollow");
      }

      [JSInvokable]
      public Task OnMapClick(double latitude, double longitude) =>
          MapClicked.InvokeAsync(new MapPoint(latitude, longitude));

      [JSInvokable]
      public Task OnMarkerMoved(ushort number, double latitude, double longitude, bool following) =>
          MarkerMoved.InvokeAsync(new MarkerMove(number, latitude, longitude, following));

      [JSInvokable]
      public Task OnMarkerSelected(ushort number) => MarkerSelected.InvokeAsync(number);

      [JSInvokable]
      public Task OnFollowEnded(ushort number, double latitude, double longitude) =>
          FollowEnded.InvokeAsync(new MarkerMove(number, latitude, longitude, Following: false));

      public async ValueTask DisposeAsync()
      {
          if (_module is not null)
          {
              try
              {
                  await _module.InvokeVoidAsync("dispose");
                  await _module.DisposeAsync();
              }
              catch (JSDisconnectedException)
              {
                  // The circuit is gone; so is the map.
              }
          }
          _self?.Dispose();
      }
  }
  ```

- [ ] **Step 3: `EntityList.razor` schreiben**

  ```razor
  @implements IDisposable
  @inject PublisherStatus Status

  @if (Entities.Count == 0)
  {
      <p class="hint">Noch keine Objekte. „Neues Objekt“, dann auf die Karte klicken.</p>
  }
  else
  {
      <table>
          <thead><tr><th></th><th>Nr.</th><th>Name</th><th>Typ</th><th></th></tr></thead>
          <tbody>
              @foreach (var entity in Entities)
              {
                  <tr class="@(entity.Number == Selected ? "selected" : "")" @onclick="() => SelectedChanged.InvokeAsync(entity.Number)">
                      <td><span class="dot force-@((int)entity.Force)"></span></td>
                      <td>@entity.Number</td>
                      <td>@entity.Name</td>
                      <td>@EntityPreset.Get(entity.PresetId).Label</td>
                      <td>
                          @if (Status.EncodeFailures.TryGetValue(entity.Number, out var failure))
                          {
                              <span class="error" title="@failure">⚠</span>
                          }
                      </td>
                  </tr>
              }
          </tbody>
      </table>
  }

  @code {
      [Parameter, EditorRequired] public IReadOnlyList<SandboxEntity> Entities { get; set; } = [];
      [Parameter] public ushort? Selected { get; set; }
      [Parameter] public EventCallback<ushort> SelectedChanged { get; set; }

      protected override void OnInitialized() => Status.Changed += OnStatusChanged;

      private void OnStatusChanged() => _ = InvokeAsync(StateHasChanged);

      public void Dispose() => Status.Changed -= OnStatusChanged;
  }
  ```

- [ ] **Step 4: `EntityEditor.razor` schreiben**

  ```razor
  @inject EntityStore Store

  <section class="editor">
      <h3>Objekt @Entity.Number</h3>

      <label>Name
          <input value="@_name" @onchange="OnNameChanged" maxlength="64" />
      </label>
      <p class="hint">Gesendet als Marking: <code>@PduFactory.MarkingText(_name)</code></p>

      <label>Typ
          <select value="@Entity.PresetId" @onchange="OnPresetChanged">
              @foreach (var preset in EntityPreset.All)
              {
                  <option value="@preset.Id">@preset.Label</option>
              }
          </select>
      </label>

      <label>Kräfte
          <select value="@Entity.Force" @onchange="OnForceChanged">
              <option value="@Force.Friendly">Eigene</option>
              <option value="@Force.Opposing">Gegnerische</option>
              <option value="@Force.Neutral">Neutrale</option>
          </select>
      </label>

      <label>Höhe über Ellipsoid (m)
          <input type="number" step="1" value="@Entity.Position.Altitude" @onchange="OnAltitudeChanged" />
      </label>

      <p class="hint">
          @Entity.Position.Latitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture),
          @Entity.Position.Longitude.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)
          · Kurs @Entity.HeadingDegrees.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)°
          · @Entity.SpeedMetresPerSecond.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) m/s
      </p>

      <div class="toolbar">
          <button class="@(Following ? "active" : "")" @onclick="() => FollowRequested.InvokeAsync()" disabled="@Following">
              Maus folgen
          </button>
          <button class="danger" @onclick="() => Store.Remove(Entity.Number)">Löschen</button>
      </div>
      @if (Following)
      {
          <p class="hint">Klick setzt das Objekt ab, Esc bricht ab.</p>
      }

      <h4>Key-Value-Paare</h4>
      <table class="pairs">
          <tbody>
              @foreach (var row in _rows)
              {
                  <tr>
                      <td><input placeholder="Schlüssel" @bind="row.Key" @bind:event="oninput" /></td>
                      <td><input placeholder="Wert" @bind="row.Value" @bind:event="oninput" /></td>
                      <td><button @onclick="() => _rows.Remove(row)" title="Entfernen">✕</button></td>
                  </tr>
              }
          </tbody>
      </table>
      <div class="toolbar">
          <button @onclick="() => _rows.Add(new PairRow())">Paar hinzufügen</button>
          <button @onclick="ApplyPairs">Übernehmen</button>
      </div>
      <p class="hint">Comment-PDU: @PairRules.CommentPduLength(Current()) / @PairRules.MaxCommentPduBytes Byte</p>
      @if (_error is not null)
      {
          <p class="error">@_error</p>
      }
  </section>

  @code {
      private sealed class PairRow
      {
          public string Key { get; set; } = "";
          public string Value { get; set; } = "";
      }

      private string _name = "";
      private List<PairRow> _rows = [];
      private string? _error;

      [Parameter, EditorRequired] public SandboxEntity Entity { get; set; } = default!;
      [Parameter] public bool Following { get; set; }
      [Parameter] public EventCallback FollowRequested { get; set; }

      // The page gives every entity its own editor (@key), so this runs once
      // per selection and moves on the map do not overwrite unsaved edits.
      protected override void OnInitialized()
      {
          _name = Entity.Name;
          _rows = [.. Entity.Pairs.Select(p => new PairRow { Key = p.Key, Value = p.Value })];
      }

      private List<KeyValuePair<string, string>> Current() =>
          [.. _rows.Select(r => new KeyValuePair<string, string>(r.Key.Trim(), r.Value))];

      private void OnNameChanged(ChangeEventArgs e)
      {
          var name = (e.Value as string ?? "").Trim();
          if (name.Length == 0)
          {
              _error = "Der Name darf nicht leer sein.";
              return;
          }
          _name = name;
          _error = null;
          Store.Update(Entity.Number, name, Entity.PresetId, Entity.Force, Entity.Position.Altitude);
      }

      private void OnPresetChanged(ChangeEventArgs e) =>
          Store.Update(Entity.Number, _name, e.Value as string ?? Entity.PresetId, Entity.Force, Entity.Position.Altitude);

      private void OnForceChanged(ChangeEventArgs e)
      {
          if (Enum.TryParse<Force>(e.Value as string, out var force))
              Store.Update(Entity.Number, _name, Entity.PresetId, force, Entity.Position.Altitude);
      }

      private void OnAltitudeChanged(ChangeEventArgs e)
      {
          if (double.TryParse(e.Value as string, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var altitude))
              Store.Update(Entity.Number, _name, Entity.PresetId, Entity.Force, altitude);
      }

      private void ApplyPairs() => Store.TrySetPairs(Entity.Number, Current(), out _error);
  }
  ```

- [ ] **Step 5: `Home.razor` ersetzen**

  ```razor
  @page "/"
  @implements IDisposable
  @inject EntityStore Store
  @inject IOptions<SandboxOptions> Options

  <StatusBar />
  <div class="workspace">
      <MapView @ref="_map" Ready="OnMapReady" MapClicked="OnMapClicked" MarkerMoved="OnMarkerMoved"
               MarkerSelected="SelectAsync" FollowEnded="OnFollowEnded" />
      <aside class="sidebar">
          <div class="toolbar">
              <select @onchange="OnBasemapChanged" title="Grundkarte">
                  @foreach (var basemap in Options.Value.Basemaps)
                  {
                      <option value="@basemap.Id">@basemap.Name</option>
                  }
              </select>
              <select @bind="_presetForNew" title="Typ des neuen Objekts">
                  @foreach (var preset in EntityPreset.All)
                  {
                      <option value="@preset.Id">@preset.Label</option>
                  }
              </select>
              <button class="@(_placing ? "active" : "")" @onclick="() => _placing = !_placing">Neues Objekt</button>
          </div>
          @if (_placing)
          {
              <p class="hint">Klick auf die Karte setzt das Objekt.</p>
          }

          <EntityList Entities="_entities" Selected="_selected" SelectedChanged="SelectAsync" />

          @if (_selected is { } number && Store.Get(number) is { } entity)
          {
              <EntityEditor @key="number" Entity="entity" Following="_following == number" FollowRequested="StartFollowAsync" />
          }
      </aside>
  </div>

  @code {
      private MapView? _map;
      private IReadOnlyList<SandboxEntity> _entities = [];
      private ushort? _selected;
      private ushort? _following;
      private bool _placing;
      private string _presetForNew = EntityPreset.All[0].Id;

      protected override void OnInitialized()
      {
          _entities = Store.Snapshot();
          Store.Changed += OnStoreChanged;
      }

      private async Task OnMapReady()
      {
          foreach (var entity in Store.Snapshot())
              await _map!.UpsertAsync(entity, entity.Number == _selected);
      }

      // Changes come from this page, other tabs and nobody else; all of them
      // are drawn from here, so the map shows what the store holds.
      private void OnStoreChanged(EntityChange change) => _ = InvokeAsync(async () =>
      {
          _entities = Store.Snapshot();
          var number = change.Entity.Number;
          if (change.Kind == EntityChangeKind.Removed)
          {
              if (_selected == number) _selected = null;
              if (_following == number) _following = null;
              if (_map is not null) await _map.RemoveAsync(number);
          }
          else if (_map is not null)
          {
              await _map.UpsertAsync(change.Entity, number == _selected);
          }
          StateHasChanged();
      });

      private async Task OnMapClicked(MapView.MapPoint point)
      {
          if (_placing)
          {
              _placing = false;
              var entity = Store.Add(_presetForNew, Force.Friendly, point.Latitude, point.Longitude);
              await SelectAsync(entity.Number);
              return;
          }
          await SelectAsync(null);
      }

      private void OnMarkerMoved(MapView.MarkerMove move) =>
          Store.Move(move.Number, move.Latitude, move.Longitude, move.Following);

      private void OnFollowEnded(MapView.MarkerMove move)
      {
          Store.Move(move.Number, move.Latitude, move.Longitude, following: true);
          Store.StopFollowing(move.Number);
          _following = null;
      }

      private async Task SelectAsync(ushort? number)
      {
          var previous = _selected;
          _selected = number;
          foreach (var n in new[] { previous, number })
          {
              if (n is { } id && Store.Get(id) is { } entity && _map is not null)
                  await _map.UpsertAsync(entity, id == _selected);
          }
      }

      private async Task StartFollowAsync()
      {
          if (_selected is not { } number || _map is null)
              return;
          _following = number;
          await _map.StartFollowAsync(number);
      }

      private async Task OnBasemapChanged(ChangeEventArgs e)
      {
          if (_map is not null && e.Value is string id)
              await _map.SetBasemapAsync(id);
      }

      public void Dispose()
      {
          Store.Changed -= OnStoreChanged;
          // A dropped circuit must not leave an object "moving" forever.
          if (_following is { } number)
              Store.StopFollowing(number);
      }
  }
  ```

  Hinweis: `SelectAsync` nimmt `ushort?`. `EntityList.SelectedChanged` und `MapView.MarkerSelected` liefern `ushort`. Meldet der Compiler einen Typkonflikt bei `SelectedChanged="SelectAsync"` bzw. `MarkerSelected="SelectAsync"`, dort per Lambda adaptieren: `SelectedChanged="(ushort n) => SelectAsync(n)"`.

- [ ] **Step 6: Bauen, starten und von Hand prüfen**

  Run: `dotnet build Bowire.Protocol.Dis.slnx -c Release`. Erwartet: 0 Warnungen.
  Dann `dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox --launch-profile http` und `http://localhost:5185` öffnen (Browser oder Playwright-MCP). Prüfen:
  1. Karte erscheint mit Dark-Style um Hamburg. Der Umschalter wechselt auf OSM, Satellit und Leer, die Marker bleiben dabei stehen.
  2. „Neues Objekt“, Klick auf die Karte: Marker erscheint, ist ausgewählt, die Liste zeigt „Objekt 1“, der Zähler „gesendet“ steigt.
  3. Marker ziehen: Position im Editor ändert sich, 0,0 m/s.
  4. „Maus folgen“: Der Marker hängt am Cursor, die Geschwindigkeit im Editor steigt. Ein Klick setzt ab, die Geschwindigkeit geht auf 0. Mit Esc genauso.
  5. Name „Größenwahn-Panzer“: Die Marking-Zeile zeigt `Gr??enwahn-`.
  6. Paare `status=ready`, `fuel=80` übernehmen: Größenanzeige `80 / 1400 Byte`. Mit Schlüssel `a=b` erscheint die Fehlermeldung.
  7. Löschen: Marker und Listeneintrag verschwinden.
  8. Zweiter Tab: Was in Tab 1 passiert, erscheint auch in Tab 2.

- [ ] **Step 7: Commit**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox
  git commit -m "feat(sandbox): Karte mit Basemaps, Ziehen und Maus folgen, Objektliste und Editor (#67)"
  ```

---

### Task 8: README, Ende-zu-Ende mit Bowire, Push

**Files:**
- Create: `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/README.md`
- Modify: `README.md` (Repo-Root; Abschnitt zu den Samples)

- [ ] **Step 1: README der Sandbox schreiben**

  `samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/README.md`:
  ````markdown
  # Sample — DIS sandbox

  A map on which you place and move objects yourself. Every change goes out
  as DIS v7 on UDP, so Bowire's **DIS** tab shows traffic you control — not
  a fixed recording like the [replay sample](../Kuestenlogik.Bowire.Protocol.Dis.Sample/README.md).

  ```
  map (MapLibre) ──► EntityStore ──► DisPublisher ──UDP/multicast──► Bowire DIS tab
  ```

  The PDUs are encoded with [OpenDisNet](https://github.com/RejectKid/OpenDisNet),
  not with this plugin's code, so the sandbox also checks Bowire's decoder
  against an independent encoder.

  ## Run

  1. **Start the sandbox**

     ```sh
     dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox
     ```

     Open <http://localhost:5185>.

  2. **Subscribe in Bowire** to `dis://239.1.2.3:3000`, for example with the
     replay sample's workbench at <http://localhost:5080/bowire>. The
     sandbox's entities have application id 3001; the replay sample's
     convoy uses a different one.

  ## What to try, and what Bowire should show

  | In the sandbox | In Bowire |
  |---|---|
  | *Neues Objekt*, click the map | an Entity State PDU, entity `1:3001:1`, marking `Objekt 1` |
  | drag a marker | one Entity State with the new position, dead reckoning *Static* |
  | *Maus folgen* | up to 10 Entity States a second, velocity set, dead reckoning *FPW*; a click or Esc ends with one at rest |
  | rename, change type or force | an Entity State with the new marking / entity type / force |
  | set key-value pairs | a Comment PDU, one variable datum per pair, value `key=value` in UTF-8 |
  | clear all pairs | one Comment PDU without records |
  | *Löschen* | a last Entity State with appearance bit 23 (deactivated) |
  | wait | every 5 s an Entity State per object, plus a Comment where it has pairs |

  ## Key-value pairs: a convention, not a standard

  SISO-REF-010 has no datum id for "a key of your choosing" and no range for
  free use. The sandbox puts every pair under one datum id, `900000` by
  default (unassigned in SISO-REF-010 v37), with the text `key=value` as the
  value. Bowire shows the value as raw bytes: the datum id alone says what a
  value means and how it is encoded, so a receiver that does not know the id
  cannot do better.

  ## Configuration

  `appsettings.json`, section `Sandbox`: target (`239.1.2.3:3000`), TTL,
  exercise / site / application id, heartbeat, follow rate, the datum id
  above, the initial view and the basemaps. Every setting can be overridden
  on the command line, e.g. `--Sandbox:Target=192.168.1.255:3000`.

  ## Basemaps

  OpenFreeMap (Dark, Positron, Liberty), OpenStreetMap, Esri World Imagery and
  World Topo Map — online services without an API key, the same set as
  Terracle. Without a network only *Leer* (a plain colour) works; objects and
  DIS keep working either way.
  ````

- [ ] **Step 2: Repo-README ergänzen**

  In `README.md` den Abschnitt suchen, der das Replay-Sample erwähnt (`grep -n "Dis.Sample" README.md`), und direkt dahinter einfügen:
  ```markdown
  - [`samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox`](samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/README.md) — place and move objects on a map and give them key-value pairs; every change goes out as DIS, encoded with an independent library.
  ```
  Gibt es keinen solchen Abschnitt, einen anlegen (`## Samples`) mit beiden Samples als Liste.

- [ ] **Step 3: Ende-zu-Ende mit Bowire**

  1. `dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sample` (Bowire auf 5080, DIS-Plugin geladen).
  2. `dotnet run --project samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox --launch-profile http`.
  3. In `http://localhost:5080/bowire` den DIS-Tab auf `dis://239.1.2.3:3000` öffnen. Die Tabelle „What to try“ aus der README Zeile für Zeile durchgehen und prüfen, dass Bowire jeweils das Beschriebene zeigt.
  4. Was davon abweicht, ist entweder ein Fehler der Sandbox (beheben) oder einer des Plugins (eigenes Ticket im Repo, im Ticket #67 verlinken).

- [ ] **Step 4: Alle Tests und Build**

  Run: `dotnet build Bowire.Protocol.Dis.slnx -c Release && dotnet test --solution Bowire.Protocol.Dis.slnx -c Release --no-build`
  Expected: 0 Warnungen, alle Tests beider Testprojekte grün.

- [ ] **Step 5: Commit und Push**

  ```bash
  git add samples/Kuestenlogik.Bowire.Protocol.Dis.Sandbox/README.md README.md
  git commit -m "docs(sandbox): README der DIS-Sandbox (#67)"
  git pull --rebase
  git push
  ```

- [ ] **Step 6: Ticket aktualisieren**

  In Kuestenlogik/Bowire.Protocol.Dis#67 die erledigten Punkte abhaken (`gh issue edit 67 -R Kuestenlogik/Bowire.Protocol.Dis --body-file …`) und mit einem Kommentar schließen: Commits, was der Ende-zu-Ende-Test ergeben hat, und Links auf eventuell neu angelegte Plugin-Tickets.
