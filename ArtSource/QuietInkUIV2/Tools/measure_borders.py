"""Measure slice corners from PNG alpha. Only JSON/verification metadata is written."""
import hashlib
import json
import statistics
from audit_atlases import ROOT, png_rgba

path = ROOT / "atlas-layout.json"
document = json.loads(path.read_text(encoding="utf-8"))
width, height, pixels = png_rgba(ROOT / "Assets/ui-controls.png")
non_sliced = {"avatar_frame", "skill_frame", "souljade_socket", "divider", "tab_underline"}
measurements = []


def stable_edges(spans):
    size = len(spans)
    middle = [span for span in spans[size // 3:2 * size // 3] if span]
    low = statistics.median(span[0] for span in middle)
    high = statistics.median(span[1] for span in middle)

    def straight(span):
        return span is not None and abs(span[0] - low) <= 1 and abs(span[1] - high) <= 1

    first = next(i for i in range(size // 2) if all(straight(span) for span in spans[i:i + 3]))
    last = next(i for i in range(size - 1, size // 2, -1) if all(straight(span) for span in spans[i - 2:i + 1]))
    return first + 4, size - 1 - last + 4, {"first_straight": first, "last_straight": last,
                                               "middle_outer_low": low, "middle_outer_high": high}


for sprite in document["sprites"]:
    sprite["proposed_border_lbrt"] = [0, 0, 0, 0]
    sprite["nine_slice_ready"] = False
    if sprite["atlas"] != "ui-controls.png":
        continue
    name = sprite["name"]
    if name in non_sliced:
        sprite["border_validation"] = {"status": "not_applicable", "reason": "Circular geometry or center diamond; preserve aspect instead of nine-slice stretching."}
        continue
    sw, sh = sprite["width"], sprite["height"]
    mask = [[pixels[((sprite["y"] + y) * width + sprite["x"] + x) * 4 + 3] >= 192
             for x in range(sw)] for y in range(sh)]
    rows = [(min(xs), max(xs)) if (xs := [x for x in range(sw) if mask[y][x]]) else None for y in range(sh)]
    cols = [(min(ys), max(ys)) if (ys := [y for y in range(sh) if mask[y][x]]) else None for x in range(sw)]
    top, bottom, row_geometry = stable_edges(rows)
    left, right, column_geometry = stable_edges(cols)
    assert left + right < sw and top + bottom < sh
    borders = [left, bottom, right, top]
    sprite["proposed_border_lbrt"] = borders
    sprite["border_validation"] = {
        "status": "source_geometry_validated_runtime_pending",
        "method": "At alpha>=192 find the first/last three consecutive straight-edge samples within 1px of the middle-third median, then preserve four additional pixels for inset paint and antialiasing.",
        "alpha_threshold": 192, "inset_safety_pixels": 4,
        "row_geometry": row_geometry, "column_geometry": column_geometry,
        "stretchable_center": {"width": sw - left - right, "height": sh - top - bottom},
    }
    measurements.append({"name": name, "rect": [sw, sh], "border_lbrt": borders,
                         "row_geometry": row_geometry, "column_geometry": column_geometry,
                         "center": [sw - left - right, sh - top - bottom]})

document["border_audit"] = {
    "source": "Assets/ui-controls.png", "source_sha256": hashlib.sha256((ROOT / "Assets/ui-controls.png").read_bytes()).hexdigest(),
    "method": "Actual V2 alpha silhouettes; not copied from V1 borders. Four-pixel inset safety after measured corner-to-straight transitions.",
    "rectangular_controls": len(measurements), "border_order": "left,bottom,right,top",
    "runtime_nine_slice_review": "pending", "pixel_changes": False,
}
path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
verification = ROOT / "Verification"
verification.mkdir(exist_ok=True)
(verification / "border-geometry.json").write_text(json.dumps(measurements, indent=2) + "\n", encoding="utf-8")
print(json.dumps(measurements, indent=2))
