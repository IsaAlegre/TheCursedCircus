"""Síntesis de la música y los efectos de sonido de The Cursed Circus (Nivel 1).
Todo se genera por código (sin muestras de terceros): no hay licencias que acreditar.
"""
import numpy as np
from scipy import signal
from scipy.io import wavfile
import subprocess, os

SR = 44100
OUT = os.environ.get("OUT", "assets/audio")
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(13)


def t_(dur):
    return np.arange(int(dur * SR)) / SR


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def env_adsr(n, a=0.01, d=0.05, s=0.7, r=0.08):
    e = np.ones(n) * s
    na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    na = min(na, n); e[:na] = np.linspace(0, 1, na)
    nd = min(nd, n - na); e[na:na + nd] = np.linspace(1, s, nd)
    nr = min(nr, n); e[n - nr:] *= np.linspace(1, 0, nr)
    return e


def bp(x, f, q):
    b, a = signal.iirpeak(f, q, SR)
    return signal.lfilter(b, a, x)


def lp(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def hp(x, f, order=2):
    b, a = signal.butter(order, f / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def reverb(x, dur=1.6, mezcla=0.25, brillo=4000):
    n = int(dur * SR)
    ir = rng.standard_normal(n) * np.exp(-np.linspace(0, 7, n))
    ir = lp(ir, brillo)
    ir /= np.sqrt(np.sum(ir ** 2))
    seco = np.concatenate([x, np.zeros(n)])
    mojado = np.zeros_like(seco)
    conv = signal.fftconvolve(x, ir)[: len(seco)]
    mojado[: len(conv)] = conv
    return (1 - mezcla) * seco + mezcla * mojado


def normalizar(x, pico=0.89):
    return x / (np.max(np.abs(x)) + 1e-9) * pico


def guardar_wav(nombre, x):
    wavfile.write(f"{OUT}/{nombre}.wav", SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))


# ---------------------------------------------------------------------------
# Instrumentos
# ---------------------------------------------------------------------------

def caliope(freq, dur, amp=1.0):
    """Órgano de feria desafinado: armónicos + vibrato + dos voces levemente separadas."""
    t = t_(dur)
    desafine = 2 ** (rng.uniform(-22, 22) / 1200)  # cada nota sale un poco fuera de tono
    vib = 1 + 0.005 * np.sin(2 * np.pi * 5.3 * t + rng.uniform(0, 6))
    out = np.zeros_like(t)
    for voz in (-7, 7):
        f = freq * desafine * 2 ** (voz / 1200) * vib
        fase = 2 * np.pi * np.cumsum(f) / SR
        for h, a in [(1, 1.0), (2, 0.55), (3, 0.35), (4, 0.2), (6, 0.1), (8, 0.05)]:
            out += a * np.sin(h * fase)
    # "aire" del tubo
    out += 0.04 * lp(rng.standard_normal(len(t)), 3000)
    return amp * out * env_adsr(len(t), 0.012, 0.06, 0.75, 0.06)


def tuba(freq, dur, amp=1.0):
    t = t_(dur)
    fase = 2 * np.pi * freq * t
    x = np.sin(fase) + 0.5 * np.sin(2 * fase) + 0.25 * np.sin(3 * fase)
    return amp * lp(x, 900) * env_adsr(len(t), 0.02, 0.1, 0.5, 0.08)


def caja_musica(freq, dur, amp=1.0):
    t = t_(dur)
    x = np.sin(2 * np.pi * freq * t) + 0.35 * np.sin(2 * np.pi * freq * 2.76 * t) * np.exp(-t * 9)
    return amp * x * np.exp(-t * 3.2) * env_adsr(len(t), 0.002, 0.01, 1.0, 0.03)


def cuerdas(freqs, dur, amp=1.0):
    """Colchón de cuerdas disonantes (sierras filtradas con trémolo lento)."""
    t = t_(dur)
    out = np.zeros_like(t)
    for f in freqs:
        for d in (-6, 0, 6):
            ff = f * 2 ** (d / 1200)
            out += signal.sawtooth(2 * np.pi * ff * t + rng.uniform(0, 6))
    out = lp(out, 1400)
    trem = 0.75 + 0.25 * np.sin(2 * np.pi * 0.35 * t)
    return amp * out * trem * env_adsr(len(t), 0.6, 0.2, 1.0, 0.6)


# ---------------------------------------------------------------------------
# Música: vals de circo maldito en re menor
# ---------------------------------------------------------------------------

def musica():
    beat = 0.42
    compas = 3 * beat
    # Acordes: (bajo, acorde de acompañamiento)
    A = {
        "Dm": (38, [57, 62, 65]), "Edim": (40, [55, 58, 64]), "A7": (45, [55, 61, 64]),
        "A": (45, [57, 61, 64]), "Gm": (43, [55, 58, 62]), "Bb": (46, [53, 58, 62]),
    }
    melodia = [
        [(69, 1), (70, 1), (69, 1)], [(68, 2), (69, 1)], [(65, 1), (64, 1), (62, 1)], [(61, 3)],
        [(62, 1), (65, 1), (69, 1)], [(70, 2), (69, 1)], [(67, 1), (65, 1), (64, 1)], [(69, 3)],
        [(74, 1), (73, 1), (74, 1)], [(70, 2), (67, 1)], [(69, 1), (68, 1), (69, 1)], [(65, 3)],
        [(64, 1), (65, 1), (68, 1)], [(69, 1), (70, 1), (69, 1)], [(68, 1), (65, 1), (64, 1)], [(62, 3)],
    ]
    acordes = ["Dm", "Edim", "Dm", "A7", "Dm", "Gm", "A7", "A",
               "Dm", "Gm", "A7", "Dm", "Edim", "Bb", "A7", "Dm"]

    vueltas = 2
    total = vueltas * len(melodia) * compas
    pista = np.zeros(int(total * SR) + SR)

    def poner(x, inicio):
        i = int(inicio * SR)
        pista[i:i + len(x)] += x[: len(pista) - i]

    for v in range(vueltas):
        for c, (notas, ac) in enumerate(zip(melodia, acordes)):
            t0 = (v * len(melodia) + c) * compas
            bajo, triada = A[ac]
            # um-pa-pa
            poner(tuba(mtof(bajo), beat * 0.9, 0.55), t0)
            for b in (1, 2):
                for m in triada:
                    poner(caliope(mtof(m), beat * 0.45, 0.10), t0 + b * beat)
            # melodía en el órgano
            tt = t0
            for m, dur in notas:
                poner(caliope(mtof(m), dur * beat * 0.95, 0.30), tt)
                if v == 1:  # segunda vuelta: caja de música una octava arriba, desafinada
                    poner(caja_musica(mtof(m + 12) * 2 ** (rng.uniform(-30, 30) / 1200), 1.2, 0.22), tt)
                tt += dur * beat
            # cuerdas disonantes cada 4 compases, con novena menor encima
            if c % 4 == 0:
                cuerda = [mtof(bajo + 12), mtof(bajo + 12 + 6), mtof(bajo + 24 + 1)]
                poner(cuerdas(cuerda, compas * 4, 0.035 if v == 0 else 0.05), t0)

    # Zumbido grave continuo (re + sol#: tritono)
    t = t_(len(pista) / SR)
    zumbido = (np.sin(2 * np.pi * mtof(26) * t) + 0.6 * np.sin(2 * np.pi * mtof(32) * t))
    zumbido *= 0.06 * (0.7 + 0.3 * np.sin(2 * np.pi * 0.11 * t))
    pista += zumbido

    pista = pista[: int(total * SR)]
    # Reverb circular para que el loop no tenga corte
    con_rev = reverb(pista, 2.2, 0.3, 3500)
    cola = con_rev[len(pista):]
    loop = con_rev[: len(pista)].copy()
    loop[: len(cola)] += cola
    loop = normalizar(loop, 0.8)
    guardar_wav("musica_circo", loop)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", f"{OUT}/musica_circo.wav",
                    "-c:a", "libvorbis", "-q:a", "5", f"{OUT}/musica_circo.ogg"], check=True)
    os.remove(f"{OUT}/musica_circo.wav")
    return total


