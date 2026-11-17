extends CharacterBody2D
## El Domador Corrompido: jefe del Nivel 1 (Experimento BOSS).
## Implementa la máquina de estados de la Figura 1 y los valores de la Tabla 1
## del documento de diseño. La arena (arena_jefe.gd) lo despierta con iniciar().

signal vida_cambiada(vida_actual: int, vida_max: int)
signal fase_cambiada(fase: int)
signal murio

enum Estado {
	INACTIVO, PRESENTACION, ACECHO,
	TELL_LATIGAZO, LATIGAZO,
	TELL_SALTO, SALTO, ATERRIZAJE,
	TELL_ONDA, ONDA,
	RECUPERACION, ATURDIDO, TRANSICION, MUERTE,
}

const ONDA_FUEGO := preload("res://scenes/onda_fuego.tscn")
## Aviso del salto: piso agrietado que brilla donde va a caer. Centro de la elipse en y = 31,6 px.
const AVISO_SALTO := preload("res://assets/ui/jefe/aviso_salto.png")
const AVISO_CENTRO_Y := 31.6
const TILE := 64.0

const SFX_TELL_LATIGAZO := preload("res://assets/audio/jefe/tell_latigazo.wav")
const SFX_LATIGAZO := preload("res://assets/audio/jefe/latigazo.wav")
const SFX_RUGIDO := preload("res://assets/audio/jefe/rugido.wav")
const SFX_ATERRIZAJE := preload("res://assets/audio/jefe/aterrizaje.wav")
const SFX_TELL_ONDA := preload("res://assets/audio/jefe/tell_onda.wav")
const SFX_ONDA := preload("res://assets/audio/jefe/onda_fuego.wav")
const SFX_DANIO := preload("res://assets/audio/jefe/danio_jefe.wav")
const SFX_TRANSICION := preload("res://assets/audio/jefe/transicion.wav")
const SFX_PURIFICACION := preload("res://assets/audio/jefe/purificacion.wav")
const SFX_LAMENTO := preload("res://assets/audio/muerte_enemigo.wav")

const NEGRO := Color("151515")
const ROJO_ARDIENTE := Color("E33224")

@export_group("Estadísticas (Tabla 1)")
@export var vida_maxima: int = 300
@export var umbral_fase_2: int = 150
@export var danio: int = 1
@export var velocidad_fase_1: float = 90.0
@export var velocidad_fase_2: float = 130.0

@export_group("Tiempos (s)")
@export var duracion_presentacion: float = 3.0
@export var tell_latigazo: float = 0.7
@export var duracion_latigazo: float = 0.35
@export var tell_salto: float = 0.9
@export var duracion_salto: float = 0.8
@export var tell_onda: float = 0.8
@export var recuperacion_fase_1: float = 0.8
@export var recuperacion_fase_2: float = 0.5
@export var duracion_aturdido: float = 2.0
@export var duracion_transicion: float = 2.0
## Si el Mago se queda lejos este tiempo, el jefe usa un ataque a distancia.
@export var paciencia_acecho: float = 1.4

@export_group("Alcances (tiles de 64 px)")
@export var alcance_latigazo: float = 4.0
@export var zona_impacto_salto: float = 3.0
@export var gravedad: float = 3000.0

@export_group("Sonido")
@export var volumen_efectos_db: float = -2.0

## Bordes de la arena (los define arena_jefe.gd).
var limite_izq: float = -INF
var limite_der: float = INF

var vida: int
var fase: int = 1
var _estado: Estado = Estado.INACTIVO
var _t: float = 0.0
var _t_acecho: float = 0.0
var _t_en_aire: float = 0.0
var _t_sonido_danio: float = 0.0
var _ataques: int = 0
var _dir: int = -1
var _golpe_hecho: bool = false
var _transicion_pendiente: bool = false
var _objetivo_salto: Vector2
## 0 = jaula entera, 1 = jaula destrozada.
var _jaula: float = 0.0
var _jugador: Node2D

@onready var _sprite: AnimatedSprite2D = $Sprite
@onready var _colision: CollisionShape2D = $CollisionShape2D


func _ready() -> void:
	add_to_group("enemigos")
	collision_layer = 0
	set_collision_layer_value(3, true)
	collision_mask = 0
	set_collision_mask_value(1, true)
	vida = vida_maxima
	_sprite.modulate = Color(0.55, 0.55, 0.6)  # en la penumbra de la jaula


func centro() -> Vector2:
	return _colision.global_position


func esta_activo() -> bool:
	return _estado != Estado.INACTIVO and _estado != Estado.MUERTE


