"""Validate and package the art; never modify PNG pixels. Python standard library only."""
from pathlib import Path
import json
import hashlib
import struct
import zipfile
import html

ROOT = Path(__file__).resolve().parents[1]
METADATA = ('entrances-layout.json', 'ui-layout.json', 'lobby-support-layout.json')
VERIFIED_SCREENSHOTS = {'01-lobby-live.png', '11-realm-status-verified.png'}

def read_json(name):
    return json.loads((ROOT / name).read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def build():
    files = sorted((ROOT / 'Assets').glob('*.png'))
    images = {}
    for path in files:
        data = path.read_bytes()
        assert data[:8] == b'\x89PNG\r\n\x1a\n', path
        width, height, depth, color = struct.unpack_from('>IIBB', data, 16)
        assert depth == 8 and color in (2, 6), (path, depth, color)
        images[path.name] = dict(file='Assets/' + path.name, width=width, height=height,
            color_type=color, bytes=len(data), sha256=sha(path))
    sprites, atlases = {}, {}
    for filename in METADATA:
        doc = read_json(filename)
        assert doc['coordinate_origin'] == 'top-left'
        for sprite in doc['sprites']:
            assert sprite['name'] not in sprites, sprite['name']
            image = images[sprite['atlas']]
            x,y,w,h = [sprite[k] for k in ('x','y','width','height')]
            assert all(isinstance(v, (int,float)) and int(v)==v for v in (x,y,w,h))
            assert x>=0 and y>=0 and w>0 and h>0 and x+w<=image['width'] and y+h<=image['height'], sprite['name']
            border = sprite.get('proposed_border_lbrt', [0]*4)
            assert len(border)==4 and min(border)>=0 and border[0]+border[2]<w and border[1]+border[3]<h
            for other in atlases.setdefault(sprite['atlas'], []):
                assert not (max(x,other['x'])<min(x+w,other['x']+other['width']) and
                            max(y,other['y'])<min(y+h,other['y']+other['height'])), (sprite['name'],other['name'])
            atlases[sprite['atlas']].append(sprite)
            sprites[sprite['name']] = dict(sprite, metadata=filename)
    for name, image in images.items():
        if name not in atlases:
            key = Path(name).stem
            assert key not in sprites
            sprites[key] = dict(name=key,atlas=name,x=0,y=0,width=image['width'],height=image['height'],standalone=True)
    world = read_json('world-layout.json')
    assert len(world['nodes'])==24
    assert images['world-map-base.png']['width']==world['source_texture_width']
    assert images['world-map-base.png']['height']==world['source_texture_height']
    ids = set()
    for node in world['nodes']:
        assert node['id'] not in ids
        ids.add(node['id'])
        assert node['art'] in sprites, node['art']
        assert 0 <= node['x'] <= 1 and 0 <= node['y'] <= 1
        px,py = node['x']*world['map_width'],node['y']*world['map_height']
        assert px-node['width']/2>=0 and px+node['width']/2<=world['map_width']
        assert py-node['height']>=0 and py<=world['map_height']
    lobby = read_json('lobby-layout.json')
    for layer in lobby['layers']:
        assert layer['asset'] in sprites, layer['asset']
        assert layer['x']>=0 and layer['y']>=0
        assert layer['x']+layer['width']<=lobby['design_width']+.01
        assert layer['y']+layer['height']<=lobby['design_height']+.01
    report = dict(png_count=len(images),atlas_count=len(atlases),sprite_count=len(sprites),
        entrance_types=12,map_entrances=len(world['nodes']),persistent_map_labels=sum(n['show_label'] for n in world['nodes']),
        lobby_layers=len(lobby['layers']),lobby_texts=len(lobby['texts']),
        missing_references=[],overlapping_slices=[],out_of_bounds_slices=[],pixel_edits=False,
        unity_import_executed_by_this_check=False,unity_visual_review_executed_by_this_check=False,
        unity_import_report_present=(ROOT/'unity-import-report.txt').exists(),
        actual_world_texture=[images['world-map-base.png']['width'],images['world-map-base.png']['height']],
        coordinate_domain=[world['map_width'],world['map_height']],
        note='Coordinate domain is not source texture resolution. No 4K or pixel-exact runtime fidelity claim.')
    (ROOT/'Verification/package-check.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    manifest = dict(package='RealmMapV1',version=1,generated_with='built-in image_gen; preserved source PNG bytes',
        status='runtime synchronized with g1Client; lobby-only bottom navigation; qingyun opens the existing current trial; 23 other gates closed',summary=report,images=list(images.values()),sprites=list(sprites.values()))
    (ROOT/'asset-manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    cards = []
    for sprite in sprites.values():
        iw,ih = images[sprite['atlas']]['width'],images[sprite['atlas']]['height']
        scale=min(180/sprite['width'],180/sprite['height'])
        style='width:{:.2f}px;height:{:.2f}px;background-image:url(Assets/{});background-size:{:.2f}px {:.2f}px;background-position:{:.2f}px {:.2f}px'.format(
            sprite['width']*scale,sprite['height']*scale,sprite['atlas'],iw*scale,ih*scale,-sprite['x']*scale,-sprite['y']*scale)
        cards.append('<article><div class="preview"><div style="'+style+'"></div></div><b>'+html.escape(sprite['name'])+'</b><small>'+str(sprite['width'])+' × '+str(sprite['height'])+'</small></article>')
    catalog='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>大厅与山海秘境资源目录</title><style>
body{margin:32px;background:#f4f0e5;color:#25332e;font:16px system-ui}h1{font-size:28px}p{line-height:1.7;max-width:950px}button{padding:10px 18px;border:1px solid #bba678;background:#fff9eb;border-radius:6px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:20px;margin-top:20px}article{background:#fffaf0;border:1px solid #d8cfbd;padding:16px;border-radius:8px}b,small{display:block;margin-top:10px;overflow-wrap:anywhere}small{color:#68736a}.preview{height:190px;display:flex;align-items:center;justify-content:center;background-color:#eee;background-image:linear-gradient(45deg,#d7d7d7 25%,transparent 25%),linear-gradient(-45deg,#d7d7d7 25%,transparent 25%),linear-gradient(45deg,transparent 75%,#d7d7d7 75%),linear-gradient(-45deg,transparent 75%,#d7d7d7 75%);background-size:20px 20px;background-position:0 0,0 10px,10px -10px,-10px 0}.preview>div{background-repeat:no-repeat}.dark .preview{background:#202a2e}.paper .preview{background:#f4f0e5}</style>
<h1>大厅与山海秘境 · 可组合资源目录</h1><p>原始 PNG + 实际透明切片。本页只展示资源，不模拟游戏业务。透明背景可切换检查。地图底图无地名与入口，入口和文字通过布局数据独立摆放。仅大厅保留底部菜单；青云古境入口复用已有当前试炼流程，其余 23 处未开放。地图显示各入口状态；点击入口图片轻浮一次，不出现选中光圈。Unity 导入报告与运行截图见 Verification 及资源包根目录；本打包脚本不执行运行验收。</p>
<button onclick="document.body.className=''">透明网格</button> <button onclick="document.body.className='paper'">米纸背景</button> <button onclick="document.body.className='dark'">深色背景</button><main>'''+''.join(cards)+'</main></html>'
    (ROOT/'资源目录.html').write_text(catalog,encoding='utf8')
    archive=ROOT.parent/'RealmMapV1-resource-pack.zip'
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as bundle:
        for path in sorted(ROOT.rglob('*')):
            if path.parent.name == 'Verification' and path.suffix.lower() == '.png' and path.name not in VERIFIED_SCREENSHOTS:
                continue  # Keep intermediate diagnostic captures in the project, not the deliverable.
            if path.is_file() and '__pycache__' not in path.parts and path.suffix not in ('.pyc','.zip'):
                bundle.write(path,Path(ROOT.name)/path.relative_to(ROOT))
    print(json.dumps(dict(report,zip=str(archive),zip_bytes=archive.stat().st_size),ensure_ascii=False,indent=2))

if __name__=='__main__':
    build()
