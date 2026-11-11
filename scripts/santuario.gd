extends Area2D
## Santuario (GDD): punto de descanso y checkpoint.
##
## Cuando el Mago lo toca:
##   1. El orbe "despierta": se ilumina de a poco y suelta una onda de luz.
##   2. Chispas de energía viajan del orbe hacia el Mago.
##   3. Al llegar, la vida se recupera corazón por corazón y el maná se llena de a poco.
##   4. Queda guardado como punto de reaparición.
## Si se vuelve a pasar con vida o maná incompletos, se repite la curación (sin la onda).
##
## Imagen: res://assets/santuario.png (se carga sola si existe), o arrastrar otra al campo Imagen.
## Colocar el nodo con su origen apoyado en el piso.
## La forma de colisión se crea por código (el aviso amarillo del editor es normal).

const RUTA_IMAGEN := "res://assets/santuario.png"
const CIAN := Color("3FD8EC")
const BLANCO_HUESO := Color("F5F2EA")
const TINTA := Color("151515")
const PIEDRA := Color("3A3330")

@export var imagen: Texture2D
@export var alto_imagen: float = 200.0
## Dónde está el orbe dentro de la imagen (0–1 en ancho y alto, desde arriba a la izquierda).
@export var posicion_orbe: Vector2 = Vector2(0.5, 0.30)
## Radio del orbe como fracción del alto de la imagen.
@export var radio_orbe: float = 0.11
## Ancho de la zona que hay que tocar.
@export var ancho_zona: float = 140.0
## Segundos entre cada corazón que se recupera.
@export var intervalo_corazon: float = 0.35
## Segundos que tarda en llenarse el maná completo.
@export var duracion_mana: float = 1.0
## Opcional: sonido al activarse (por ejemplo bloqueo_perfecto.wav).
@export var sonido_activar: AudioStream

var _activo: bool = false
var _t: float = 0.0
## 0 = apagado, 1 = encendido. Sube de a poco al activarse.
var _encendido: float = 0.0
var _onda: float = 0.0          # 1 → 0: onda de luz que se expande
var _chispas: Array = []        # cada una: {t, dur, desde, ctrl, retraso}

var _jugador: Node2D
var _restaurando: bool = false
var _t_espera: float = 0.0      # hasta que llegan las primeras chispas
var _t_corazon: float = 0.0
var _resto_mana: float = 0.0
var _t_chispa: float = 0.0

var _brillo: Node2D             # capa que suma luz (modo aditivo)


func _ready() -> void:
	if imagen == null and ResourceLoader.exists(RUTA_IMAGEN):
		imagen = load(RUTA_IMAGEN)
	collision_layer = 0
	collision_mask = 0
	set_collision_mask_value(2, true)
	var forma := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(ancho_zona, alto_imagen)
	forma.shape = rect
	forma.position = Vector2(0, -alto_imagen * 0.5)
	add_child(forma)
	body_entered.connect(_al_entrar)

	# La luz se dibuja en un nodo aparte con mezcla aditiva: brilla en vez de tapar.
	_brillo = Node2D.new()
	var material_luz := CanvasItemMaterial.new()
	material_luz.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
	_brillo.material = material_luz
	add_child(_brillo)
	_brillo.draw.connect(_dibujar_luz)

	var partida := get_node_or_null("/root/Partida")
	if partida and partida.hay_checkpoint and partida.id_santuario == String(get_path()):
		_activo = true
		_encendido = 1.0


func _al_entrar(cuerpo: Node) -> void:
	if not cuerpo.is_in_group("jugador"):
		return
	_jugador = cuerpo
	var partida := get_node_or_null("/root/Partida")
	if partida:
		partida.guardar_checkpoint(cuerpo.global_position, String(get_path()))
	if not _activo:
		_activo = true
		_onda = 1.0
		if sonido_activar:
			var p := AudioStreamPlayer.new()
			p.stream = sonido_activar
			add_child(p)
			p.play()
			p.finished.connect(p.queue_free)
	if _le_falta_algo() and not _restaurando:
		_restaurando = true
		_t_espera = 0.5
		_t_corazon = 0.0
		_resto_mana = 0.0
		for i in 10:
			_lanzar_chispa(i * 0.04)


func _le_falta_algo() -> bool:
	if _jugador == null or not is_instance_valid(_jugador):
		return false
	return _jugador.vida < _jugador.vida_maxima or _jugador.mana < _jugador.mana_maximo


func _process(delta: float) -> void:
	_t += delta
	if _activo:
		_encendido = move_toward(_encendido, 1.0, delta / 0.8)
	_onda = move_toward(_onda, 0.0, delta / 0.9)
	_actualizar_curacion(delta)
	_actualizar_chispas(delta)
	queue_redraw()
	_brillo.queue_redraw()