## La arena lo llama cuando el Mago entra.
func iniciar() -> void:
	if _estado == Estado.INACTIVO:
		_jugador = get_tree().get_first_node_in_group("jugador")
		_cambiar_estado(Estado.PRESENTACION)


# ------------------------------------------------------------------
# Máquina de estados
# ------------------------------------------------------------------

func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y += gravedad * delta
	_t -= delta
	_t_sonido_danio -= delta

	match _estado:
		Estado.INACTIVO, Estado.MUERTE:
			velocity.x = 0.0
		Estado.PRESENTACION:
			velocity.x = 0.0
			if _t <= 0.0:
				_cambiar_estado(Estado.ACECHO)
		Estado.ACECHO:
			_acechar(delta)
		Estado.TELL_LATIGAZO:
			velocity.x = 0.0
			if _t <= 0.0:
				_cambiar_estado(Estado.LATIGAZO)
		Estado.LATIGAZO:
			velocity.x = 0.0
			# El golpe pega en el segundo cuadro, cuando el látigo está extendido.
			if not _golpe_hecho and _t <= duracion_latigazo - 0.1:
				_golpe_hecho = true
				_golpear_latigazo()
			if _estado == Estado.LATIGAZO and _t <= 0.0:
				_cambiar_estado(Estado.RECUPERACION)
		Estado.TELL_SALTO:
			velocity.x = 0.0
			if _t <= 0.0:
				_saltar()
		Estado.SALTO:
			_t_en_aire += delta
		Estado.ATERRIZAJE, Estado.ONDA, Estado.RECUPERACION:
			velocity.x = 0.0
			if _t <= 0.0:
				if _estado == Estado.RECUPERACION:
					_despues_de_pausa()
				else:
					_cambiar_estado(Estado.RECUPERACION)
		Estado.TELL_ONDA:
			velocity.x = 0.0
			if _t <= 0.0:
				_lanzar_onda()
				_cambiar_estado(Estado.ONDA)
		Estado.ATURDIDO:
			velocity.x = move_toward(velocity.x, 0.0, 1500.0 * delta)
			if _t <= 0.0:
				_despues_de_pausa()
		Estado.TRANSICION:
			velocity.x = 0.0
			if _t <= 0.0:
				_cambiar_estado(Estado.ACECHO)

	move_and_slide()
	global_position.x = clampf(global_position.x, limite_izq, limite_der)

	if _estado == Estado.SALTO and _t_en_aire > 0.15 and is_on_floor():
		_aterrizar()

	_animar()
	queue_redraw()


func _cambiar_estado(nuevo: Estado) -> void:
	_estado = nuevo
	match nuevo:
		Estado.PRESENTACION:
			_t = duracion_presentacion
			_sprite.modulate = Color.WHITE
			var tw := create_tween()
			tw.tween_property(self, "_jaula", 1.0, 0.6).set_ease(Tween.EASE_OUT)
			_sonido(SFX_RUGIDO, 3.0)
			_sacudir(12.0, 0.6)
		Estado.ACECHO:
			_t_acecho = 0.0
		Estado.TELL_LATIGAZO:
			_t = tell_latigazo
			_mirar_al_jugador()
			_sonido(SFX_TELL_LATIGAZO)
		Estado.LATIGAZO:
			_t = duracion_latigazo
			_golpe_hecho = false
			_sonido(SFX_LATIGAZO, 2.0, 0.05)
		Estado.TELL_SALTO:
			_t = tell_salto
			_mirar_al_jugador()
			# Apunta a donde estaba el Mago al empezar el aviso (no lo persigue en el aire).
			var x := clampf(_jugador.centro().x, limite_izq, limite_der) if _jugador else global_position.x
			_objetivo_salto = Vector2(x, global_position.y)
			_sonido(SFX_RUGIDO, -2.0, 0.1)
		Estado.ATERRIZAJE:
			_t = 0.35
		Estado.TELL_ONDA:
			_t = tell_onda
			_mirar_al_jugador()
			_sonido(SFX_TELL_ONDA)
		Estado.ONDA:
			_t = 0.4
		Estado.RECUPERACION:
			_t = recuperacion_fase_1 if fase == 1 else recuperacion_fase_2
		Estado.ATURDIDO:
			_t = duracion_aturdido
		Estado.TRANSICION:
			_t = duracion_transicion
			fase = 2
			_transicion_pendiente = false
			fase_cambiada.emit(2)
			_sonido(SFX_TRANSICION, 3.0)
			_sacudir(10.0, 1.0)
		Estado.MUERTE:
			velocity = Vector2.ZERO
			# Sale de la capa de enemigos (los disparos lo atraviesan), pero sigue apoyado en el piso.
			set_deferred("collision_layer", 0)
			_sonido(SFX_LAMENTO, 0.0, 0.0, 0.6)
			_sonido(SFX_PURIFICACION, 2.0)
			var tw := create_tween()
			tw.tween_interval(1.4)
			tw.tween_callback(func(): murio.emit())
			tw.tween_property(_sprite, "modulate", Color(1, 1, 1, 0), 1.0)


