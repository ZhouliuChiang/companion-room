"""Edit recorded gameplay into a one-minute promotional trailer with an original score."""
from pathlib import Path
from collections import deque
import subprocess, wave, json, math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Artifacts/Demo'
WORK = OUT / 'TrailerWork'
WORK.mkdir(exist_ok=True)
FF = ROOT / 'Tools/demo_deps/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'
SOURCE = OUT / 'CompanionRoom_DEMO_60s.mp4'
DEST = OUT / 'CompanionRoom_Trailer_60s.mp4'
W, H, FPS = 1280, 720, 24
FONT = 'C:/Windows/Fonts/msjhbd.ttc'
REGULAR = 'C:/Windows/Fonts/msjh.ttc'

def score():
    sr, seconds = 44100, 60
    rng = np.random.default_rng(41527)
    music = np.zeros((sr*seconds, 2), np.float32)
    beat = 60/112
    def note(midi, start, length, amp, kind='pluck', pan=0):
        n = int(length*sr)
        t = np.arange(n)/sr
        freq = 440*2**((midi-69)/12)
        if kind == 'bell':
            sound = (np.sin(2*np.pi*freq*t)*np.exp(-4*t)
                     + .22*np.sin(2*np.pi*freq*3.99*t)*np.exp(-12*t)
                     + .06*np.sin(2*np.pi*freq*10.03*t)*np.exp(-23*t))
        elif kind == 'bass':
            sound = (np.sin(2*np.pi*freq*t)+.16*np.sin(4*np.pi*freq*t))*np.exp(-3*t)
        else:
            sound = sum((1/k**1.5)*np.sin(2*np.pi*freq*k*t)*np.exp(-(4+k*.9)*t)
                        for k in range(1, 7))
        sound *= np.minimum(t/.008, 1)*np.minimum((length-t)/.04, 1)*amp
        at = int(start*sr)
        if at < 0 or at >= len(music): return
        n = min(n, len(music)-at)
        gains = np.sqrt([(1-pan)/2, (1+pan)/2])
        music[at:at+n] += sound[:n,None]*gains
    def drum(start, kind, amp):
        length = .22 if kind == 'kick' else .09
        t = np.arange(int(sr*length))/sr
        if kind == 'kick':
            snd = np.sin(2*np.pi*(49*t+28*.035*(1-np.exp(-t/.035))))*np.exp(-t*24)
        else:
            noise = rng.standard_normal(len(t))
            high = noise-np.convolve(noise, np.ones(9)/9, 'same')
            snd = high*np.exp(-t*(55 if kind == 'hat' else 35))
        at = int(start*sr)
        n = min(len(t), len(music)-at)
        if n > 0: music[at:at+n] += snd[:n,None]*amp
    chords = [(48,[60,64,67,71]), (55,[59,62,67,69]),
              (57,[60,64,67,69]), (53,[60,65,69,72]),
              (48,[60,64,67,71]), (52,[59,64,67,71]),
              (53,[60,65,69,72]), (55,[59,62,67,69])]
    melodies = [[76,79,81,79,76,74], [74,79,83,81,79,74],
                [72,76,81,79,76,72], [77,81,84,81,79,77],
                [79,76,72,74,76,79], [76,79,83,79,76,74],
                [77,81,79,77,76,72], [74,79,81,79,74,71]]
    for bar in range(28):
        start = bar*4*beat
        bass, chord = chords[bar%8]
        for k in range(8):
            # A soft, alternating picked accompaniment.
            note(chord[[0,2,1,3,0,2,1,2][k]], start+k*.5*beat,
                 .6, .075, 'pluck', -.35 if k%2 == 0 else .35)
        for k in (0, 2): note(bass-12, start+k*beat, .65, .17, 'bass', 0)
        if bar > 0:
            positions = [0,.75,1.5,2,2.75,3.5]
            for k, midi in enumerate(melodies[bar%8]):
                if bar%4 == 3 and k == 5: continue
                note(midi, start+positions[k]*beat, .72, .105, 'bell', .18)
        if 2 <= bar <= 26:
            for k in (0, 2): drum(start+k*beat, 'kick', .105)
            for k in (1, 3): drum(start+k*beat, 'clap', .035)
            for k in range(8): drum(start+(k*.5+.25)*beat, 'hat', .012 if k%2 else .02)
    # Resolve to C, then let the last notes ring out.
    for m in (48,60,64,67,72): note(m, 58.0, 1.9, .09, 'bell', 0)
    for delay, gain in ((.115,.10),(.231,.06),(.348,.035)):
        n = int(delay*sr)
        music[n:] += music[:-n,::-1].copy()*gain
    envelope = np.minimum(np.arange(len(music))/(sr*.7),1)
    envelope *= np.minimum((len(music)-np.arange(len(music)))/(sr*1.7),1)
    music *= envelope[:,None]
    music *= .86/max(float(np.abs(music).max()), .001)
    with wave.open(str(WORK/'Room_Original_Score.wav'), 'wb') as wav:
        wav.setparams((2,2,sr,0,'NONE','not compressed'))
        wav.writeframes((music*32767).astype('<i2').tobytes())
    print('Original music composed', flush=True)

