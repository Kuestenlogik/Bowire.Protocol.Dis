# Generates the multi-entity DIS recording.
#
# The PDUs are built by patching the fields we vary into a byte template
# rather than writing all 144 from scratch: every byte we do NOT touch
# then stays exactly what the plugin's own Unmarshal already accepts, and
# the surface for a layout mistake shrinks to the six fields below.
# A test unmarshals every generated step, so a mistake in those six
# cannot ship either.
#
# Offsets are IEEE 1278.1 §5.3.32 (EntityStatePdu, 144 bytes):
#     12  EntityID      site(2) application(2) entity(2)
#     18  ForceId       1
#     20  EntityType    kind(1) domain(1) country(2) cat(1) sub(1) spec(1) extra(1)
#     36  LinearVelocity  3 x float32
#     48  Location      3 x float64, geocentric (ECEF) metres
#     72  Orientation   3 x float32
#    128  Marking       charset(1) + 11 ASCII bytes

import base64
import json
import math
import struct
import io
import os

A = 6378137.0
F = 1.0 / 298.257223563
E2 = F * (2.0 - F)


def to_ecef(lat_deg, lon_deg, alt_m):
    lat = math.radians(lat_deg)
    lon = math.radians(lon_deg)
    n = A / math.sqrt(1.0 - E2 * math.sin(lat) ** 2)
    return (
        (n + alt_m) * math.cos(lat) * math.cos(lon),
        (n + alt_m) * math.cos(lat) * math.sin(lon),
        (n * (1.0 - E2) + alt_m) * math.sin(lat),
    )


def dest(lat, lon, bearing_deg, metres):
    """Move along a great circle — keeps a convoy's leg straight on the map."""
    r = 6371000.0
    br = math.radians(bearing_deg)
    p1 = math.radians(lat)
    l1 = math.radians(lon)
    d = metres / r
    p2 = math.asin(math.sin(p1) * math.cos(d) + math.cos(p1) * math.sin(d) * math.cos(br))
    l2 = l1 + math.atan2(
        math.sin(br) * math.sin(d) * math.cos(p1),
        math.cos(d) - math.sin(p1) * math.sin(p2),
    )
    return math.degrees(p2), math.degrees(l2)


TEMPLATE = bytearray(base64.b64decode(
    "BgEBAQAAAAAAkAAAAAEAAQPoAQABAQDhAQEAAAEBAOEBAQAAAAAAAAAAAAAAAAAAQUy5hAAAAABBJCwQ"
    "AAAAAEFTfEgAAAAAAAAAAAAAAAAAAAAAAAAAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
    "AAAAAAAAAAABQk9XTElORTAxAAAAAAAA"))
assert len(TEMPLATE) == 144, len(TEMPLATE)


def pdu(entity_no, force, etype, marking, lat, lon, alt, heading_deg, speed_mps, timestamp):
    b = bytearray(TEMPLATE)
    struct.pack_into(">I", b, 4, timestamp & 0xFFFFFFFF)
    struct.pack_into(">HHH", b, 12, 1, 1, entity_no)
    b[18] = force
    struct.pack_into(">BBHBBBB", b, 20, *etype)

    # Linear velocity in the ECEF frame: east/north rotated onto the
    # local tangent plane, so a heading reads as motion in the direction
    # the entity is actually travelling rather than as raw axis values.
    hr = math.radians(heading_deg)
    lat_r, lon_r = math.radians(lat), math.radians(lon)
    vn, ve = speed_mps * math.cos(hr), speed_mps * math.sin(hr)
    vx = -ve * math.sin(lon_r) - vn * math.sin(lat_r) * math.cos(lon_r)
    vy = ve * math.cos(lon_r) - vn * math.sin(lat_r) * math.sin(lon_r)
    vz = vn * math.cos(lat_r)
    struct.pack_into(">fff", b, 36, vx, vy, vz)

    struct.pack_into(">ddd", b, 48, *to_ecef(lat, lon, alt))
    struct.pack_into(">fff", b, 72, float(hr), 0.0, 0.0)

    b[128] = 1  # ASCII charset
    name = marking.encode("ascii")[:11]
    b[129:140] = name + b"\x00" * (11 - len(name))
    return base64.b64encode(bytes(b)).decode("ascii")


# Force ids: 1 Friendly, 2 Opposing, 3 Neutral.
# EntityType (kind, domain, country, category, subcategory, specific, extra):
TANK = (1, 1, 225, 1, 1, 0, 0)          # Platform / Land / Germany / Tank
TRUCK = (1, 1, 225, 7, 1, 0, 0)         # Platform / Land / Germany / Utility vehicle
UAV = (1, 2, 225, 50, 1, 0, 0)          # Platform / Air / Germany / UAV
TANK_OPFOR = (1, 1, 222, 1, 1, 0, 0)    # Platform / Land / Russia / Tank

# Four spatially separated groups across Schleswig-Holstein / Mecklenburg,
# so the map shows distinct clusters rather than one overlapping smudge.
GROUPS = [
    # Convoy Alpha — three tanks nose-to-tail heading east along a road.
    dict(kind="convoy", name="ALPHA", force=1, etype=TANK, alt=18.0,
         members=[("ALPHA-1", 0), ("ALPHA-2", 60), ("ALPHA-3", 120)],
         lat=54.09, lon=10.20, bearing=90.0, speed=11.0),
    # Convoy Bravo — two trucks heading south, well clear of Alpha.
    dict(kind="convoy", name="BRAVO", force=1, etype=TRUCK, alt=32.0,
         members=[("BRAVO-1", 0), ("BRAVO-2", 80)],
         lat=53.86, lon=11.05, bearing=180.0, speed=16.0),
    # Drone Kite — one UAV orbiting at 1200 m over the Bay of Lübeck.
    dict(kind="orbit", name="KITE", force=1, etype=UAV, alt=1200.0,
         members=[("KITE-07", 0)],
         lat=54.02, lon=11.50, radius=2500.0, speed=38.0),
    # A tank engagement: two pairs closing head-on across open ground.
    dict(kind="closing", name="ENGAGEMENT", force=1, etype=TANK, alt=8.0,
         members=[("BLAU-1", 0), ("BLAU-2", 90)],
         lat=54.30, lon=10.75, bearing=135.0, speed=9.0),
    dict(kind="closing", name="ENGAGEMENT", force=2, etype=TANK_OPFOR, alt=8.0,
         members=[("ROT-1", 0), ("ROT-2", 90)],
         lat=54.24, lon=10.83, bearing=315.0, speed=9.0),
]

