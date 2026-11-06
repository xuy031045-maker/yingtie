"""Create YingTie's original vector mark and compact Windows icon (Pillow)."""
from pathlib import Path
from PIL import Image, ImageDraw
import io
import struct

root = Path(__file__).resolve().parent / 'assets'
root.mkdir(exist_ok=True)
svg = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128">
<rect x="4" y="4" width="120" height="120" rx="28" fill="#2864d8"/>
<path d="M31 54V34a4 4 0 0 1 4-4h20M74 30h19a4 4 0 0 1 4 4v19M31 74v19a4 4 0 0 0 4 4h19" fill="none" stroke="#a9dfff" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>
<rect x="44" y="48" width="58" height="58" rx="10" fill="#123e9a" opacity=".28"/>
<path d="M49 43h39l17 17v31a10 10 0 0 1-10 10H49a10 10 0 0 1-10-10V53a10 10 0 0 1 10-10Z" fill="#fff"/>
<path d="M88 43v12a5 5 0 0 0 5 5h12Z" fill="#73dce0"/>
</svg>'''
(root / 'logo.svg').write_text(svg, encoding='utf-8')

scale = 8
im = Image.new('RGBA', (128*scale, 128*scale))
d = ImageDraw.Draw(im)
def box(x): return tuple(round(v*scale) for v in x)
def line(points):
    p = [(x*scale,y*scale) for x,y in points]
    d.line(p, fill='#a9dfff', width=7*scale, joint='curve')
    for x,y in [points[0], points[-1]]:
        d.ellipse(box((x-3.5,y-3.5,x+3.5,y+3.5)), fill='#a9dfff')
d.rounded_rectangle(box((4,4,124,124)), radius=28*scale, fill='#2864d8')
line([(31,54),(31,34),(35,30),(55,30)])
line([(74,30),(93,30),(97,34),(97,53)])
line([(31,74),(31,93),(35,97),(54,97)])
d.rounded_rectangle(box((44,48,102,106)), radius=10*scale, fill='#2259c7')
d.rounded_rectangle(box((39,43,105,101)), radius=10*scale, fill='white')
d.polygon([(88*scale,43*scale),(105*scale,43*scale),(105*scale,60*scale)], fill='#2864d8')
d.polygon([(88*scale,43*scale),(88*scale,60*scale),(105*scale,60*scale)], fill='#73dce0')
logo = im.resize((512,512), Image.Resampling.LANCZOS)
logo.quantize(colors=128,method=Image.Quantize.FASTOCTREE).save(root/'logo.png', optimize=True)
# Palette PNG entries preserve transparency and keep the embedded icon small.
frames=[]
for size in [16,24,32,48,256]:
    frame=im.resize((size,size),Image.Resampling.LANCZOS)
    output=io.BytesIO()
    frame.quantize(colors=64,method=Image.Quantize.FASTOCTREE).save(output,format='PNG',optimize=True)
    frames.append((size,output.getvalue()))
header=struct.pack('<HHH',0,1,len(frames)); offset=6+16*len(frames); entries=[]
for size,data in frames:
    entries.append(struct.pack('<BBBBHHII',size%256,size%256,0,0,1,32,len(data),offset)); offset+=len(data)
(root/'app.ico').write_bytes(header+b''.join(entries)+b''.join(data for size,data in frames))
print([(p.name,p.stat().st_size) for p in root.iterdir()])