# ---------------------------------------------------------------------------
# Efectos (GDD, tabla de VFX/SFX)
# ---------------------------------------------------------------------------

def barrido_sen(f0, f1, dur, curva=3.0):
    t = t_(dur)
    f = f1 + (f0 - f1) * np.exp(-curva * t / dur)
    return np.sin(2 * np.pi * np.cumsum(f) / SR)


def voz(f0s, dur, formantes, aire=0.15):
    """Fuente glotal (pulsos ricos en armónicos) filtrada por formantes."""
    t = t_(dur)
    f0 = np.interp(t, np.linspace(0, dur, len(f0s)), f0s)
    f0 *= 1 + 0.02 * rng.standard_normal(len(t)).cumsum() / np.sqrt(np.arange(1, len(t) + 1))
    fase = np.cumsum(f0) / SR
    fuente = signal.sawtooth(2 * np.pi * fase, 0.05) + aire * rng.standard_normal(len(t))
    out = sum(g * bp(fuente, f, q) for f, q, g in formantes)
    return out


def disparo():
    """Proyectil Arcano: estallido grave con eco corto."""
    d = 0.5
    boom = barrido_sen(140, 45, d, 5) * np.exp(-t_(d) * 9)
    ruido = rng.standard_normal(int(d * SR))
    sweep = np.zeros_like(ruido)
    for i, f in enumerate(np.geomspace(2500, 500, 10)):
        seg = slice(int(i * len(ruido) / 10), int((i + 1) * len(ruido) / 10))
        sweep[seg] = bp(ruido, f, 3)[seg]
    sweep *= np.exp(-t_(d) * 7)
    brillo = barrido_sen(900, 1600, d, 2) * np.exp(-t_(d) * 12) * 0.25
    x = 0.9 * boom + 0.5 * sweep + brillo
    eco = np.zeros(len(x) + int(0.11 * SR)); eco[: len(x)] += x; eco[int(0.11 * SR):] += 0.35 * x
    return normalizar(reverb(eco, 0.6, 0.2), 0.85)


