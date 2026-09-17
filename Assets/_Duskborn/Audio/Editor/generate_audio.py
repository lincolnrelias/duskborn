import os
import wave
import struct
import math
import numpy as np

SR = 44100

def create_stereo(left, right=None):
    if right is None:
        right = left.copy()
    left = np.clip(left, -1.0, 1.0)
    right = np.clip(right, -1.0, 1.0)
    return np.vstack((left, right))

def save_wav(filename, stereo_audio, normalize_db=-0.8):
    peak = np.max(np.abs(stereo_audio))
    if peak > 0:
        target_peak = 10 ** (normalize_db / 20)
        stereo_audio = stereo_audio * (target_peak / peak)
    stereo_audio = np.clip(stereo_audio, -1.0, 1.0)
    int_data = (stereo_audio * 32767).astype(np.int16)
    
    interleaved = np.empty((int_data.shape[1] * 2,), dtype=np.int16)
    interleaved[0::2] = int_data[0]
    interleaved[1::2] = int_data[1]
    
    os.makedirs(os.path.dirname(os.path.abspath(filename)), exist_ok=True)
    with wave.open(filename, 'wb') as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(interleaved.tobytes())
    print(f"Generated: {filename} ({len(int_data[0])/SR:.2f}s)")

# --- DSP Utilities ---

def apply_biquad(audio, b0, b1, b2, a1, a2):
    out = np.zeros_like(audio)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(audio)):
        x0 = audio[i]
        y0 = b0*x0 + b1*x1 + b2*x2 - a1*y1 - a2*y2
        out[i] = y0
        x2, x1 = x1, x0
        y2, y1 = y1, y0
    return out

def lowpass_filter(audio, cutoff, q=0.707):
    cutoff = min(cutoff, SR * 0.49)
    w0 = 2 * math.pi * cutoff / SR
    alpha = math.sin(w0) / (2 * q)
    cos_w0 = math.cos(w0)
    b0 = (1 - cos_w0) / 2
    b1 = 1 - cos_w0
    b2 = (1 - cos_w0) / 2
    a0 = 1 + alpha
    a1 = -2 * cos_w0
    a2 = 1 - alpha
    return apply_biquad(audio, b0/a0, b1/a0, b2/a0, a1/a0, a2/a0)

def highpass_filter(audio, cutoff, q=0.707):
    cutoff = max(cutoff, 10.0)
    w0 = 2 * math.pi * cutoff / SR
    alpha = math.sin(w0) / (2 * q)
    cos_w0 = math.cos(w0)
    b0 = (1 + cos_w0) / 2
    b1 = -(1 + cos_w0)
    b2 = (1 + cos_w0) / 2
    a0 = 1 + alpha
    a1 = -2 * cos_w0
    a2 = 1 - alpha
    return apply_biquad(audio, b0/a0, b1/a0, b2/a0, a1/a0, a2/a0)

def bandpass_filter(audio, center_freq, q=2.0):
    center_freq = min(max(center_freq, 20.0), SR * 0.49)
    w0 = 2 * math.pi * center_freq / SR
    alpha = math.sin(w0) / (2 * q)
    cos_w0 = math.cos(w0)
    b0 = alpha
    b1 = 0.0
    b2 = -alpha
    a0 = 1 + alpha
    a1 = -2 * cos_w0
    a2 = 1 - alpha
    return apply_biquad(audio, b0/a0, b1/a0, b2/a0, a1/a0, a2/a0)

def simple_reverb(audio, decay=0.35, delays=(1117, 1373, 1789, 2111), wet=0.25):
    out = audio.copy()
    comb_sum = np.zeros_like(audio)
    for d in delays:
        buf = np.zeros_like(audio)
        for i in range(len(audio)):
            delayed = buf[i - d] if i >= d else 0.0
            buf[i] = audio[i] + delayed * decay
        comb_sum += buf
    comb_sum /= len(delays)
    return out * (1.0 - wet) + comb_sum * wet

def white_noise(samples):
    return np.random.uniform(-1.0, 1.0, samples)

def pink_noise(samples):
    white = np.random.uniform(-1.0, 1.0, samples)
    b0 = b1 = b2 = 0.0
    pink = np.zeros(samples)
    for i in range(samples):
        b0 = 0.99765 * b0 + white[i] * 0.0990460
        b1 = 0.96300 * b1 + white[i] * 0.2965164
        b2 = 0.57000 * b2 + white[i] * 1.0526913
        pink[i] = b0 + b1 + b2 + white[i] * 0.1848
    return pink / 3.0

# ==========================================
# 1. WEAPON SWINGS & WHOOSHES
# ==========================================

def gen_swing_light(variation=1):
    duration = 0.22 + variation * 0.02
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    noise = white_noise(n) * 0.6 + pink_noise(n) * 0.4
    env = np.sin(np.pi * (t / duration) ** 0.8) ** 2
    
    sub_freq = np.linspace(180, 80, n)
    phase = np.cumsum(2 * np.pi * sub_freq / SR)
    sub = np.sin(phase) * env * 0.4
    
    edge = highpass_filter(noise, 1500) * env * 0.7
    swept = bandpass_filter(noise, 650 + variation * 100, q=1.8) * env * 1.2
    
    mono = swept + edge + sub
    mono = np.tanh(mono * 1.5)
    
    pan = np.linspace(-0.4, 0.4, n)
    left = mono * (1 - pan) * 0.5
    right = mono * (1 + pan) * 0.5
    return create_stereo(left, right)

