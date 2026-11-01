"""Read-only RGBA PNG audit. Writes JSON metadata only; never modifies pixels."""
from pathlib import Path
import json
import struct
import zlib
from collections import deque
import hashlib

ROOT = Path(__file__).resolve().parents[1]
V1 = ROOT.parent / "QuietInkUIV1"


def png_rgba(path):
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    offset = 8
    compressed = bytearray()
    while offset < len(data):
        size = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        chunk = data[offset + 8:offset + 8 + size]
        if kind == b"IHDR":
            width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            assert (depth, color, interlace) == (8, 6, 0), (path, depth, color, interlace)
        elif kind == b"IDAT":
            compressed.extend(chunk)
        offset += size + 12
    raw = zlib.decompress(compressed)
    stride = width * 4
    pixels = bytearray(width * height * 4)
    previous = bytearray(stride)
    for y in range(height):
        start = y * (stride + 1)
        mode = raw[start]
        row = bytearray(raw[start + 1:start + 1 + stride])
        for x in range(stride):
            a = row[x - 4] if x >= 4 else 0
            b = previous[x]
            c = previous[x - 4] if x >= 4 else 0
            if mode == 1:
                predict = a
            elif mode == 2:
                predict = b
            elif mode == 3:
                predict = (a + b) // 2
            elif mode == 4:
                p = a + b - c
                da, db, dc = abs(p - a), abs(p - b), abs(p - c)
                predict = a if da <= db and da <= dc else b if db <= dc else c
            else:
                assert mode == 0
                predict = 0
            row[x] = (row[x] + predict) & 255
        pixels[y * stride:(y + 1) * stride] = row
        previous = row
    return width, height, pixels


def components(width, height, alpha, threshold=16):
    seen = bytearray(len(alpha))
    result = []
    for seed, value in enumerate(alpha):
        if value < threshold or seen[seed]:
            continue
        seen[seed] = 1
        queue = deque([seed])
        x0 = x1 = seed % width
        y0 = y1 = seed // width
        count = 0
        while queue:
            pixel = queue.popleft()
            x, y = pixel % width, pixel // width
            x0, x1 = min(x0, x), max(x1, x)
            y0, y1 = min(y0, y), max(y1, y)
            count += 1
            for adjacent in (pixel - 1 if x else -1,
                             pixel + 1 if x + 1 < width else -1,
                             pixel - width if y else -1,
                             pixel + width if y + 1 < height else -1):
                if adjacent >= 0 and not seen[adjacent] and alpha[adjacent] >= threshold:
                    seen[adjacent] = 1
                    queue.append(adjacent)
        result.append({"x": x0, "y": y0, "width": x1 - x0 + 1,
                       "height": y1 - y0 + 1, "pixels": count})
    return result


