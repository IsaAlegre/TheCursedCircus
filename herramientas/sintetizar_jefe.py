"""Audio del Domador Corrompido (Experimento BOSS), sintetizado por código.
Usa las mismas funciones que herramientas/sintetizar_audio.py.
Ejecutar desde la carpeta del proyecto: python3 herramientas/sintetizar_jefe.py
"""
import os, sys, subprocess
import numpy as np
from scipy import signal

sys.path.insert(0, os.path.dirname(__file__))
os.environ.setdefault("OUT", "assets/audio/jefe")
import sintetizar_audio as base
from sintetizar_audio import (SR, t_, mtof, env_adsr, bp, lp, hp, reverb, normalizar,
                              guardar_wav, caliope, tuba, cuerdas, barrido_sen, voz)

OUT = base.OUT
rng = np.random.default_rng(31)


def recortar_cola(x, umbral_db=-55):
    idx = np.where(np.abs(x) > 10 ** (umbral_db / 20))[0]
    x = x[: idx[-1] + int(0.05 * SR)] if len(idx) else x
    n = int(0.04 * SR)
    x[-n:] *= np.linspace(1, 0, n)
    return x


# ---------------------------------------------------------------------------
# Efectos (Tabla 4 del documento)
# ---------------------------------------------------------------------------

def tell_latigazo():
    """Silbido del látigo girando en el aire, que crece hasta el golpe."""
    d = 0.7
    t = t_(d)
    ruido = rng.standard_normal(len(t))
    giro = 0.5 + 0.5 * np.sin(2 * np.pi * np.cumsum(np.linspace(4, 11, len(t))) / SR)
    x = np.zeros_like(t)
    for i, f in enumerate(np.geomspace(700, 2600, 12)):
        seg = slice(int(i * len(t) / 12), int((i + 1) * len(t) / 12))
        x[seg] = bp(ruido, f, 4)[seg]
    x *= giro * np.linspace(0.2, 1, len(t)) ** 1.5
    return normalizar(x, 0.7)


def latigazo():
    """Chasquido seco."""
    d = 0.35
    t = t_(d)
    x = hp(rng.standard_normal(len(t)), 2500) * np.exp(-t * 60)
    x[:int(0.002 * SR)] += 1.0
    x += 0.4 * barrido_sen(1800, 300, d, 8) * np.exp(-t * 40)
    return normalizar(reverb(x, 0.4, 0.15), 0.9)


def rugido(dur=1.1, grave=1.0):
    """Rugido de bestia: voz grave, áspera y con mucho aire."""
    f0 = np.array([85, 110, 100, 80, 65]) * grave
    x = voz(f0, dur, [(420, 4, 1.0), (900, 4, 0.7), (2100, 6, 0.3)], 0.9)
    aspereza = 1 + 0.6 * lp(rng.standard_normal(int(dur * SR)), 60)
    x = x / np.max(np.abs(x)) * aspereza * env_adsr(int(dur * SR), 0.06, 0.2, 0.85, 0.35)
    x += 0.5 * lp(rng.standard_normal(len(x)), 500) * env_adsr(len(x), 0.1, 0.2, 0.6, 0.3)
    return normalizar(reverb(x, 1.2, 0.3, 2500), 0.85)


def aterrizaje():
    """Golpe sordo con temblor de madera."""
    d = 1.0
    t = t_(d)
    golpe = barrido_sen(70, 30, d, 4) * np.exp(-t * 5)
    madera = bp(rng.standard_normal(len(t)), 900, 2) * np.exp(-t * 18) * 0.6
    astillas = hp(rng.standard_normal(len(t)), 3000) * (rng.random(len(t)) > 0.995) * np.exp(-t * 6)
    temblor = lp(rng.standard_normal(len(t)), 80) * np.exp(-t * 3) * 2.0
    return normalizar(golpe + madera + 0.3 * astillas + temblor, 0.95)