def gen_swing_heavy(variation=1):
    duration = 0.38 + variation * 0.04
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    noise = pink_noise(n) * 0.8 + white_noise(n) * 0.2
    env = np.sin(np.pi * (t / duration) ** 0.7) ** 2.5
    
    sub_freq = np.linspace(120, 45, n)
    phase = np.cumsum(2 * np.pi * sub_freq / SR)
    sub = np.sin(phase) * env * 0.7
    
    swept = bandpass_filter(noise, 400 + variation * 80, q=2.2) * env * 1.5
    blade_friction = bandpass_filter(noise, 1200, q=3.0) * env * 0.5
    
    mono = swept + sub + blade_friction
    mono = np.tanh(mono * 1.8)
    
    pan = np.linspace(-0.6, 0.6, n)
    left = mono * (1 - pan) * 0.5
    right = mono * (1 + pan) * 0.5
    return create_stereo(left, right)

# ==========================================
# 2. IMPACTS & SURFACE HITS
# ==========================================

def gen_hit_flesh(variation=1):
    duration = 0.28
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    click = np.zeros(n)
    click_n = min(150, n)
    click[:click_n] = np.sin(np.linspace(0, np.pi*3, click_n)) * np.linspace(1, 0, click_n)
    
    thump_freq = np.linspace(130 + variation * 10, 45, n)
    thump = np.sin(np.cumsum(2 * np.pi * thump_freq / SR)) * np.exp(-18 * t)
    
    crunch_noise = white_noise(n)
    crunch = bandpass_filter(crunch_noise, 900 + variation * 150, q=2.5) * np.exp(-25 * t)
    squelch = bandpass_filter(crunch_noise, 2200, q=4.0) * np.exp(-35 * t) * 0.6
    
    mono = click * 0.8 + thump * 1.2 + crunch * 0.9 + squelch * 0.6
    mono = np.tanh(mono * 2.0)
    
    left = mono * 0.95
    right = np.roll(mono, 8) * 0.95
    return simple_reverb(create_stereo(left, right), decay=0.2, wet=0.15)

def gen_hit_wood(variation=1):
    duration = 0.32
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    click = np.zeros(n)
    click_len = 100
    click[:click_len] = np.sin(np.linspace(0, np.pi*4, click_len)) * np.linspace(1, 0, click_len)
    
    modes = [180 + variation*15, 340 + variation*20, 680 + variation*30, 1150]
    wood_body = np.zeros(n)
    for idx, f in enumerate(modes):
        wood_body += np.sin(2 * np.pi * f * t) * np.exp(-(16 + idx * 8) * t) * (1.0 / (idx + 1))
        
    splinters = np.zeros(n)
    for _ in range(8 + variation * 2):
        pos = np.random.randint(50, int(SR * 0.12))
        splinters[pos:pos+40] += white_noise(40) * np.exp(-np.linspace(0, 5, 40)) * 0.5
    splinters = highpass_filter(splinters, 1200)
    
    mono = click * 0.9 + wood_body * 1.1 + splinters * 0.7
    mono = np.tanh(mono * 1.7)
    
    left = mono
    right = np.roll(mono, 6)
    return simple_reverb(create_stereo(left, right), decay=0.25, wet=0.18)

def gen_hit_stone(variation=1):
    duration = 0.30
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    click = np.zeros(n)
    click_len = 80
    click[:click_len] = white_noise(click_len) * np.linspace(1, 0, click_len)
    
    pings = [820 + variation * 40, 1420 + variation * 60, 2650, 4200]
    mineral_tone = np.zeros(n)
    for idx, f in enumerate(pings):
        decay = 25 + idx * 12
        mineral_tone += np.sin(2 * np.pi * f * t) * np.exp(-decay * t) * (0.8 / (idx + 1))
        
    gravel = bandpass_filter(white_noise(n), 2800, q=2.0) * np.exp(-30 * t) * 0.6
    
    mono = click * 1.0 + mineral_tone * 1.3 + gravel * 0.7
    mono = np.tanh(mono * 1.8)
    
    left = mono
    right = np.roll(mono, 4)
    return simple_reverb(create_stereo(left, right), decay=0.3, wet=0.22)

def gen_hit_metal(variation=1):
    duration = 0.55
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    strike = np.zeros(n)
    strike[:120] = white_noise(120) * np.linspace(1, 0, 120)
    strike = highpass_filter(strike, 3000)
    
    base_f = 920 + variation * 80
    modes = [base_f, base_f * 1.78, base_f * 2.45, base_f * 3.32, base_f * 4.6]
    bell_decay = [8.0, 12.0, 16.0, 22.0, 30.0]
    metal_ring = np.zeros(n)
    for idx, (f, dec) in enumerate(zip(modes, bell_decay)):
        metal_ring += np.sin(2 * np.pi * f * t + np.random.uniform(0, math.pi)) * np.exp(-dec * t) * (0.7 / (idx + 1))
        
    mono = strike * 1.1 + metal_ring * 1.4
    mono = np.tanh(mono * 1.6)
    
    left = mono
    right = np.roll(mono, 10)
    return simple_reverb(create_stereo(left, right), decay=0.45, wet=0.28)