CLIPS = [
    (6, 3, '01 / YOUR LITTLE ESCAPE', '今天，也給自己一點好心情。', '一間屬於你的療癒小房間', 'room'),
    (0, 4, '兔兔房間  /  COMPANION ROOM', '把日常，過成喜歡的模樣。', '溫柔的房間，藏著剛剛好的小驚喜', 'room'),
    (3, 6, '02 / A ROOM FOR EVERY MOOD', '讓房間，陪你度過每個時刻。', '早晨・夕陽・夜晚，光影柔和流轉', 'room'),
    (10, 2, '03 / MAKE IT YOURS', '用舒服的節奏，享受陪伴。', '音量與文字速度，自由調整', 'ui'),
    (17, 18, '04 / A LITTLE INSPIRATION', '翻開一張牌，遇見新的靈感。', '三張塔羅牌，整理過去、現在與未來', 'ui'),
    (35, 11, '05 / KEEP THE LITTLE MOMENTS', '把每次的小發現，好好收藏。', '立體占卜手帳，翻閱卡牌與牌面筆記', 'ui'),
    (48, 11, '06 / A MOMENT OF CALM', '靜下心，跟著靈擺輕輕搖晃。', '從校正開始，體驗一段專注的小儀式', 'ui'),
    (0, 6.75, '', '', '', 'end'),
]

def font(size, regular=False): return ImageFont.truetype(REGULAR if regular else FONT, size)

def overlay(clip):
    image = Image.new('RGBA',(W,H))
    d = ImageDraw.Draw(image)
    plum, cream, rose = (43,28,49,255), (255,247,235,255), (234,164,182,255)
    if clip[5] == 'end':
        d.rectangle((0,0,W,H), fill=(31,20,38,180))
        d.line((520,216,760,216),fill=rose,width=2)
        d.text((640,278),'兔兔房間',font=font(76),anchor='mm',fill=cream)
        d.text((640,346),'COMPANION ROOM',font=font(19),anchor='mm',fill=rose)
        d.text((640,430),'為日常，留一個溫柔角落。',font=font(35),anchor='mm',fill=cream)
        d.line((570,483,710,483),fill=rose,width=2)
        d.text((640,535),'房間互動  ·  塔羅占卜  ·  占卜手帳  ·  靈擺',font=font(22,True),anchor='mm',fill=cream)
        d.text((640,650),'走進房間，開始你的陪伴時光。',font=font(19,True),anchor='mm',fill=rose)
        return image
    # Opaque mattes replace the small white titles and low-contrast dialogue in the recording.
    d.rectangle((0,0,W,73),fill=plum)
    d.rectangle((0,538,W,H),fill=plum)
    d.rectangle((48,27,52,47),fill=rose)
    d.text((68,37),clip[2],font=font(20),anchor='lm',fill=cream)
    d.text((1230,37),'兔兔房間',font=font(19),anchor='rm',fill=rose)
    d.line((48,566,114,566),fill=rose,width=3)
    d.text((48,589),clip[3],font=font(38),fill=cream)
    d.text((49,649),clip[4],font=font(23,True),fill=(225,209,222,255))
    d.text((1230,679),'COMPANION ROOM',font=font(12),anchor='rs',fill=rose)
    return image