def crepitar(d, crece=True, densidad=(30, 400)):
    t = t_(d)
    dens = np.linspace(*densidad, len(t)) if crece else np.full(len(t), densidad[1])
    chispas = (rng.random(len(t)) < dens / SR).astype(float) * rng.uniform(0.3, 1, len(t))
    chispas = hp(signal.lfilter([1], [1, -0.6], chispas), 1500)
    rugido_fuego = lp(rng.standard_normal(len(t)), 400) * (0.7 + 0.3 * lp(rng.standard_normal(len(t)), 8))
    vol = np.linspace(0.2, 1, len(t)) if crece else np.ones(len(t))
    return (chispas * 1.4 + rugido_fuego) * vol


def tell_onda():
    """Crepitar de fuego que sube de volumen."""
    return normalizar(crepitar(0.8) * env_adsr(int(0.8 * SR), 0.05, 0.1, 1, 0.05), 0.8)


def onda():
    """Rugido de llamas que se desplaza."""
    d = 1.0
    x = crepitar(d, False, (0, 250)) * env_adsr(int(d * SR), 0.02, 0.2, 0.8, 0.5)
    x += 0.8 * barrido_sen(90, 50, d, 2) * np.exp(-t_(d) * 4)
    return normalizar(reverb(x, 0.6, 0.2), 0.85)


def danio_jefe():
    """Gruñido de dolor."""
    x = voz([120, 95, 80], 0.32, [(500, 5, 1.0), (1000, 5, 0.5), (2300, 7, 0.2)], 0.5)
    x = x / np.max(np.abs(x)) * env_adsr(len(x), 0.01, 0.08, 0.6, 0.12)
    golpe = barrido_sen(160, 50, 0.32, 6) * np.exp(-t_(0.32) * 14)
    return normalizar(x + golpe[: len(x)], 0.8)


def transicion():
    """Rugido prolongado y llamarada."""
    r = rugido(2.0, 0.9)
    f = crepitar(2.0) * env_adsr(int(2.0 * SR), 0.6, 0.2, 1, 0.4)
    n = max(len(r), len(f))
    x = np.zeros(n); x[: len(r)] += r; x[: len(f)] += 0.6 * normalizar(f)
    return normalizar(x, 0.9)


def purificacion():
    """Acorde luminoso (re mayor) que rompe el ambiente oscuro: la única recompensa sonora cálida."""
    d = 4.0
    t = t_(d)
    x = np.zeros_like(t)
    for i, m in enumerate([62, 66, 69, 74, 78, 81]):
        f = mtof(m)
        inicio = int(i * 0.07 * SR)
        tt = t[: len(t) - inicio]
        campana = (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2.0 * tt) +
                   0.15 * np.sin(2 * np.pi * f * 3.01 * tt)) * np.exp(-tt * 0.9)
        x[inicio:] += campana
    colchon = sum(np.sin(2 * np.pi * mtof(m) * t + rng.uniform(0, 6)) for m in (50, 57, 62, 66))
    x += 0.35 * colchon * env_adsr(len(t), 0.8, 0.5, 0.8, 1.5)
    x *= np.linspace(1, 0, len(x)) ** 0.7  # sin corte al final
    return normalizar(reverb(x, 2.5, 0.4, 6000), 0.8)


def disparo_fuego():
    """Disparo del Hechizo de Fuego: estallido con crepitar (GDD)."""
    d = 0.5
    t = t_(d)
    boom = barrido_sen(160, 60, d, 6) * np.exp(-t * 10)
    llama = crepitar(d, False, (0, 600)) * np.exp(-t * 6)
    return normalizar(boom + 0.7 * normalizar(llama), 0.85)


# ---------------------------------------------------------------------------
# Música del encuentro: dos capas del mismo largo que suenan sincronizadas
# ---------------------------------------------------------------------------

def tambor(d=0.6, f=55):
    t = t_(d)
    return barrido_sen(f * 2.2, f, d, 8) * np.exp(-t * 7) + 0.3 * lp(rng.standard_normal(len(t)), 300) * np.exp(-t * 20)


def caja(d=0.2):
    t = t_(d)
    return hp(rng.standard_normal(len(t)), 1200) * np.exp(-t * 25) + 0.4 * np.sin(2 * np.pi * 190 * t) * np.exp(-t * 30)