def gen_hit_default(variation=1):
    duration = 0.25
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    thump = np.sin(2 * np.pi * (110 - 40 * t/duration) * t) * np.exp(-20 * t)
    dirt = bandpass_filter(white_noise(n), 700 + variation * 100, q=1.5) * np.exp(-22 * t)
    
    mono = thump * 1.2 + dirt * 0.8
    mono = np.tanh(mono * 1.5)
    return create_stereo(mono, np.roll(mono, 5))

# ==========================================
# 3. PLAYER LOCOMOTION & VITALS
# ==========================================

def gen_footstep_grass(variation=1):
    duration = 0.14
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    thump = np.sin(2 * np.pi * (95 + variation * 8) * t) * np.exp(-35 * t) * 0.7
    foliage = bandpass_filter(white_noise(n), 2400 + variation * 200, q=2.5) * np.exp(-40 * t) * 0.5
    low_turf = bandpass_filter(pink_noise(n), 450, q=1.8) * np.exp(-28 * t) * 0.4
    
    mono = thump + foliage + low_turf
    mono = np.tanh(mono * 1.5)
    return create_stereo(mono, np.roll(mono, 4))

def gen_footstep_stone(variation=1):
    duration = 0.12
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    heel_strike = np.sin(2 * np.pi * (240 + variation * 20) * t) * np.exp(-45 * t) * 0.9
    mineral_click = bandpass_filter(white_noise(n), 3200, q=3.0) * np.exp(-50 * t) * 0.5
    
    mono = heel_strike + mineral_click
    mono = np.tanh(mono * 1.6)
    return create_stereo(mono, np.roll(mono, 3))

def gen_jump_takeoff():
    duration = 0.22
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    whoosh = bandpass_filter(pink_noise(n), 550, q=1.5) * (np.sin(np.pi * t / duration) ** 2) * 1.2
    cloth = highpass_filter(white_noise(n), 1800) * np.exp(-12 * t) * 0.4
    
    mono = np.tanh((whoosh + cloth) * 1.4)
    return create_stereo(mono, np.roll(mono, 6))

def gen_jump_land():
    duration = 0.28
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    toe_t = t[:int(SR*0.06)]
    toe = np.sin(2 * np.pi * 140 * toe_t) * np.exp(-40 * toe_t) * 0.5
    
    heel_t = t[int(SR*0.04):]
    heel = np.sin(2 * np.pi * 65 * (heel_t - heel_t[0])) * np.exp(-18 * (heel_t - heel_t[0])) * 1.2
    
    body = np.zeros(n)
    body[:len(toe)] += toe
    body[int(SR*0.04):int(SR*0.04)+len(heel)] += heel
    
    turf_crush = bandpass_filter(pink_noise(n), 800, q=2.0) * np.exp(-22 * t) * 0.7
    
    mono = np.tanh((body + turf_crush) * 1.6)
    return create_stereo(mono, np.roll(mono, 7))

def gen_player_hurt(variation=1):
    duration = 0.35
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    f0 = np.linspace(135 - variation * 10, 80, n)
    glottal = (np.sin(np.cumsum(2 * np.pi * f0 / SR)) + 0.5 * np.sin(np.cumsum(4 * np.pi * f0 / SR))) * np.exp(-14 * t)
    vocal = bandpass_filter(glottal, 480, q=3.0) * 1.0 + bandpass_filter(glottal, 1150, q=4.0) * 0.6
    thump = np.sin(2 * np.pi * 75 * t) * np.exp(-25 * t) * 0.9
    
    mono = np.tanh((vocal + thump) * 1.8)
    return create_stereo(mono, np.roll(mono, 6))

def gen_player_death():
    duration = 0.9
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    thud_freq = np.linspace(95, 35, n)
    thud = np.sin(np.cumsum(2 * np.pi * thud_freq / SR)) * np.exp(-6 * t) * 1.2
    gasp_noise = bandpass_filter(white_noise(n), 1200, q=2.0) * (np.sin(np.pi * (t / 0.5)) ** 2) * (t < 0.5) * 0.6
    sub = np.sin(2 * np.pi * 42 * t) * np.exp(-4 * t) * 0.7
    
    mono = np.tanh((thud + gasp_noise + sub) * 1.5)
    return simple_reverb(create_stereo(mono, np.roll(mono, 10)), decay=0.4, wet=0.25)

def gen_heartbeat_loop():
    duration = 1.0
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    audio = np.zeros(n)
    
    lub_len = int(SR * 0.2)
    t_lub = t[:lub_len]
    lub = np.sin(2 * np.pi * 52 * t_lub) * np.exp(-20 * t_lub) * 1.2
    audio[:lub_len] += lub
    
    offset = int(SR * 0.28)
    dub_len = int(SR * 0.25)
    t_dub = t[:dub_len]
    dub = np.sin(2 * np.pi * 64 * t_dub) * np.exp(-18 * t_dub) * 1.0
    audio[offset:offset+dub_len] += dub
    
    audio = np.tanh(audio * 1.6)
    return create_stereo(audio, audio)