## Después de una recuperación o del aturdimiento: o pasa a la fase 2, o vuelve a acechar.
func _despues_de_pausa() -> void:
	if _transicion_pendiente:
		_cambiar_estado(Estado.TRANSICION)
	else:
		_cambiar_estado(Estado.ACECHO)


func _acechar(delta: float) -> void:
	if _jugador == null or not is_instance_valid(_jugador):
		velocity.x = 0.0
		return
	_mirar_al_jugador()
	_t_acecho += delta
	var distancia := absf(_jugador.centro().x - centro().x)
	var proximo := _ataques + 1
	# Cada tercer ataque es un salto aplastante: no deja quedarse siempre a la misma distancia.
	if proximo % 3 == 0 and _t_acecho > 0.4:
		_ataques += 1
		_cambiar_estado(Estado.TELL_SALTO)
	elif distancia <= alcance_latigazo * TILE * 0.9:
		_ataques += 1
		_cambiar_estado(Estado.TELL_LATIGAZO)
	elif _t_acecho > paciencia_acecho:
		_ataques += 1
		_cambiar_estado(Estado.TELL_ONDA if fase == 2 else Estado.TELL_SALTO)
	else:
		velocity.x = _dir * (velocidad_fase_1 if fase == 1 else velocidad_fase_2)


func _mirar_al_jugador() -> void:
	if _jugador and is_instance_valid(_jugador):
		_dir = 1 if _jugador.centro().x >= centro().x else -1


# ------------------------------------------------------------------
# Ataques
# ------------------------------------------------------------------

func _golpear_latigazo() -> void:
	if _jugador == null:
		return
	var rel: Vector2 = _jugador.centro() - centro()
	var adelante: float = rel.x * _dir
	if adelante > -40.0 and adelante <= alcance_latigazo * TILE + 30.0 and absf(rel.y) < 120.0:
		# Bloqueable: si el Mago hace un bloqueo perfecto, llama a aturdir().
		_jugador.recibir_danio(danio, self)


func _saltar() -> void:
	var dx := _objetivo_salto.x - global_position.x
	velocity = Vector2(dx / duracion_salto, -gravedad * duracion_salto / 2.0)
	_t_en_aire = 0.0
	_estado = Estado.SALTO


func _aterrizar() -> void:
	velocity.x = 0.0
	_sonido(SFX_ATERRIZAJE, 3.0)
	_sacudir(16.0, 0.4)
	if _jugador and is_instance_valid(_jugador):
		var cerca := absf(_jugador.centro().x - global_position.x) <= zona_impacto_salto * TILE / 2.0 + 20.0
		# No se bloquea: se esquiva alejándose (por ejemplo, con el retroceso del disparo).
		if cerca and _jugador.is_on_floor():
			_jugador.recibir_danio(danio, self, false)
	_cambiar_estado(Estado.ATERRIZAJE)


func _lanzar_onda() -> void:
	var onda := ONDA_FUEGO.instantiate()
	onda.direccion = _dir
	onda.danio = danio
	onda.limite_izq = limite_izq - 60.0
	onda.limite_der = limite_der + 60.0
	get_parent().add_child(onda)
	onda.global_position = Vector2(global_position.x + _dir * 90.0, global_position.y)
	_sonido(SFX_ONDA, 2.0)


# ------------------------------------------------------------------
# Daño (lo llaman el Proyectil Arcano / Fuego y el bloqueo perfecto del Mago)
# ------------------------------------------------------------------

func recibir_danio(cantidad: int, _origen: Node2D) -> bool:
	if _estado in [Estado.INACTIVO, Estado.PRESENTACION, Estado.TRANSICION, Estado.MUERTE]:
		_destello(Color(0.5, 0.5, 0.5))  # invulnerable: el golpe no entra
		return false
	vida = maxi(vida - cantidad, 0)
	vida_cambiada.emit(vida, vida_maxima)
	_destello(Color.WHITE * 2.2)
	if _t_sonido_danio <= 0.0:
		_t_sonido_danio = 0.3
		_sonido(SFX_DANIO, -3.0, 0.1)
	if vida <= 0:
		_cambiar_estado(Estado.MUERTE)
	elif fase == 1 and vida <= umbral_fase_2:
		_transicion_pendiente = true
	return true


