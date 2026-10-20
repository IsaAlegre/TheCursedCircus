# The Cursed Circus

Videojuego de plataformas 2D de vista lateral con combate mágico, ambientado en las ruinas de un circo gótico consumido por una maldición antigua.

## Historia

Un poderoso mago queda atrapado en las ruinas de un circo gótico. Para recuperar su libertad y su cordura tiene que adentrarse en la carpa, sobrevivir a los artistas corrompidos y purificar el núcleo de la maldición.

## Cómo se juega

- **Plataformeo táctico:** saltos precisos, esquivas y caídas a través de plataformas semi-sólidas.
- **Combate mágico progresivo:** los hechizos se desbloquean a medida que se avanza y se alternan en tiempo real según la situación (ofensivo, defensivo o de bloqueo).
- **Exploración por zonas:** Entrada, Las Jaulas, Camerinos, Arena Central y Corazón Maldito, cada una con enemigos propios.
- **Cuatro jefes:** Domador Corrompido, Bailarina Espectral, Payaso Devorador y el Director.
- **Maná y santuarios:** la energía mágica es limitada y se restaura en los puntos de descanso.
- **Sin tutoriales explícitos:** las mecánicas se aprenden con el entorno.

Una partida dura entre 30 y 45 minutos. Plataforma: PC.

## Estilo visual

Ilustración 2D dibujada a mano con trazo grueso de tinta (*Heavy Inking*), con una paleta apagada de tonos tierra. Los acentos arcanos (cian, violeta y naranja) se reservan para el jugador, el maná y las recompensas. Los escenarios usan parallax de tres capas y una grilla de tiles de 64 × 64 px.

## Cómo ejecutarlo

Requisitos: [Godot 4](https://godotengine.org/download) (desarrollado con la versión 4.7.2).

1. Cloná el repositorio:
   ```bash
   git clone https://github.com/IsaAlegre/TheCursedCircus.git
   ```
2. Abrí Godot, elegí **Importar** y seleccioná el archivo `project.godot`.
3. Abrí la escena `nivel_1.tscn` y presioná **F6** para ejecutarla.

## Controles

| Acción | Tecla |
|---|---|
| Moverse | ← / → |
| Saltar | Espacio o ↑ |

## Estructura del proyecto

```
TheCursedCircus/
├── assets/      # imágenes: fondos, tiles, personajes y objetos
├── scenes/      # escenas de Godot
├── scripts/     # scripts en GDScript
└── project.godot
```

## Créditos

- Diseño, programación y dirección de arte: **Isabella Alegre y Lucia Benitez**.
- Gran parte del arte se generó con herramientas de inteligencia artificial y fue editado y adaptado para el proyecto.

## Licencia

Proyecto académico desarrollado para la carrera de Ingeniería en Sistemas. Todos los derechos reservados: el código y los recursos no pueden reutilizarse sin autorización de la autora.