# ==========================================
# 4. ENEMY (SWARMER) AUDIO
# ==========================================

def gen_swarmer_attack(variation=1):
    duration = 0.35
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    snap = np.zeros(n)
    snap[:80] = white_noise(80) * np.linspace(1, 0, 80)
    snap = highpass_filter(snap, 2500)
    
    mod = np.sin(2 * np.pi * 45 * t)
    hiss_freq = np.linspace(2400 + variation * 300, 1100, n)
    screech = np.sin(np.cumsum(2 * np.pi * (hiss_freq + 200 * mod) / SR)) * np.exp(-10 * t) * 0.8
    hiss = bandpass_filter(white_noise(n), 3200, q=2.0) * np.exp(-12 * t) * 0.7
    
    mono = np.tanh((snap * 1.1 + screech * 0.9 + hiss * 0.6) * 1.8)
    return create_stereo(mono, np.roll(mono, 8))

def gen_swarmer_hurt(variation=1):
    duration = 0.26
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    freq = np.linspace(1800 + variation * 200, 750, n)
    yelp = np.sin(np.cumsum(2 * np.pi * freq / SR)) * np.exp(-16 * t) * 0.9
    squawk = bandpass_filter(white_noise(n), 1900, q=3.0) * np.exp(-18 * t) * 0.7
    
    mono = np.tanh((yelp + squawk) * 1.7)
    return create_stereo(mono, np.roll(mono, 5))

def gen_swarmer_death(variation=1):
    duration = 0.52
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    crunch = bandpass_filter(white_noise(n), 1400, q=1.8) * np.exp(-14 * t) * 1.1
    splatter = bandpass_filter(pink_noise(n), 650, q=2.5) * np.exp(-16 * t) * 0.9
    rasp = bandpass_filter(white_noise(n), 2800, q=3.5) * np.exp(-8 * t) * 0.5
    
    mono = np.tanh((crunch + splatter + rasp) * 1.8)
    return simple_reverb(create_stereo(mono, np.roll(mono, 8)), decay=0.35, wet=0.22)

# ==========================================
# 5. ENVIRONMENT, PROPS & LOOT
# ==========================================

def gen_chest_open():
    duration = 0.85
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    click = np.zeros(n)
    click[:120] = np.sin(np.linspace(0, np.pi*6, 120)) * np.linspace(1, 0, 120)
    
    creak_t = t[int(SR*0.05):int(SR*0.35)]
    creak_freq = 240 + 60 * np.sin(2 * np.pi * 35 * (creak_t - creak_t[0]))
    creak_saw = (np.mod(np.cumsum(creak_freq / SR), 1.0) * 2 - 1) * np.sin(np.pi * (creak_t - creak_t[0]) / (creak_t[-1] - creak_t[0])) * 0.7
    
    notes = [1046.5, 1318.5, 1567.98, 1975.53, 2093.0]
    shimmer = np.zeros(n)
    for idx, f in enumerate(notes):
        onset = int(SR * (0.28 + idx * 0.08))
        if onset < n:
            rem = n - onset
            t_rem = np.linspace(0, rem/SR, rem)
            shimmer[onset:] += (np.sin(2 * np.pi * f * t_rem) + 0.3 * np.sin(4 * np.pi * f * t_rem)) * np.exp(-7 * t_rem) * 0.4
            
    audio = np.zeros(n)
    audio += click * 1.2
    audio[int(SR*0.05):int(SR*0.05)+len(creak_saw)] += creak_saw
    audio += shimmer
    
    audio = np.tanh(audio * 1.6)
    left = audio
    right = np.roll(audio, 12)
    return simple_reverb(create_stereo(left, right), decay=0.45, wet=0.35)

def gen_tree_fall():
    duration = 1.4
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    creak = bandpass_filter(pink_noise(n), 320, q=2.0) * (t < 0.4) * np.sin(np.pi * t / 0.4) * 0.8
    
    crack = np.zeros(n)
    crack_start = int(SR * 0.35)
    crack_len = int(SR * 0.15)
    crack[crack_start:crack_start+crack_len] = white_noise(crack_len) * np.linspace(1, 0, crack_len)
    crack = bandpass_filter(crack, 1100, q=1.5) * 1.5
    
    impact_start = int(SR * 0.7)
    impact_len = n - impact_start
    t_imp = np.linspace(0, impact_len/SR, impact_len)
    impact = np.sin(2 * np.pi * 55 * t_imp) * np.exp(-9 * t_imp) * 1.3
    leaves = bandpass_filter(white_noise(impact_len), 2200, q=1.8) * np.exp(-6 * t_imp) * 0.6
    
    audio = creak + crack
    audio[impact_start:] += impact + leaves
    audio = np.tanh(audio * 1.6)
    return simple_reverb(create_stereo(audio, np.roll(audio, 10)), decay=0.4, wet=0.25)