def original_sprites():
    base = json.loads((V1 / "atlas-layout.json").read_text(encoding="utf-8-sig"))["sprites"]
    items = json.loads((V1 / "item-atlas-layout.json").read_text(encoding="utf-8-sig"))["items"]
    for item in items:
        item["row"] += 1
        item["column"] += 1
    extra_names = ("skill_jade", "skill_blue", "skill_wind", "skill_fire",
                   "currency_crystal", "currency_coin", "talent_node", "notification_dot")
    extras = [{"atlas": "icons-extra.png", "name": name,
               "row": index // 4 + 1, "column": index % 4 + 1,
               "x": (index % 4) * 313.5, "y": (index // 4) * 627,
               "width": 313.5, "height": 627} for index, name in enumerate(extra_names)]
    illustration_names = ("soul_ring", "result_emblem", "task_gate", "task_furnace",
                          "task_scripture", "inventory_empty", "talent_mountain", "cultivation_seal")
    illustrations = [{"atlas": "illustrations.png", "name": name,
                      "row": index // 4 + 1, "column": index % 4 + 1,
                      "x": (index % 4) * 313.5, "y": (index // 4) * 627,
                      "width": 313.5, "height": 627} for index, name in enumerate(illustration_names)]
    settings_names = ("audio_volume", "audio_mute", "quality_landscape", "control_gamepad")
    settings = [{"atlas": "icons-settings.png", "name": name,
                 "row": index // 2 + 1, "column": index % 2 + 1,
                 "x": (index % 2) * 627, "y": (index // 2) * 627,
                 "width": 627, "height": 627} for index, name in enumerate(settings_names)]
    return base + items + extras + illustrations + settings


def audit():
    previous_path = ROOT / "atlas-layout.json"
    previous = json.loads(previous_path.read_text(encoding="utf-8")) if previous_path.exists() else {}
    previous_sprites = {(s["atlas"], s["name"]): s for s in previous.get("sprites", [])}
    sprites = original_sprites()
    atlases = []
    output = []
    sources = {
        "icons-navigation.png": "exec-b1ccfdcc-ac24-4b9f-a756-602a7fe227ca.png",
        "icons-items.png": "exec-08b7c154-9b2d-494a-a9c8-f17fddfa6446.png",
        "ui-controls.png": "exec-0fe69ad4-2384-4a9a-96d6-b492ea308991.png",
        "icons-extra.png": "exec-cdc43364-9775-4279-aa4b-b81dff238240.png",
        "illustrations.png": "exec-c9fcc04c-da95-4b78-a6a8-3d1dc414e605.png",
        "icons-settings.png": "exec-e5c58a7f-9d20-42d2-91bb-dfc5ab137685.png",
    }
    for filename in sources:
        width, height, pixels = png_rgba(ROOT / "Assets" / filename)
        alpha = pixels[3::4]
        comps = components(width, height, alpha)
        known = [s for s in sprites if s["atlas"] == filename]
        groups = [[] for _ in known]
        for component in comps:
            if component["pixels"] < 20:
                continue
            cx = component["x"] + component["width"] / 2
            cy = component["y"] + component["height"] / 2
            def score(s):
                sx = (s["x"] + s["width"] / 2) * width / 1254
                sy = (s["y"] + s["height"] / 2) * height / 1254
                return (cx - sx) ** 2 + (cy - sy) ** 2
            index = min(range(len(known)), key=lambda i: score(known[i]))
            groups[index].append(component)
        metadata = {"file": filename, "width": width, "height": height,
                    "format": "RGBA8", "alpha_min": min(alpha), "alpha_max": max(alpha),
                    "fully_transparent_percent": round(100 * alpha.count(0) / len(alpha), 3),
                    "components_alpha_ge16": len(comps),
                    "components_at_least20_pixels": sum(c["pixels"] >= 20 for c in comps),
                    "isolated_small_component_pixels": sum(c["pixels"] for c in comps if c["pixels"] < 20),
                    "sha256": hashlib.sha256((ROOT / "Assets" / filename).read_bytes()).hexdigest(),
                    "source": "C:/Users/25348/.codex/generated_images/01a194cd-6af1-7241-8c1c-471cbb59493e/" + sources[filename],
                    "slice_policy": "Use the measured individual sprite rectangles; never equal-grid slicing"}
        for source, group in zip(known, groups):
            assert group, (filename, source["name"])
            x = min(c["x"] for c in group)
            y = min(c["y"] for c in group)
            right = max(c["x"] + c["width"] for c in group)
            bottom = max(c["y"] + c["height"] for c in group)
            content = {"x": x, "y": y, "width": right - x, "height": bottom - y}
            pad = 4
            left, top = max(0, x - pad), max(0, y - pad)
            right, bottom = min(width, right + pad), min(height, bottom + pad)
            item = {"atlas": filename, "name": source["name"],
                    "row": source["row"], "column": source["column"],
                    "x": left, "y": top, "width": right - left, "height": bottom - top,
                    "contentRect": content, "components": len(group),
                    "status": "alpha_measured_and_source_visually_reviewed",
                    "nine_slice_ready": False}
            if "label" in source:
                item["label"] = source["label"]
            prior = previous_sprites.get((filename, source["name"]), {})
            if all(prior.get(field) == item[field] for field in ("x", "y", "width", "height")):
                for field in ("proposed_border_lbrt", "border_validation"):
                    if field in prior:
                        item[field] = prior[field]
            output.append(item)
            if source["name"] in ("avatar_frame", "skill_frame", "currency_coin", "soul_ring", "result_emblem", "cultivation_seal"):
                center_x, center_y = (left + right) // 2, (top + bottom) // 2
                item["center_alpha"] = alpha[center_y * width + center_x]
                assert item["center_alpha"] == 0, (filename, source["name"], "center not transparent")
        for first in output:
            if first["atlas"] != filename:
                continue
            for second in output:
                if second["atlas"] != filename or first["name"] >= second["name"]:
                    continue
                overlap = (max(first["x"], second["x"]) < min(first["x"] + first["width"], second["x"] + second["width"])
                           and max(first["y"], second["y"]) < min(first["y"] + first["height"], second["y"] + second["height"]))
                assert not overlap, (filename, first["name"], second["name"])
        atlases.append(metadata)
    singles = []
    for filename, source in {
        "fx-slash.png": "exec-a9486934-209a-4f85-a97f-1987caaa86ed.png",
        "fx-impact.png": "exec-15fb18eb-f056-4f9c-9c12-8d6f77e3505d.png",
        "fx-ring.png": "exec-8d031ae4-15a9-41c8-8ea7-bc5c072971d6.png",
    }.items():
        width, height, pixels = png_rgba(ROOT / "Assets" / filename)
        alpha = pixels[3::4]
        comps = [c for c in components(width, height, alpha) if c["pixels"] >= 20]
        left, top = min(c["x"] for c in comps), min(c["y"] for c in comps)
        right = max(c["x"] + c["width"] for c in comps)
        bottom = max(c["y"] + c["height"] for c in comps)
        entry = {"file": filename, "resource_key": filename[:-4], "width": width, "height": height,
                 "format": "RGBA8", "alpha_min": min(alpha), "alpha_max": max(alpha),
                 "fully_transparent_percent": round(100 * alpha.count(0) / len(alpha), 3),
                 "contentRect": {"x": left, "y": top, "width": right - left, "height": bottom - top},
                 "source": "C:/Users/25348/.codex/generated_images/01a194cd-6af1-7241-8c1c-471cbb59493e/" + source,
                 "sha256": hashlib.sha256((ROOT / "Assets" / filename).read_bytes()).hexdigest(),
                 "import_mode": "Single sprite; preserve full canvas and true alpha", "pixel_changes": False}
        if filename == "fx-ring.png":
            entry["center_alpha"] = alpha[(height // 2) * width + width // 2]
            assert entry["center_alpha"] == 0
        singles.append(entry)
    document = {"schema_version": 1, "title": "QuietInk UI V2 color atlas actual sprite bounds",
                "coordinate_origin": "top-left", "rect_units": "pixels", "atlas_root": "Assets",
                "generated_with": "built-in image_gen; transparent_background=true",
                "status": "alpha_measured_and_source_visually_reviewed",
                "original_prompt_files": ["Prompts/icons-navigation.txt", "Prompts/icons-items.txt",
                                          "Prompts/ui-controls.txt", "Prompts/ui-controls-cleanup.txt", "Prompts/icons-extra.txt",
                                          "Prompts/illustrations.txt", "Prompts/illustrations-final-assembly.txt", "Prompts/icons-settings.txt",
                                          "Prompts/fx-slash.txt", "Prompts/fx-impact.txt", "Prompts/fx-ring.txt"],
                "generation_notes": ["The three V1 atlases were recolored via independent built-in image_gen edits, retaining each original semantic layout.",
                                     "UI controls received one tool-only alpha-edge cleanup pass. Extra skills/currency indicators were generated from the colored core-page concept.",
                                     "All final PNGs are byte-for-byte copies of built-in generated files. No Python or other raster-library pixel editing was performed.",
                                     "Illustrations and settings icons were reconstructed from the approved growth/services concepts; the illustration ring refinement was performed with image_gen only.",
                                     "Source-level visual review confirms semantic count/order, restrained differentiated color, and genuine transparent gaps. Pixel-exact geometry and Unity nine-slice rendering are not claimed.",
                                     "Illustration ink rings retain a broader brush band than the thin reference gesture and need runtime opacity/composition review; this is explicitly not a pixel-exact extraction."],
                "audit_method": {"decoder": "Python standard library struct/zlib read-only PNG decoder",
                                 "rect_detection": "Actual alpha >= 16 connected components, excluding isolated components below 20 pixels; grouped to original semantic sprite center, padded by 4 pixels. No equal-grid slicing.",
                                 "pixel_changes": False, "alpha_threshold": 16,
                                 "minimum_component_pixels": 20, "padding": 4,
                                 "unity_conversion": "unityY = atlasHeight - y - height",
                                 "caveat": "Transparent/fringe alpha below 16 and isolated <20 pixel specks are excluded from measured content, but unchanged in original PNG."},
                "atlases": atlases, "sprites": output, "single_assets": singles}
    if "border_audit" in previous:
        document["border_audit"] = previous["border_audit"]
    (ROOT / "atlas-layout.json").write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(atlases, indent=2))
    print("Measured", len(output), "non-overlapping sprite rects")
    print(json.dumps(singles, indent=2))


if __name__ == "__main__":
    audit()