def gameplay_frames(clip):
    start,length,*_ = clip
    cmd = [str(FF),'-hide_banner','-loglevel','error','-ss',str(start),'-i',str(SOURCE),
           '-t',str(length),'-vf','fps=24,eq=contrast=1.1:brightness=-0.025:gamma=0.72:saturation=1.12',
           '-f','rawvideo','-pix_fmt','rgb24','pipe:1']
    decoder = subprocess.Popen(cmd, stdout=subprocess.PIPE)
    layer = overlay(clip)
    count = round(length*FPS)
    for i in range(count):
        raw = decoder.stdout.read(W*H*3)
        if len(raw) != W*H*3: raise RuntimeError('Incomplete source decode')
        im = Image.frombytes('RGB',(W,H),raw)
        if clip[5] in ('room','end'):
            zoom = 1+.025*i/max(count-1,1)
            nw,nh = round(W/zoom),round(H/zoom)
            im = im.crop(((W-nw)//2,(H-nh)//2,(W+nw)//2,(H+nh)//2)).resize((W,H),Image.Resampling.BICUBIC)
        if clip[5] == 'end': im = im.filter(ImageFilter.GaussianBlur(5))
        im = Image.alpha_composite(im.convert('RGBA'),layer).convert('RGB')
        yield im
    decoder.stdout.close()
    if decoder.wait() != 0: raise RuntimeError('Decoder failed')

def render():
    intermediate = WORK/'Trailer_Picture.mp4'
    enc = subprocess.Popen([str(FF),'-y','-hide_banner','-loglevel','warning',
        '-f','rawvideo','-pix_fmt','rgb24','-s','1280x720','-r','24','-i','pipe:0',
        '-an','-c:v','libx264','-preset','medium','-crf','17','-pix_fmt','yuv420p',str(intermediate)],stdin=subprocess.PIPE)
    pending = deque()
    frames = 0
    def send(im):
        nonlocal frames
        # Fade the opening and closing pictures, leaving the typography readable throughout.
        if frames < 8: im = Image.blend(Image.new('RGB',(W,H),(43,28,49)),im,(frames+1)/8)
        if frames >= 1428: im = Image.blend(im,Image.new('RGB',(W,H),(43,28,49)),(frames-1427)/12)
        enc.stdin.write(im.tobytes())
        if frames in (36,132,240,360,600,864,1104,1344): im.save(WORK/f'proof_{frames:04}.jpg',quality=94)
        frames += 1
    for index,clip in enumerate(CLIPS):
        print(f'Rendering scene {index+1}/{len(CLIPS)}',flush=True)
        incoming = gameplay_frames(clip)
        if pending:
            for k in range(6): send(Image.blend(pending.popleft(),next(incoming),(k+1)/7))
        for im in incoming:
            pending.append(im)
            if len(pending)>6: send(pending.popleft())
    while pending: send(pending.popleft())
    enc.stdin.close()
    if enc.wait() != 0: raise RuntimeError('Picture encoding failed')
    if frames != 1440: raise RuntimeError(f'Unexpected frame count: {frames}')
    # Two-pass average bitrate keeps the complete picture and soundtrack comfortably below 10MB.
    common = [str(FF),'-y','-hide_banner','-loglevel','warning','-i',str(intermediate)]
    video = ['-c:v','libx264','-preset','slow','-b:v','1030k','-pix_fmt','yuv420p',
             '-passlogfile',str(WORK/'trailer_pass')]
    subprocess.run(common+video+['-pass','1','-an','-f','null','NUL'],check=True)
    subprocess.run(common+['-i',str(WORK/'Room_Original_Score.wav')]+video+
                   ['-pass','2','-map','0:v:0','-map','1:a:0','-af','loudnorm=I=-17:TP=-1.5:LRA=9',
                    '-c:a','aac','-b:a','112k','-t','60','-movflags','+faststart',str(DEST)],check=True)
    size = DEST.stat().st_size
    if size >= 10_000_000: raise RuntimeError(f'File exceeds limit: {size}')
    (OUT/'Trailer_notes.txt').write_text(
        f'60 seconds / 1280x720 / 24 fps / H.264 + AAC\n{size} bytes\n'
        'Original instrumental score: procedural composition, 112 BPM, C major.\n'
        'No third-party music recording or sampled music was used.\n'
        'Gameplay source: CompanionRoom_DEMO_60s.mp4 (preserved unchanged).\n'
        'Edited with typography, contrast correction, reframing, crossfades and closing title.\n',encoding='utf-8')
    print(json.dumps({'output':str(DEST),'bytes':size,'frames':frames}),flush=True)

if __name__ == '__main__':
    score()
    render()