def gen_rock_shatter():
    duration = 1.1
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    crack = np.zeros(n)
    crack[:int(SR*0.08)] = white_noise(int(SR*0.08)) * np.linspace(1, 0, int(SR*0.08))
    crack = highpass_filter(crack, 2200) * 1.2
    
    boom = np.sin(2 * np.pi * 70 * t) * np.exp(-12 * t) * 1.3
    
    debris = np.zeros(n)
    for _ in range(12):
        pos = np.random.randint(int(SR*0.1), int(SR*0.8))
        d_len = np.random.randint(500, 1500)
        if pos + d_len < n:
            debris[pos:pos+d_len] += np.sin(2 * np.pi * np.random.uniform(300, 1200) * np.linspace(0, d_len/SR, d_len)) * np.linspace(1, 0, d_len) * 0.3
            
    mono = np.tanh((crack + boom + debris) * 1.6)
    return simple_reverb(create_stereo(mono, np.roll(mono, 8)), decay=0.35, wet=0.25)

def gen_gold_pickup():
    duration = 0.38
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    coin1 = (np.sin(2 * np.pi * 2450 * t) + 0.4 * np.sin(2 * np.pi * 4900 * t)) * np.exp(-18 * t)
    
    coin2 = np.zeros(n)
    offset = int(SR * 0.07)
    t2 = t[:n-offset]
    coin2[offset:] = (np.sin(2 * np.pi * 2900 * t2) + 0.3 * np.sin(2 * np.pi * 5800 * t2)) * np.exp(-22 * t2) * 0.7
    
    audio = np.tanh((coin1 + coin2) * 1.5)
    left = audio
    right = np.roll(audio, 5) * 0.95
    return simple_reverb(create_stereo(left, right), decay=0.3, wet=0.25)

def gen_item_pickup():
    duration = 0.48
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    frequencies = [659.25, 987.77, 1318.51]
    audio = np.zeros(n)
    for idx, f in enumerate(frequencies):
        onset = int(SR * (idx * 0.08))
        rem = n - onset
        t_rem = np.linspace(0, rem/SR, rem)
        audio[onset:] += (np.sin(2 * np.pi * f * t_rem) + 0.25 * np.sin(4 * np.pi * f * t_rem)) * np.exp(-9 * t_rem) * 0.5
        
    audio = np.tanh(audio * 1.5)
    return simple_reverb(create_stereo(audio, np.roll(audio, 8)), decay=0.45, wet=0.35)

# ==========================================
# 6. UI & TRANSITION STINGERS
# ==========================================

def gen_ui_button_click():
    duration = 0.07
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    click = np.sin(2 * np.pi * 420 * t) * np.exp(-55 * t) * 1.2
    transient = white_noise(n) * np.exp(-80 * t) * 0.6
    
    mono = np.tanh((click + transient) * 1.5)
    return create_stereo(mono, mono)

def gen_ui_modal_open():
    duration = 0.24
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    parchment = bandpass_filter(white_noise(n), 1600, q=2.0) * (np.sin(np.pi * t / duration) ** 2) * 1.2
    rustle = highpass_filter(white_noise(n), 3500) * np.exp(-15 * t) * 0.5
    
    mono = np.tanh((parchment + rustle) * 1.4)
    return create_stereo(mono, np.roll(mono, 4))

def gen_ui_error():
    duration = 0.16
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    knock = np.sin(2 * np.pi * 145 * t) * np.exp(-30 * t) * 1.3
    noise = bandpass_filter(white_noise(n), 450, q=1.5) * np.exp(-35 * t) * 0.5
    
    mono = np.tanh((knock + noise) * 1.5)
    return create_stereo(mono, mono)

def gen_dawn_horn():
    duration = 2.6
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    freq = np.where(t < 0.6, 196.0, 293.66)
    phase = np.cumsum(2 * np.pi * freq / SR)
    brass = (
        1.0 * np.sin(phase) +
        0.6 * np.sin(2 * phase) +
        0.35 * np.sin(3 * phase) +
        0.2 * np.sin(4 * phase)
    )
    
    env = np.ones(n)
    attack_n = int(SR * 0.18)
    env[:attack_n] = np.linspace(0, 1, attack_n)
    env[attack_n:] = np.exp(-1.5 * (t[attack_n:] - t[attack_n]))
    
    mono = brass * env
    mono = np.tanh(mono * 1.4)
    
    left = mono
    right = np.roll(mono, 14)
    return simple_reverb(create_stereo(left, right), decay=0.55, wet=0.38)

def gen_night_horn():
    duration = 3.2
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    f1 = 82.4
    f2 = 116.5
    brass1 = np.sin(2 * np.pi * f1 * t) + 0.7 * np.sin(4 * np.pi * f1 * t) + 0.4 * np.sin(6 * np.pi * f1 * t)
    brass2 = np.sin(2 * np.pi * f2 * t) + 0.5 * np.sin(4 * np.pi * f2 * t)
    
    env = np.zeros(n)
    swell_n = int(SR * 0.4)
    env[:swell_n] = np.linspace(0, 1, swell_n) ** 1.5
    env[swell_n:] = np.exp(-1.2 * (t[swell_n:] - t[swell_n]))
    
    boom = np.sin(2 * np.pi * 48 * t) * np.exp(-4 * t) * 1.1
    
    mono = (brass1 * 0.8 + brass2 * 0.5) * env + boom
    mono = np.tanh(mono * 1.7)
    
    left = mono
    right = np.roll(mono, 16)
    return simple_reverb(create_stereo(left, right), decay=0.6, wet=0.45)