def metal(freq, dur, amp):
    """Metales graves: sierra filtrada con ataque lento."""
    t = t_(dur)
    x = sum(signal.sawtooth(2 * np.pi * freq * 2 ** (d / 1200) * t) for d in (-8, 0, 8))
    brillante, opaco = lp(x, 1500), lp(x, 900)
    mezcla = np.exp(-t * 3)  # el brillo del ataque se apaga
    x = brillante * mezcla + opaco * (1 - mezcla)
    return amp * x * env_adsr(len(t), 0.04, 0.15, 0.7, 0.1)


def musica_jefe():
    bpm = 132
    negra = 60 / bpm
    compases = 16
    total = compases * 4 * negra
    n = int(total * SR)
    capa1 = np.zeros(n + SR)
    capa2 = np.zeros(n + SR)

    def poner(pista, x, inicio):
        i = int(inicio * SR)
        pista[i:i + len(x)] += x[: len(pista) - i]

    # Progresión en re menor: i - VI - VII - V (Dm, Bb, C, A), dos compases cada acorde
    acordes = [(38, [62, 65, 69]), (34, [58, 62, 65]), (36, [60, 64, 67]), (33, [61, 64, 69])] * 2
    melodia = [74, 73, 74, 70, 69, 70, 65, 64, 69, 68, 69, 73, 74, 76, 77, 76]
    for c in range(compases):
        t0 = c * 4 * negra
        bajo, triada = acordes[c // 2]
        # Capa 1: tambores (1 y 3), bajo en ostinato, metales en el 1, organillo con la melodía
        for b, f in ((0, 50), (2, 50), (2.5, 62)):
            poner(capa1, tambor(0.6, f), t0 + b * negra)
        for b in range(8):
            poner(capa1, tuba(mtof(bajo + (12 if b % 2 else 0)), negra * 0.45, 0.35), t0 + b * negra / 2)
        for m in triada:
            poner(capa1, metal(mtof(m - 12), negra * 1.6, 0.06), t0)
        poner(capa1, caliope(mtof(melodia[c]), negra * 3.6, 0.22), t0)
        # Capa 2 (fase 2): organillo acelerado en semicorcheas, caja y cuerdas disonantes
        arpegio = triada + [triada[1] + 12]
        for k in range(16):
            m = arpegio[k % 4] + 12 + (1 if k % 8 == 7 else 0)  # nota "equivocada" cada medio compás
            poner(capa2, caliope(mtof(m), negra / 4 * 0.9, 0.09), t0 + k * negra / 4)
        for b in (1, 3):
            poner(capa2, caja(), t0 + b * negra)
        poner(capa2, caja(0.12) * 0.5, t0 + 3.75 * negra)
        if c % 2 == 0:
            poner(capa2, cuerdas([mtof(bajo + 12), mtof(bajo + 18), mtof(bajo + 25)], 8 * negra, 0.03), t0)

    def cerrar(pista, nombre, pico):
        pista = pista[: n + SR]
        con_rev = reverb(pista[:n], 1.5, 0.25, 4000)
        cola = con_rev[n:]
        loop = con_rev[:n].copy()
        loop[: len(cola)] += cola
        loop = loop / np.max(np.abs(loop)) * pico
        base.guardar_wav(nombre, loop)
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", f"{OUT}/{nombre}.wav",
                        "-c:a", "libvorbis", "-q:a", "5", f"{OUT}/{nombre}.ogg"], check=True)
        os.remove(f"{OUT}/{nombre}.wav")

    cerrar(capa1, "musica_jefe_fase1", 0.8)
    cerrar(capa2, "musica_jefe_fase2", 0.6)
    return total


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("música jefe:", round(musica_jefe(), 1), "s")
    for nombre, f in [("tell_latigazo", tell_latigazo), ("latigazo", latigazo),
                      ("rugido", rugido), ("aterrizaje", aterrizaje), ("tell_onda", tell_onda),
                      ("onda_fuego", onda), ("danio_jefe", danio_jefe), ("transicion", transicion),
                      ("purificacion", purificacion), ("disparo_fuego", disparo_fuego)]:
        guardar_wav(nombre, recortar_cola(f()))
        print(nombre, "ok")
