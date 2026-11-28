"""Generates Assets/Resources/Audio/alarm_0.wav: the warning beep a robot gives just
before it blows up. A short electronic tone with a soft metallic echo."""
import numpy as np, wave, os

sr = 44100; dur = 0.2
t = np.arange(int(sr * dur)) / sr
f = 1480 * (1 - 0.02 * t / dur)
ph = 2 * np.pi * np.cumsum(f) / sr
sig = np.tanh(1.8 * (np.sin(ph) + 0.35 * np.sin(2 * ph) + 0.18 * np.sin(3 * ph)))
env = np.minimum(1, t / 0.004) * np.where(t < 0.13, 1.0, np.exp(-(t - 0.13) / 0.018))
sig = np.concatenate([sig * env, np.zeros(int(sr * 0.12))])
out = sig.copy()
for d, g in ((0.023, 0.28), (0.041, 0.18), (0.067, 0.1)):
    n = int(d * sr); out[n:] += g * sig[:-n]
out /= np.max(np.abs(out)) * 1.15
path = os.path.join(os.path.dirname(__file__), '..', 'Assets', 'Resources', 'Audio', 'alarm_0.wav')
w = wave.open(path, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
w.writeframes((out * 32767).astype('<i2').tobytes()); w.close()