# ==========================================
# 7. MUSIC & AMBIENT SOUNDSCAPES (LOOPABLE)
# ==========================================

def karplus_strong(freq, duration_s, decay=0.992):
    period = int(SR / freq)
    n = int(SR * duration_s)
    buffer = np.random.uniform(-1.0, 1.0, period)
    out = np.zeros(n)
    for i in range(n):
        val = buffer[i % period]
        out[i] = val
        buffer[i % period] = 0.5 * (val + buffer[(i + 1) % period]) * decay
    return out

def gen_music_main_menu():
    duration = 40.0
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    left = np.zeros(n)
    right = np.zeros(n)
    
    drone_freqs = [73.42, 110.0, 146.83, 174.61]
    drone = np.zeros(n)
    for f in drone_freqs:
        drone += np.sin(2 * np.pi * f * t) * 0.12
    drone = lowpass_filter(drone, 600)
    left += drone * 0.6
    right += drone * 0.6
    
    notes = [146.83, 174.61, 196.0, 220.0, 261.63, 293.66, 329.63, 349.23, 440.0]
    pattern = [0, 3, 5, 7, 5, 3, 1, 4, 6, 8, 6, 4, 2, 4, 5, 7]
    step_time = 0.625
    
    for phrase in range(4):
        for step_idx, note_idx in enumerate(pattern):
            onset_s = (phrase * 10.0) + (step_idx * step_time)
            onset = int(onset_s * SR)
            freq = notes[note_idx % len(notes)]
            if phrase in (1, 3):
                freq *= 1.5
            pluck = karplus_strong(freq, 2.5, decay=0.993)
            p_len = len(pluck)
            rem = min(p_len, n - onset)
            
            pan = 0.25 * math.sin(step_idx * 1.2)
            left[onset:onset+rem] += pluck[:rem] * (0.5 - pan) * 0.35
            right[onset:onset+rem] += pluck[:rem] * (0.5 + pan) * 0.35
            
    wind = bandpass_filter(pink_noise(n), 450, q=1.2) * 0.08
    left += wind
    right += np.roll(wind, 50)
    
    seam_len = int(SR * 1.5)
    fade_in = np.linspace(0, 1, seam_len)
    fade_out = np.linspace(1, 0, seam_len)
    left[:seam_len] = left[:seam_len] * fade_in + left[-seam_len:] * fade_out
    right[:seam_len] = right[:seam_len] * fade_in + right[-seam_len:] * fade_out
    
    stereo = create_stereo(left, right)
    return simple_reverb(stereo, decay=0.5, wet=0.35)

def gen_music_day_exploration():
    duration = 44.0
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    left = np.zeros(n)
    right = np.zeros(n)
    
    chords = [
        [98.0, 146.83, 196.0, 246.94, 293.66],  # G
        [82.4, 123.47, 164.81, 196.0, 246.94], # Em
        [65.41, 130.81, 164.81, 196.0, 261.63],# C
        [73.42, 146.83, 220.0, 293.66, 369.99] # D
    ]
    chord_len = 11.0
    
    for c_idx, chord in enumerate(chords):
        for rep in range(4):
            strum_time = (c_idx * chord_len) + (rep * 2.75)
            for s_idx, note_f in enumerate(chord):
                onset_s = strum_time + s_idx * 0.035
                onset = int(onset_s * SR)
                if onset >= n: break
                pluck = karplus_strong(note_f, 3.2, decay=0.994)
                rem = min(len(pluck), n - onset)
                left[onset:onset+rem] += pluck[:rem] * 0.28
                right[onset:onset+rem] += pluck[:rem] * 0.28
                
    flute_notes = [293.66, 329.63, 392.0, 440.0, 493.88, 587.33]
    for phrase in range(4):
        for f_idx in range(6):
            f_start = phrase * 11.0 + f_idx * 1.5 + 1.0
            onset = int(f_start * SR)
            dur_s = 1.2
            dur_n = int(dur_s * SR)
            if onset + dur_n >= n: break
            t_flute = np.linspace(0, dur_s, dur_n)
            vibrato = 1.0 + 0.012 * np.sin(2 * np.pi * 5.0 * t_flute)
            freq = flute_notes[(f_idx + phrase * 2) % len(flute_notes)] * vibrato
            phase = np.cumsum(2 * np.pi * freq / SR)
            flute_tone = (np.sin(phase) + 0.15 * np.sin(2 * phase)) * (np.sin(np.pi * t_flute / dur_s) ** 1.5) * 0.18
            left[onset:onset+dur_n] += flute_tone * 0.4
            right[onset:onset+dur_n] += flute_tone * 0.6
            
    wind = bandpass_filter(pink_noise(n), 520, q=1.5) * 0.06
    left += wind
    right += np.roll(wind, 40)
    
    seam_len = int(SR * 2.0)
    fade_in = np.linspace(0, 1, seam_len)
    fade_out = np.linspace(1, 0, seam_len)
    left[:seam_len] = left[:seam_len] * fade_in + left[-seam_len:] * fade_out
    right[:seam_len] = right[:seam_len] * fade_in + right[-seam_len:] * fade_out
    
    stereo = create_stereo(left, right)
    return simple_reverb(stereo, decay=0.45, wet=0.28)

