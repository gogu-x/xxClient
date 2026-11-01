"""Read-only PNG pixel audit; emits rectangle metadata and JSON, never PNGs."""
from pathlib import Path
import importlib.util
import json
import hashlib

ROOT = Path(__file__).resolve().parents[1]
HELPER = Path(__file__).resolve().with_name('png_audit.py')
spec = importlib.util.spec_from_file_location('quietink_png_audit', HELPER)
png = importlib.util.module_from_spec(spec)
spec.loader.exec_module(png)

SOURCES = Path(r'C:\Users\25348\.codex\generated_images\01a195ee-530e-7e71-9392-5d802aec6fcd')
PLANS = [
    ('nav-six.png', 'exec-91dcf93d-f7dc-4801-91ff-4752822ad997.png',
     ['nav_character', 'nav_cultivation', 'nav_realm', 'nav_sect', 'nav_inventory', 'nav_souljade'],
     [(215, 418), (626, 420), (1030, 445), (216, 880), (626, 896), (1050, 900)]),
    ('map-overlays.png', 'exec-6e4751f7-6cae-4e7d-81e7-e11cd4539a95.png',
     ['nameplate_vertical', 'selection_halo', 'fog_ribbon', 'fog_patch', 'map_lock', 'map_locate'],
     [(263, 294), (767, 307), (1250, 337), (300, 775), (770, 777), (1280, 773)])
]


def audit():
    sprites = []
    atlases = []
    for filename, source_name, names, centers in PLANS:
        path = ROOT / 'Assets' / filename
        source = SOURCES / source_name
        source_available = source.is_file()
        source_identical = path.read_bytes() == source.read_bytes() if source_available else None
        if source_available:
            assert source_identical, 'PNG pixels/bytes changed'
        width, height, pixels = png.png_rgba(path)
        alpha = pixels[3::4]
        components = png.components(width, height, alpha, threshold=16)
        groups = [[] for _ in names]
        for component in components:
            if component['pixels'] < 20:
                continue
            cx = component['x'] + component['width'] / 2
            cy = component['y'] + component['height'] / 2
            index = min(range(len(names)), key=lambda i: (cx-centers[i][0])**2+(cy-centers[i][1])**2)
            groups[index].append(component)
        local = []
        for name, group in zip(names, groups):
            assert group, name
            x0 = max(0, min(c['x'] for c in group)-4)
            y0 = max(0, min(c['y'] for c in group)-4)
            x1 = min(width, max(c['x']+c['width'] for c in group)+4)
            y1 = min(height, max(c['y']+c['height'] for c in group)+4)
            item = {'atlas': filename, 'name': name, 'x': x0, 'y': y0,
                    'width': x1-x0, 'height': y1-y0, 'pivot': [0.5, 0.5],
                    'proposed_border_lbrt': [22,42,22,42] if name == 'nameplate_vertical' else [0,0,0,0],
                    'components': len(group), 'padding': 4,
                    'measured_alpha_threshold': 16}
            if name == 'nameplate_vertical':
                item['nine_slice_note'] = 'Preserves clipped cap and fine outline; intended labels 3-5 vertical characters.'
            local.append(item)
        for i, first in enumerate(local):
            for second in local[i+1:]:
                assert first['x']+first['width'] <= second['x'] or second['x']+second['width'] <= first['x'] or first['y']+first['height'] <= second['y'] or second['y']+second['height'] <= first['y'], (first['name'], second['name'])
        center_alpha = {}
        if filename == 'map-overlays.png':
            center_alpha = {'selection_halo': int(alpha[307*width+767]),
                            'map_locate_open_quadrant': int(alpha[730*width+1240])}
        metadata = {'file': filename, 'source': str(source), 'width': width, 'height': height,
                    'format': 'RGBA8', 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
                    'alpha_min': min(alpha), 'alpha_max': max(alpha),
                    'fully_transparent_percent': round(100*alpha.count(0)/len(alpha), 3),
                    'corner_alpha': [int(alpha[i]) for i in [0, width-1, (height-1)*width, len(alpha)-1]],
                    'discarded_isolated_component_pixels': sum(c['pixels'] for c in components if c['pixels'] < 20),
                    'alpha_probe': center_alpha,
                    'source_available': source_available, 'source_identical': source_identical,
                    'source_bytes_identical': source_identical, 'sprite_rectangles_nonoverlapping': True}
        sprites.extend(local)
        atlases.append(metadata)
    result = {'schema_version': 1, 'coordinate_origin': 'top-left', 'rect_units': 'pixels',
              'generated_with': 'built-in image_gen transparent_background=true',
              'sprite_count': len(sprites), 'pixel_changes': False,
              'slice_policy': 'Actual connected alpha>=16 components >=20px, grouped semantically and padded4px; never equal-grid slicing.',
              'visual_review': 'All twelve semantic elements inspected. Rejected first two overlays for broad ring/overdecoration. Final halo is thin; fog keeps natural alpha. Runtime white/dark composition and nine-slice review still required.',
              'atlases': atlases, 'sprites': sprites}
    (ROOT / 'ui-layout.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    (ROOT / 'Verification' / 'ui-alpha-audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    audit()