func _actualizar_curacion(delta: float) -> void:
	if not _restaurando:
		return
	if not _le_falta_algo():
		_restaurando = false
		return
	_t_espera -= delta
	if _t_espera > 0.0:
		return
	# Corazones, de a uno.
	_t_corazon -= delta
	if _t_corazon <= 0.0 and _jugador.vida < _jugador.vida_maxima:
		_jugador.curar(1)
		_t_corazon = intervalo_corazon
	# Maná, de a poco.
	if _jugador.mana < _jugador.mana_maximo:
		_resto_mana += _jugador.mana_maximo * delta / duracion_mana
		var entero := int(_resto_mana)
		if entero > 0:
			_resto_mana -= entero
			_jugador.recuperar_mana(entero)
	# Mientras cura, siguen saliendo chispas.
	_t_chispa -= delta
	if _t_chispa <= 0.0:
		_t_chispa = 0.08
		_lanzar_chispa(0.0)


# ------------------------------------------------------------------
# Chispas: viajan en curva desde el orbe hasta el Mago
# ------------------------------------------------------------------

func _centro_orbe() -> Vector2:
	if imagen == null:
		return Vector2(0, -170.0 * 0.72)
	var tam := _tamanio_imagen()
	return Vector2(-tam.x * 0.5, -tam.y) + tam * posicion_orbe


func _lanzar_chispa(retraso: float) -> void:
	var desde := _centro_orbe() + Vector2(randf_range(-10, 10), randf_range(-10, 10))
	var ctrl := desde + Vector2(randf_range(-90, 90), randf_range(-120, -40))
	_chispas.append({"t": 0.0, "dur": randf_range(0.45, 0.7), "desde": desde, "ctrl": ctrl, "retraso": retraso})


func _actualizar_chispas(delta: float) -> void:
	for i in range(_chispas.size() - 1, -1, -1):
		var c: Dictionary = _chispas[i]
		if c.retraso > 0.0:
			c.retraso -= delta
			continue
		c.t += delta
		if c.t >= c.dur:
			_chispas.remove_at(i)


func _posicion_chispa(c: Dictionary) -> Vector2:
	var destino: Vector2 = c.desde
	if _jugador and is_instance_valid(_jugador):
		destino = to_local(_jugador.centro())
	var p: float = ease(c.t / c.dur, 2.0)  # arranca lento y acelera
	var a: Vector2 = c.desde.lerp(c.ctrl, p)
	var b: Vector2 = c.ctrl.lerp(destino, p)
	return a.lerp(b, p)


# ------------------------------------------------------------------
# Dibujo
# ------------------------------------------------------------------

func _tamanio_imagen() -> Vector2:
	return Vector2(alto_imagen * imagen.get_width() / float(imagen.get_height()), alto_imagen)


func _draw() -> void:
	var gris := Color(0.5, 0.5, 0.55)
	var color := gris.lerp(Color.WHITE, _encendido)
	if imagen:
		var tam := _tamanio_imagen()
		draw_texture_rect(imagen, Rect2(Vector2(-tam.x * 0.5, -tam.y), tam), false, color)
		# Orbe apagado: una sombra lo tapa y se va al encenderse.
		if _encendido < 1.0:
			draw_circle(_centro_orbe(), alto_imagen * radio_orbe, Color(0.05, 0.06, 0.08, 0.75 * (1.0 - _encendido)))
	else:
		# Sin imagen: pedestal provisorio.
		draw_rect(Rect2(-34, -18, 68, 18), PIEDRA * color)
		draw_rect(Rect2(-34, -18, 68, 18), TINTA, false, 3.0)
		draw_rect(Rect2(-12, -170 * 0.55, 24, 170 * 0.55 - 18), PIEDRA * color)
		draw_rect(Rect2(-12, -170 * 0.55, 24, 170 * 0.55 - 18), TINTA, false, 3.0)
		draw_circle(_centro_orbe(), 14.0, Color(0.3, 0.3, 0.33).lerp(CIAN, _encendido))


func _dibujar_luz() -> void:
	var orbe := _centro_orbe()
	var r := alto_imagen * radio_orbe if imagen else 14.0
	# Resplandor del orbe: respira suave cuando está encendido.
	if _encendido > 0.0:
		var respiro := 0.5 + 0.5 * sin(_t * 2.2)
		_brillo.draw_circle(orbe, r * (2.6 + 0.3 * respiro), Color(CIAN, 0.06 * _encendido))
		_brillo.draw_circle(orbe, r * (1.6 + 0.15 * respiro), Color(CIAN, 0.12 * _encendido))
		_brillo.draw_circle(orbe, r * 1.05, Color(CIAN, (0.10 + 0.08 * respiro) * _encendido))
	# Onda de activación.
	if _onda > 0.0:
		var avance := 1.0 - _onda
		_brillo.draw_circle(orbe, r * (1.0 + 2.0 * avance), Color(CIAN, 0.5 * _onda))
		_brillo.draw_arc(orbe, r * (1.2 + 7.0 * avance), 0.0, TAU, 48, Color(CIAN, 0.8 * _onda), 4.0 * _onda + 1.0)
	# Chispas con estela.
	for c in _chispas:
		if c.retraso > 0.0:
			continue
		var pos := _posicion_chispa(c)
		_brillo.draw_circle(pos, 9.0, Color(CIAN, 0.25))
		_brillo.draw_circle(pos, 4.0, Color(CIAN, 0.8))
		_brillo.draw_circle(pos, 2.0, BLANCO_HUESO)
