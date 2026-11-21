"""Tanitim videosu icin ozgun arka plan muzigi (telifsiz, tamamen burada sentezlenir).
Sakin ambient/synthwave: Am - F - C - G akorlari, pad, bas, arpej, hafif vurus ve yanki.
Kullanim: python music.py cikis.wav [saniye]
"""
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, lfilter

SR = 44100
out = sys.argv[1]
DUR = float(sys.argv[2]) if len(sys.argv) > 2 else 42.0
BPM = 90.0
BEAT = 60.0 / BPM
BAR = BEAT * 4
n = int(DUR * SR)
t_all = np.arange(n) / SR
L = np.zeros(n)
R = np.zeros(n)


def hz(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


# akorlar (MIDI): Am, F, C, G
CHORDS = [[57, 60, 64], [53, 57, 60], [48, 52, 55], [55, 59, 62]]
ROOTS = [45, 41, 36, 43]


def add(sig, start, gain_l, gain_r):
    s = int(start * SR)
    if s >= n:
        return
    e = min(n, s + len(sig))
    L[s:e] += sig[: e - s] * gain_l
    R[s:e] += sig[: e - s] * gain_r


def env(length, a, r):
    t = np.arange(int(length * SR)) / SR
    e = np.minimum(1, t / a) * np.minimum(1, (length - t) / r)
    return np.clip(e, 0, 1)


def pad_voice(f, length):
    t = np.arange(int(length * SR)) / SR
    s = np.zeros_like(t)
    for k, amp in enumerate([1, 0.45, 0.25, 0.12, 0.06], 1):
        for det in (-0.12, 0.12):  # hafif akort farki = genis ses
            s += amp * np.sin(2 * np.pi * f * k * (1 + det / 100) * t + k)
    return s * env(length, 1.2, 1.4)


def pluck(f, length=0.9):
    t = np.arange(int(length * SR)) / SR
    s = np.sin(2 * np.pi * f * t) + 0.3 * np.sin(2 * np.pi * 2 * f * t) + 0.1 * np.sin(2 * np.pi * 3 * f * t)
    return s * np.exp(-t * 5.5) * np.minimum(1, t / 0.004)


def kick(length=0.35):
    t = np.arange(int(length * SR)) / SR
    f = 50 + 70 * np.exp(-t * 30)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 9)


bars = int(np.ceil(DUR / BAR))
for b in range(bars):
    ci = b % 4
    t0 = b * BAR
    # pad
    for m in CHORDS[ci]:
        v = pad_voice(hz(m), BAR + 1.2) * 0.05
        add(v, t0, 0.9, 1.0)
    # bas (2. bolumden itibaren)
    if b >= 2:
        t = np.arange(int(BAR * SR)) / SR
        bass = np.sin(2 * np.pi * hz(ROOTS[ci]) * t) * env(BAR, 0.05, 0.4) * 0.16
        add(bass, t0, 1, 1)
    # arpej (4. olcuden itibaren, sekizlikler)
    if b >= 3 and t0 < DUR - BAR:
        notes = CHORDS[ci] + [CHORDS[ci][0] + 12, CHORDS[ci][1] + 12]
        pattern = [0, 1, 2, 3, 4, 3, 2, 1]
        for i, p in enumerate(pattern):
            m = notes[p] + 12
            pan = 0.35 + 0.3 * (i % 2)
            add(pluck(hz(m)) * 0.07, t0 + i * BEAT / 2, 1 - pan * 0.6, 0.4 + pan * 0.6)
    # yumusak vurus (5. olcuden itibaren, son olcude yok)
    if 4 <= b and t0 < DUR - BAR * 1.2:
        for k in range(4):
            add(kick() * 0.22, t0 + k * BEAT, 1, 1)

# yanki (sentetik oda tepkisi)
ir_t = np.arange(int(2.6 * SR)) / SR
rng = np.random.default_rng(3)
irL = rng.standard_normal(len(ir_t)) * np.exp(-ir_t * 2.4)
irR = rng.standard_normal(len(ir_t)) * np.exp(-ir_t * 2.4)
b_, a_ = butter(2, 5000 / (SR / 2), "low")
irL, irR = lfilter(b_, a_, irL), lfilter(b_, a_, irR)
wetL = fftconvolve(L, irL)[:n] * 0.035
wetR = fftconvolve(R, irR)[:n] * 0.035
L2, R2 = L + wetL, R + wetR

# giris ve cikis sonmesi, normalize
fade = np.ones(n)
fi, fo = int(2.0 * SR), int(3.5 * SR)
fade[:fi] = np.linspace(0, 1, fi)
fade[-fo:] = np.linspace(1, 0, fo) ** 1.5
L2 *= fade
R2 *= fade
peak = max(np.abs(L2).max(), np.abs(R2).max())
st = np.stack([L2, R2], 1) / peak * 0.85
wavfile.write(out, SR, (st * 32767).astype(np.int16))
print("ok", out, DUR, "s")
