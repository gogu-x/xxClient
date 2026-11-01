"""Read RGBA PNG only and write sprite metadata. Never changes image pixels."""
from pathlib import Path
import importlib.util
import json
import hashlib
import argparse

ROOT = Path(__file__).resolve().parents[1]
utility = Path(__file__).resolve().parent / 'png_audit.py'
spec = importlib.util.spec_from_file_location('png_audit', utility)
reader = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reader)

NAMES = [
    ('entrance-qingyun-gate', '青云古门', 'large', 320),
    ('entrance-cloud-palace', '浮空仙宫', 'large', 340),
    ('entrance-ice-cave', '冰雪洞天', 'large', 330),
    ('entrance-sword-ruins', '巨剑遗墟', 'large', 340),
    ('entrance-autumn-gate', '秋林山门', 'medium', 230),
    ('entrance-bamboo-grotto', '竹林洞府', 'medium', 220),
    ('entrance-lake-pavilion', '湖心仙阁', 'medium', 220),
    ('entrance-cliff-grotto', '悬崖石窟', 'medium', 230),
    ('entrance-ancient-well', '古井阵眼', 'small', 130),
    ('entrance-stone-door', '小型石门', 'small', 140),
    ('entrance-tree-shrine', '树下小祠', 'small', 155),
    ('entrance-small-cave', '小型洞口', 'small', 140),
]

def audit(write_metadata=False):
    path = ROOT / 'Assets/realm-entrances.png'
    original_layout_path = ROOT / 'entrances-layout.json'
    original_layout = json.loads(original_layout_path.read_text(encoding='utf-8-sig')) if original_layout_path.exists() else {}
    original_report = next((a for a in original_layout.get('atlases', []) if a.get('file') == path.name), {})
    source_text = original_report.get('source', 'C:/Users/25348/.codex/generated_images/01a195ed-e4a1-7100-bd11-05e3e51fd61a/exec-49bc252d-8c94-4681-a2c0-ac2c22f89f4f.png')
    source = Path(source_text)
    expected_hash = original_report.get('sha256', '5919558fc0b16b5b3cb283080100a3f2e953d7d978d0e29c17d0b16b5e450c94')
    asset_bytes = path.read_bytes()
    actual_hash = hashlib.sha256(asset_bytes).hexdigest()
    assert actual_hash == expected_hash, 'Asset hash differs from the production evidence; metadata was not overwritten.'
    source_available = source.is_file()
    source_identical = asset_bytes == source.read_bytes() if source_available else None
    width, height, rgba = reader.png_rgba(path)
    alpha = rgba[3::4]
    components = reader.components(width, height, alpha, 16)
    groups = [[] for _ in NAMES]
    for component in components:
        if component['pixels'] < 20:
            continue
        cx = component['x'] + component['width'] / 2
        cy = component['y'] + component['height'] / 2
        index = min(range(12), key=lambda i: ((cx - (i % 3 + .5) * width / 3) / width) ** 2 +
                    ((cy - (i // 3 + .5) * height / 4) / height) ** 2)
        groups[index].append(component)
    sprites = []
    for index, (name, label, tier, suggested_height) in enumerate(NAMES):
        group = groups[index]
        assert group, name
        x0 = max(0, min(c['x'] for c in group) - 4)
        y0 = max(0, min(c['y'] for c in group) - 4)
        x1 = min(width, max(c['x'] + c['width'] for c in group) + 4)
        y1 = min(height, max(c['y'] + c['height'] for c in group) + 4)
        sprites.append(dict(atlas=path.name, name=name, display_name=label, size_tier=tier,
                            x=x0, y=y0, width=x1-x0, height=y1-y0,
                            pivot=[.5, 0], proposed_border_lbrt=[0, 0, 0, 0],
                            suggestedWorldSize=[round(suggested_height*(x1-x0)/(y1-y0)), suggested_height],
                            row=index//3+1, column=index%3+1,
                            component_count=len(group), solid_pixels=sum(c['pixels'] for c in group)))
    overlaps = []
    for i, a in enumerate(sprites):
        for b in sprites[i+1:]:
            if max(a['x'],b['x']) < min(a['x']+a['width'],b['x']+b['width']) and max(a['y'],b['y']) < min(a['y']+a['height'],b['y']+b['height']):
                overlaps.append([a['name'], b['name']])
    nonzero_margin = [
        min(i % width for i,v in enumerate(alpha) if v >= 16),
        min(i // width for i,v in enumerate(alpha) if v >= 16),
        width-1-max(i % width for i,v in enumerate(alpha) if v >= 16),
        height-1-max(i // width for i,v in enumerate(alpha) if v >= 16)
    ]
    report = dict(file=path.name, width=width, height=height, format='RGBA8', alpha_min=min(alpha), alpha_max=max(alpha),
                  fully_transparent_percent=round(alpha.count(0)*100/len(alpha),3),
                  alpha_below16_percent=round(sum(v<16 for v in alpha)*100/len(alpha),3),
                  outer_content_margin_ltrb=nonzero_margin, components_alpha_ge16=len(components),
                  significant_components=sum(c['pixels']>=20 for c in components),
                  excluded_tiny_component_pixels=sum(c['pixels'] for c in components if c['pixels']<20),
                  rect_overlaps=overlaps, source=source_text,
                  sha256=actual_hash, expected_sha256=expected_hash,
                  expected_sha256_matches=actual_hash == expected_hash,
                  source_available=source_available, source_identical=source_identical,
                  pixel_edits=False,
                  validation_scope='Source pixels, alpha and sprite metadata only; Unity composition requires separate visual review.')
    doc = dict(schema_version=1, coordinate_origin='top-left', rect_units='pixels', atlas_root='Assets',
               suggested_world_reference_size=[3840,2560],
               generation='built-in image_gen, true transparent_background; one cleanup edit for spacing',
               rect_method='alpha>=16 connected components of at least20 pixels, assigned by visual semantic cell, bounds padded4px; no equal-grid cropping',
               pixel_edits=False, atlases=[report], sprites=sprites)
    assert not overlaps, overlaps
    assert source_identical is not False, 'Available original source differs; metadata was not overwritten.'
    # Preserve the original source/hash provenance unless metadata regeneration was explicitly requested.
    if write_metadata:
        if original_report:
            doc['production_provenance'] = original_layout.get('production_provenance', original_report)
        original_layout_path.write_text(json.dumps(doc,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    (ROOT/'Verification/entrances-alpha-report.current.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(report, ensure_ascii=False, indent=2))
    print(json.dumps([{k:s[k] for k in ('name','x','y','width','height','suggestedWorldSize')} for s in sprites],ensure_ascii=False,indent=2))

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write-metadata', action='store_true', help='Explicitly regenerate slice metadata after hash checks; original provenance is retained.')
    audit(parser.parse_args().write_metadata)
