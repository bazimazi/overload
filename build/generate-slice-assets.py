"""Reproducible original vector art and synthesized audio. Python standard library only."""
from pathlib import Path
import math
import random
import struct
import wave

root = Path(__file__).resolve().parents[1] / 'src/Overload.Game/Assets'
root.mkdir(exist_ok=True)
outline = '#111c28'
def svg(name, body, width=48, height=64):
    (root / f'{name}.svg').write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" shape-rendering="crispEdges">{body}</svg>\n')

for name, cloth, plate, trim, helm in [
    ('warden', '#235461', '#a8ccd0', '#e8c486', '#476975'),
    ('pursuer', '#6f3346', '#a88288', '#df9b90', '#574452'),
    ('brute', '#654132', '#bba181', '#edc38d', '#705846')]:
    bulk = 3 if name == 'brute' else 0
    body = f'''<path d="M15 21 L8 48 17 53 22 49 30 54 39 49 32 21Z" fill="{outline}"/>
<path d="M16 23 L11 47 18 49 22 45 31 50 35 47 30 23Z" fill="{cloth}"/>
<path d="M16 42h7v14h-9v-5h2zm10 0h7v10h3v5H25Z" fill="{outline}"/>
<path d="M17 45h4v8h-5v-2h1zm10 0h4v8h3v2h-7Z" fill="{helm}"/>
<path d="M{13-bulk} 22h{22+bulk*2}v20H{13-bulk}Z" fill="{outline}"/>
<path d="M15 24h18v15H15Z" fill="{plate}"/><path d="M16 26h15v3H16zm3 7h11v4H19Z" fill="{helm}"/>
<path d="M21 24h3v15h-3Z" fill="{trim}"/><path d="M13 38h22v5H13Z" fill="{cloth}"/><path d="M21 38h6v4h-6Z" fill="{trim}"/>
<path d="M16 7h15l4 5v12H13V12Z" fill="{outline}"/><path d="M18 9h11l4 4v8H15v-8Z" fill="{plate}"/>
<path d="M17 14h14v5H17Z" fill="{outline}"/><path d="M18 15h11v1H18Z" fill="{trim}"/><path d="M23 9h3v13h-3Z" fill="{helm}"/>
<path d="M8 24h8v16H8Z" fill="{outline}"/><path d="M9 25h5v10H9Z" fill="{plate}"/>
<path d="M33 24h7v15h-7Z" fill="{outline}"/><path d="M34 26h4v8h-4Z" fill="{plate}"/>'''
    if name == 'warden':
        body += f'<path d="M4 30h14v15l-7 6-7-6Z" fill="{outline}"/><path d="M6 32h10v12l-5 4-5-4Z" fill="{trim}"/><path d="M10 34h2v11h-2Z" fill="{cloth}"/><path d="M40 9l3 4v24h-5V13Z" fill="#dce6de"/><path d="M36 35h9v3h-9zm4 3h2v8h-2Z" fill="{trim}"/>'
    elif name == 'brute':
        body += '<path d="M39 16h4v35h-4Z" fill="#ae805c"/><path d="M33 10h14v15H33Z" fill="#171f29"/><path d="M35 12h11v10H35Z" fill="#bbaa91"/><path d="M36 13h3v8h-3Z" fill="#e5cfab"/>'
    else:
        body += '<path d="M40 23l4-4-1 18-5 7-2-2 4-7Z" fill="#ded0b4"/><path d="M37 39h3v9h-3Z" fill="#a87964"/>'
    svg(name,body)

svg('caster', '''<path d="M22 5 34 20 31 31 40 55H7l9-24-2-11Z" fill="#131c29"/>
<path d="M22 9 30 21 27 32 35 52H12l8-22-3-9Z" fill="#78608b"/>
<path d="M22 11 26 22 24 46 31 51H16l6-19-4-10Z" fill="#ac8fb2"/>
<path d="M20 20h9v10h-9Z" fill="#252339"/><path d="M22 22h5v2h-5Z" fill="#c9f7ed"/>
<path d="M16 32h17v3H16Z" fill="#dbbb89"/><path d="M24 35h3v14h-3Z" fill="#dbbb89"/>
<path d="M10 30h7v13h-7zm23 1h7v9h-7Z" fill="#655172"/><path d="M40 14h3v44h-3Z" fill="#c6a373"/>
<path d="M36 7 42 3 47 8 42 15Z" fill="#303b4d"/><path d="M39 8 42 5 45 8 42 12Z" fill="#9ae2d5"/>
<path d="M16 52h6v6h-8v-3h2zm10 0h6v3h3v3h-9Z" fill="#252335"/>''')