TICKS = 24          # ~2 minutes of exercise at one PDU per entity per tick
TICK_SECONDS = 5

steps = []
entity_no = 1000
assignments = []
for g in GROUPS:
    for marking, offset in g["members"]:
        entity_no += 1
        assignments.append((entity_no, marking, offset, g))

for tick in range(TICKS):
    elapsed = tick * TICK_SECONDS
    for entity, marking, offset, g in assignments:
        if g["kind"] == "orbit":
            # One full orbit every two minutes.
            ang = (elapsed / 120.0) * 360.0
            lat, lon = dest(g["lat"], g["lon"], ang, g["radius"])
            heading = (ang + 90.0) % 360.0
        else:
            # Members trail the leader by their offset in metres. The
            # offset is NOT clamped at zero: clamping would stack the
            # whole convoy on one point until the leader had driven far
            # enough to clear it, and the first frames are exactly when
            # someone is looking at whether the grouping works.
            travelled = g["speed"] * elapsed - offset
            lat, lon = dest(g["lat"], g["lon"], g["bearing"], travelled)
            heading = g["bearing"]
        steps.append({
            "id": f"{marking.lower().replace('-', '_')}_{tick:02d}",
            "capturedAt": elapsed * 1000,
            "protocol": "dis",
            "service": "EntityState",
            "method": "Send",
            "methodType": "Unary",
            "status": "OK",
            "responseBinary": pdu(entity, g["force"], g["etype"], marking,
                                  lat, lon, g["alt"], heading, g["speed"],
                                  elapsed * 1000),
            "metadata": {
                "multicast-group": "239.1.2.3",
                "port": "3000",
                "ttl": "1",
            },
        })

# Ordered by capture time so a replay interleaves the groups the way a
# real exercise would, rather than playing one entity to completion and
# then starting the next.
steps.sort(key=lambda s: (s["capturedAt"], s["id"]))

doc = {
    "id": "dis-exercise",
    "name": "dis multi-entity exercise",
    "description": (
        "Ten entities in four spatially separated groups, broadcast on "
        "239.1.2.3:3000 over two simulated minutes: Convoy Alpha (three tanks "
        "east along a road), Convoy Bravo (two trucks south), UAV Kite (orbiting "
        "at 1200 m over the Bay of Lübeck) and a tank engagement of two closing "
        "pairs (BLAU friendly, ROT opposing). Each PDU carries one entity, so "
        "the map's track grouping has to work across frames rather than within "
        "one — set 'Group by' to `marking` to separate them."
    ),
    "createdAt": "2026-09-05T00:00:00Z",
    "recordingFormatVersion": 1,
    "steps": steps,
}

out = os.path.join(
    r"C:\Projekte\Kuestenlogik\Bowire.Protocol.Dis", "samples",
    "exercise.bowire-recording.json")
with io.open(out, "w", encoding="utf-8", newline="\n") as fh:
    json.dump(doc, fh, indent=2, ensure_ascii=False)
    fh.write("\n")

print(f"{len(assignments)} entities x {TICKS} ticks = {len(steps)} steps -> {out}")

# The original single-entity convoy, regenerated. Its ECEF location was
# picked as round numbers rather than converted from a position, which
# put a ground convoy at 15.5 km altitude — visible the moment the map
# could plot it at all.
convoy_steps = []
for tick in range(3):
    lat, lon = dest(53.5511, 9.9937, 90.0, tick * 100.0)
    convoy_steps.append({
        "id": f"pdu_{tick:02d}",
        "capturedAt": tick * 1000,
        "protocol": "dis",
        "service": "EntityState",
        "method": "Send",
        "methodType": "Unary",
        "status": "OK",
        "responseBinary": pdu(1000, 1, TANK, "BOWLINE01", lat, lon, 12.0,
                              90.0, 11.0, tick * 1000),
        "metadata": {"multicast-group": "239.1.2.3", "port": "3000", "ttl": "1"},
    })

convoy = json.load(io.open(os.path.join(
    r"C:\Projekte\Kuestenlogik\Bowire.Protocol.Dis", "samples",
    "convoy.bowire-recording.json"), encoding="utf-8"))
convoy["description"] = (
    "Three Entity State PDUs broadcast on 239.1.2.3:3000, stepping an M1 tank "
    "~100m/update eastward near Hamburg at 12 m altitude. The minimal DIS "
    "sample: one entity, one track. See exercise.bowire-recording.json for the "
    "multi-entity version.")
convoy["steps"] = convoy_steps
convoy_out = os.path.join(
    r"C:\Projekte\Kuestenlogik\Bowire.Protocol.Dis", "samples",
    "convoy.bowire-recording.json")
with io.open(convoy_out, "w", encoding="utf-8", newline="\n") as fh:
    json.dump(convoy, fh, indent=2, ensure_ascii=False)
    fh.write("\n")
print(f"convoy regenerated at a ground altitude -> {convoy_out}")