## Bloqueo perfecto del latigazo: abre la ventana de contraataque.
func aturdir() -> void:
	if _estado == Estado.LATIGAZO:
		velocity.x = -_dir * 220.0
		_cambiar_estado(Estado.ATURDIDO)


# ------------------------------------------------------------------
# Presentación
# ------------------------------------------------------------------

func _animar() -> void:
	_sprite.flip_h = _dir < 0
	match _estado:
		Estado.INACTIVO:
			_reproducir("quieto")
		Estado.PRESENTACION:
			_reproducir("rugido")
		Estado.ACECHO:
			_reproducir("caminar" if absf(velocity.x) > 1.0 else "quieto")
		Estado.TELL_LATIGAZO:
			_reproducir("tell_latigazo")
		Estado.LATIGAZO:
			_reproducir("latigazo")
		Estado.TELL_SALTO:
			_reproducir("tell_salto")
		Estado.SALTO:
			_reproducir("salto" if velocity.y < 0.0 else "caida")
		Estado.ATERRIZAJE:
			_reproducir("aterrizaje")
		Estado.TELL_ONDA:
			_reproducir("tell_onda")
		Estado.ONDA:
			_reproducir("onda")
		Estado.RECUPERACION:
			_reproducir("recuperacion")
		Estado.ATURDIDO:
			_reproducir("aturdido")
		Estado.TRANSICION:
			_reproducir("transicion")
		Estado.MUERTE:
			_reproducir("muerte")
	# En la fase 2 el látigo encendido le tiñe el cuerpo de naranja.
	if _estado != Estado.MUERTE:
		_sprite.self_modulate = Color(1.15, 0.92, 0.82) if fase == 2 else Color.WHITE


func _reproducir(nombre: StringName) -> void:
	if _sprite.animation != nombre:
		_sprite.play(nombre)


func _destello(color: Color) -> void:
	var tw := create_tween()
	modulate = color
	tw.tween_property(self, "modulate", Color.WHITE, 0.15)


func _sacudir(intensidad: float, duracion: float) -> void:
	if _jugador and _jugador.has_method("sacudir_camara"):
		_jugador.sacudir_camara(intensidad, duracion)


func _sonido(stream: AudioStream, extra_db: float = 0.0, variacion: float = 0.0, tono: float = 1.0) -> void:
	var p := AudioStreamPlayer2D.new()
	p.stream = stream
	p.volume_db = volumen_efectos_db + extra_db
	p.pitch_scale = tono + randf_range(-variacion, variacion)
	p.max_distance = 2500.0
	add_child(p)
	p.play()
	p.finished.connect(p.queue_free)


func _draw() -> void:
	# Jaula: barrotes que salen volando en la presentación.
	if _jaula < 1.0:
		var a := 1.0 - _jaula
		var c := Color(0.23, 0.21, 0.2, a)
		for i in 9:
			var x := -130.0 + i * 32.5
			var vuelo := Vector2((i - 4) * 90.0, -260.0) * _jaula
			draw_line(Vector2(x, -300) + vuelo, Vector2(x, 0) + vuelo * 0.6, c, 7.0)
		draw_line(Vector2(-140, -300) + Vector2(0, -200) * _jaula, Vector2(140, -300) + Vector2(0, -200) * _jaula, c, 10.0)
	# Aviso del salto: el piso se agrieta y brilla donde va a caer; crece durante el aviso y late.
	if _estado == Estado.TELL_SALTO or _estado == Estado.SALTO:
		var p := to_local(_objetivo_salto)
		var progreso := 1.0 if _estado == Estado.SALTO else clampf(1.0 - _t / tell_salto, 0.0, 1.0)
		var ancho := (zona_impacto_salto * TILE + 70.0) * lerpf(0.55, 1.0, progreso)
		var escala := ancho / AVISO_SALTO.get_width()
		var tam := Vector2(AVISO_SALTO.get_size()) * escala
		var pulso := 0.75 + 0.25 * sin(Time.get_ticks_msec() / 60.0)
		var rect := Rect2(p - Vector2(tam.x / 2.0, AVISO_CENTRO_Y * escala), tam)
		draw_texture_rect(AVISO_SALTO, rect, false, Color(1.3, 1.1, 1.1, pulso))