svg('bellkeeper', '''<path d="M28 3h9v8l10 4 5 9 2 28 8 9v6H2v-6l8-9 2-28 5-9 11-4Z" fill="#171c29"/>
<path d="M30 5h5v10h-5Z" fill="#d4ad69"/><path d="M20 17 32 12 44 17 48 25 49 50 56 60H8l7-10 1-25Z" fill="#a98150"/>
<path d="M21 20 32 16 43 20 45 28H19Z" fill="#e1c18a"/><path d="M21 29h23v19H21Z" fill="#715345"/>
<path d="M26 29h13v8H26Z" fill="#1d2330"/><path d="M27 31h11v2H27Z" fill="#efb586"/>
<path d="M18 23h3v26l-6 9h-4l6-11Zm25 0h3l1 24 6 11h-4l-6-9Z" fill="#f0d6a0"/>
<path d="M9 58h46v6H9Z" fill="#d5b379"/><path d="M12 60h40v2H12Z" fill="#806242"/>
<path d="M28 65h9v5h-9Z" fill="#c7a16b"/><path d="M30 42h4v9h-4zm-5 4h14v2H25Z" fill="#c0a477"/>
<path d="M13 26h4v18h-4Zm35 0h4v18h-4Z" fill="#493c3b"/>''',64,72)

svg('court-banner', '''<path d="M2 2h28v3H2Z" fill="#b99c70"/><path d="M5 5h22v44L16 58 5 49Z" fill="#203e4a"/>
<path d="M7 7h18v41l-9 7-9-7Z" fill="#365a64"/><path d="M9 9h1v39H9Zm13 0h1v39h-1Z" fill="#be9b68"/>
<path d="M16 17 22 27 16 37 10 27Z" fill="#d3b883"/><path d="M16 21 19 27 16 33 13 27Z" fill="#365a64"/>
<path d="M15 13h2v29h-2Z" fill="#d3b883"/>''',32,64)

rate = 22050
rng = random.Random(42)
def audio(name, seconds, sample):
    n = int(rate * seconds)
    data = [max(-.92,min(.92,sample(i/rate, i, n))) for i in range(n)]
    with wave.open(str(root / f'{name}.wav'),'wb') as f:
        f.setparams((1,2,rate,n,'NONE','not compressed'))
        f.writeframes(struct.pack('<'+'h'*n, *(int(x*32767) for x in data)))
def bell(t, freq):
    return sum(math.sin(2*math.pi*freq*r*t)*math.exp(-t*(2+j)) / (j+1) for j,r in enumerate([1,2.71,4.1]))*.18
audio('strike', .18, lambda t,i,n: .25*(rng.random()*2-1)*math.exp(-t*23)+.15*math.sin(2*math.pi*(180*t-220*t*t))*math.exp(-t*18))
audio('hit', .22, lambda t,i,n: .25*math.sin(2*math.pi*75*t)*math.exp(-t*20)+.15*(rng.random()*2-1)*math.exp(-t*30))
audio('memory', .6, lambda t,i,n: bell(t, 660)*.8)
audio('ui', .12, lambda t,i,n: math.sin(2*math.pi*440*t)*.12*math.exp(-t*35))
audio('bell', 2, lambda t,i,n: bell(t, 130.8128))
audio('ambience', 8, lambda t,i,n: .035*math.sin(2*math.pi*55*t)+.025*math.sin(2*math.pi*82.5*t)+.01*(rng.random()*2-1)*math.sin(math.pi*i/n)**2)
notes=[130.8128,196,174.614,146.832,130.8128,261.6256,196,146.832]
audio('music', 16, lambda t,i,n: sum(bell((t-j*2)%16, f)*.3 for j,f in enumerate(notes)))
print('Generated 6 original SVGs and 7 PCM WAVs in',root)