def gen_music_night_combat():
    duration = 45.0
    n = int(SR * duration)
    t = np.linspace(0, duration, n)
    
    left = np.zeros(n)
    right = np.zeros(n)
    
    beat_s = 60.0 / 128.0
    total_beats = int(duration / beat_s)
    
    taiko_n = int(SR * 0.35)
    t_taiko = np.linspace(0, 0.35, taiko_n)
    taiko_boom = np.sin(2 * np.pi * (75 - 35 * t_taiko/0.35) * t_taiko) * np.exp(-12 * t_taiko) * 1.2
    taiko_punch = bandpass_filter(pink_noise(taiko_n), 150, q=1.5) * np.exp(-15 * t_taiko) * 0.8
    taiko_hit = np.tanh(taiko_boom + taiko_punch)
    
    rim_n = int(SR * 0.15)
    t_rim = np.linspace(0, 0.15, rim_n)
    rim_hit = bandpass_filter(white_noise(rim_n), 1200, q=2.0) * np.exp(-25 * t_rim) * 0.7
    
    for beat in range(total_beats):
        onset = int(beat * beat_s * SR)
        if onset >= n: break
        
        if beat % 2 == 0:
            rem = min(taiko_n, n - onset)
            left[onset:onset+rem] += taiko_hit[:rem] * 0.85
            right[onset:onset+rem] += taiko_hit[:rem] * 0.85
            
        eighth_onset = onset + int(beat_s * 0.5 * SR)
        if eighth_onset < n:
            rem = min(rim_n, n - eighth_onset)
            left[eighth_onset:eighth_onset+rem] += rim_hit[:rem] * 0.45
            right[eighth_onset:eighth_onset+rem] += rim_hit[:rem] * 0.45
            
    cello_freqs = [73.42, 65.41, 58.27, 55.0]
    for bar in range(int(duration / (beat_s * 4))):
        f = cello_freqs[bar % len(cello_freqs)]
        bar_onset = int(bar * beat_s * 4 * SR)
        bar_len = int(beat_s * 4 * SR)
        t_bar = np.linspace(0, beat_s * 4, bar_len)
        saw = (np.mod(f * t_bar, 1.0) * 2 - 1)
        pulse = lowpass_filter(saw, 380) * (0.8 + 0.2 * np.sin(2 * np.pi * (1.0/beat_s) * t_bar)) * 0.35
        rem = min(bar_len, n - bar_onset)
        left[bar_onset:bar_onset+rem] += pulse[:rem]
        right[bar_onset:bar_onset+rem] += pulse[:rem]
        
    tension_drone = np.sin(2 * np.pi * 587.33 * t) * 0.05 + np.sin(2 * np.pi * 622.25 * t) * 0.05
    left += tension_drone
    right += np.roll(tension_drone, 30)
    
    seam_len = int(SR * 1.5)
    fade_in = np.linspace(0, 1, seam_len)
    fade_out = np.linspace(1, 0, seam_len)
    left[:seam_len] = left[:seam_len] * fade_in + left[-seam_len:] * fade_out
    right[:seam_len] = right[:seam_len] * fade_in + right[-seam_len:] * fade_out
    
    stereo = create_stereo(left, right)
    return simple_reverb(stereo, decay=0.4, wet=0.25)

# ==========================================
# MAIN BATCH GENERATOR
# ==========================================