def impacto_enemigo():
    d = 0.25
    clic = rng.standard_normal(int(0.004 * SR))
    golpe = barrido_sen(220, 60, d, 6) * np.exp(-t_(d) * 16)
    crujido = hp(rng.standard_normal(int(d * SR)), 1500) * np.exp(-t_(d) * 30) * 0.4
    x = golpe + crujido
    x[: len(clic)] += clic
    return normalizar(x, 0.8)


def danio_mago():
    """Golpe sordo + gruñido contenido."""
    d = 0.45
    golpe = barrido_sen(110, 35, d, 5) * np.exp(-t_(d) * 8)
    grunido = voz([140, 115, 100], 0.22, [(450, 6, 1.0), (1000, 6, 0.5), (2400, 8, 0.2)], 0.3)
    grunido *= env_adsr(len(grunido), 0.01, 0.05, 0.6, 0.1)
    x = golpe.copy()
    x[int(0.02 * SR): int(0.02 * SR) + len(grunido)] += 0.35 * grunido / np.max(np.abs(grunido))
    return normalizar(x, 0.85)


def bloqueo():
    """Crash metálico nítido."""
    d = 1.0
    t = t_(d)
    base = 520
    x = np.zeros_like(t)
    for r, a, dec in [(1, 1, 4), (2.32, 0.7, 5), (4.25, 0.5, 7), (6.63, 0.35, 9), (8.95, 0.25, 12), (11.3, 0.2, 15)]:
        x += a * np.sin(2 * np.pi * base * r * t + rng.uniform(0, 6)) * np.exp(-t * dec)
    x += 0.8 * hp(rng.standard_normal(len(t)), 3000) * np.exp(-t * 25)
    x *= np.linspace(1, 0, len(x)) ** 0.5  # sin corte brusco al final
    return normalizar(reverb(x, 0.8, 0.2), 0.8)


def muerte_enemigo():
    """Lamento grave que se apaga."""
    d = 1.2
    lamento = voz([150, 135, 110, 80, 65], d, [(520, 5, 1.0), (850, 6, 0.6), (2300, 8, 0.15)], 0.08)
    lamento *= (0.8 + 0.2 * np.sin(2 * np.pi * 6 * t_(d))) * env_adsr(int(d * SR), 0.05, 0.2, 0.8, 0.6)
    ruido = lp(rng.standard_normal(int(d * SR)), 300) * np.exp(-t_(d) * 2.5) * 0.6
    x = lamento / np.max(np.abs(lamento)) + ruido
    return normalizar(reverb(x, 1.2, 0.3), 0.8)


def risa():
    """Risa de payaso maldito: 'ja-ja-ja' que baja de tono, doblada por una voz una octava abajo."""
    vocal_a = [(780, 6, 1.0), (1180, 7, 0.7), (2500, 9, 0.25)]
    vocal_a_grave = [(650, 6, 1.0), (1000, 7, 0.6), (2200, 9, 0.15)]
    partes = []
    for i in range(6):
        f0 = 300 - i * 22 + rng.uniform(-10, 10)
        dur = 0.12 + rng.uniform(-0.01, 0.02)
        nh = int(0.03 * SR)
        h = hp(rng.standard_normal(nh), 900) * 0.3 * np.linspace(1, 0.2, nh)
        agudo = voz([f0 * 1.08, f0, f0 * 0.9], dur, vocal_a, 0.08)
        grave = voz([f0 * 0.54, f0 * 0.5, f0 * 0.45], dur, vocal_a_grave, 0.05)
        a = agudo / np.max(np.abs(agudo)) + 0.6 * grave / np.max(np.abs(grave))
        a *= env_adsr(len(a), 0.008, 0.03, 0.7, 0.05)
        partes += [h, a, np.zeros(int(rng.uniform(0.05, 0.075) * SR))]
    x = np.concatenate(partes)
    x *= np.linspace(1, 0.5, len(x))
    return normalizar(reverb(x, 1.0, 0.25, 3000), 0.85)


if __name__ == "__main__":
    print("música:", round(musica(), 1), "s")
    for nombre, f in [("disparo_arcano", disparo), ("impacto_enemigo", impacto_enemigo),
                      ("danio_mago", danio_mago), ("bloqueo_perfecto", bloqueo),
                      ("muerte_enemigo", muerte_enemigo), ("risa_payaso", risa)]:
        guardar_wav(nombre, f())
        print(nombre, "ok")