def main():
    sfx_dir = "Assets/_Duskborn/Art/SFX"
    music_dir = "Assets/_Duskborn/Audio/Music"
    
    print("=== Generating Duskborn Release-Quality Audio Assets ===")
    
    # 1. Weapon Swings
    save_wav(f"{sfx_dir}/swing_light_01.wav", gen_swing_light(1))
    save_wav(f"{sfx_dir}/swing_light_02.wav", gen_swing_light(2))
    save_wav(f"{sfx_dir}/swing_light_03.wav", gen_swing_light(3))
    
    save_wav(f"{sfx_dir}/swing_heavy_01.wav", gen_swing_heavy(1))
    save_wav(f"{sfx_dir}/swing_heavy_02.wav", gen_swing_heavy(2))
    save_wav(f"{sfx_dir}/swing_heavy_03.wav", gen_swing_heavy(3))
    
    save_wav(f"{sfx_dir}/axe_swing.wav", gen_swing_light(1))
    save_wav(f"{sfx_dir}/weapon_swing_1.wav", gen_swing_light(2))
    save_wav(f"{sfx_dir}/weapon_swing_2.wav", gen_swing_light(3))
    
    # 2. Impacts & Surface Hits
    save_wav(f"{sfx_dir}/hit_flesh_01.wav", gen_hit_flesh(1))
    save_wav(f"{sfx_dir}/hit_flesh_02.wav", gen_hit_flesh(2))
    save_wav(f"{sfx_dir}/hit_flesh_03.wav", gen_hit_flesh(3))
    save_wav(f"{sfx_dir}/punch_3.wav", gen_hit_flesh(1))
    
    save_wav(f"{sfx_dir}/hit_wood_01.wav", gen_hit_wood(1))
    save_wav(f"{sfx_dir}/hit_wood_02.wav", gen_hit_wood(2))
    save_wav(f"{sfx_dir}/hit_wood_03.wav", gen_hit_wood(3))
    save_wav(f"{sfx_dir}/axe_hit_wood.wav", gen_hit_wood(1))
    
    save_wav(f"{sfx_dir}/hit_stone_01.wav", gen_hit_stone(1))
    save_wav(f"{sfx_dir}/hit_stone_02.wav", gen_hit_stone(2))
    save_wav(f"{sfx_dir}/hit_stone_03.wav", gen_hit_stone(3))
    
    save_wav(f"{sfx_dir}/hit_metal_01.wav", gen_hit_metal(1))
    save_wav(f"{sfx_dir}/hit_metal_02.wav", gen_hit_metal(2))
    save_wav(f"{sfx_dir}/hit_metal_03.wav", gen_hit_metal(3))
    
    save_wav(f"{sfx_dir}/hit_default_01.wav", gen_hit_default(1))
    save_wav(f"{sfx_dir}/hit_default_02.wav", gen_hit_default(2))
    
    # 3. Locomotion & Vitals
    save_wav(f"{sfx_dir}/footstep_grass_01.wav", gen_footstep_grass(1))
    save_wav(f"{sfx_dir}/footstep_grass_02.wav", gen_footstep_grass(2))
    save_wav(f"{sfx_dir}/footstep_grass_03.wav", gen_footstep_grass(3))
    save_wav(f"{sfx_dir}/footstep_grass_04.wav", gen_footstep_grass(4))
    
    save_wav(f"{sfx_dir}/footstep_stone_01.wav", gen_footstep_stone(1))
    save_wav(f"{sfx_dir}/footstep_stone_02.wav", gen_footstep_stone(2))
    save_wav(f"{sfx_dir}/footstep_stone_03.wav", gen_footstep_stone(3))
    
    save_wav(f"{sfx_dir}/jump_takeoff.wav", gen_jump_takeoff())
    save_wav(f"{sfx_dir}/jump_land.wav", gen_jump_land())
    
    save_wav(f"{sfx_dir}/player_hurt_01.wav", gen_player_hurt(1))
    save_wav(f"{sfx_dir}/player_hurt_02.wav", gen_player_hurt(2))
    save_wav(f"{sfx_dir}/player_death.wav", gen_player_death())
    save_wav(f"{sfx_dir}/heartbeat_loop.wav", gen_heartbeat_loop())
    
    # 4. Enemies (Swarmer)
    save_wav(f"{sfx_dir}/enemy_swarmer_attack_01.wav", gen_swarmer_attack(1))
    save_wav(f"{sfx_dir}/enemy_swarmer_attack_02.wav", gen_swarmer_attack(2))
    save_wav(f"{sfx_dir}/enemy_swarmer_hurt_01.wav", gen_swarmer_hurt(1))
    save_wav(f"{sfx_dir}/enemy_swarmer_hurt_02.wav", gen_swarmer_hurt(2))
    save_wav(f"{sfx_dir}/enemy_swarmer_death_01.wav", gen_swarmer_death(1))
    save_wav(f"{sfx_dir}/enemy_swarmer_death_02.wav", gen_swarmer_death(2))
    
    # 5. Environment & Loot
    save_wav(f"{sfx_dir}/chest_open.wav", gen_chest_open())
    save_wav(f"{sfx_dir}/tree_fall.wav", gen_tree_fall())
    save_wav(f"{sfx_dir}/rock_shatter.wav", gen_rock_shatter())
    save_wav(f"{sfx_dir}/gold_pickup.wav", gen_gold_pickup())
    save_wav(f"{sfx_dir}/gold_sfx.wav", gen_gold_pickup())
    save_wav(f"{sfx_dir}/item_pickup.wav", gen_item_pickup())
    save_wav(f"{sfx_dir}/pickup_sfx.wav", gen_item_pickup())
    
    # 6. UI & Stingers
    save_wav(f"{sfx_dir}/ui_button_click.wav", gen_ui_button_click())
    save_wav(f"{sfx_dir}/ui_modal_open.wav", gen_ui_modal_open())
    save_wav(f"{sfx_dir}/ui_error.wav", gen_ui_error())
    save_wav(f"{sfx_dir}/dawn_horn.wav", gen_dawn_horn())
    save_wav(f"{sfx_dir}/night_horn.wav", gen_night_horn())
    
    # 7. Adaptive Music & Ambience Tracks
    save_wav(f"{music_dir}/music_main_menu.wav", gen_music_main_menu())
    save_wav(f"{music_dir}/music_day_exploration.wav", gen_music_day_exploration())
    save_wav(f"{music_dir}/music_night_combat.wav", gen_music_night_combat())
    
    print("\nAll Duskborn audio assets generated successfully!")

if __name__ == "__main__":
    main()
