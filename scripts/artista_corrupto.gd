extends CharacterBody2D
## Artista Corrupto: enemigo base de la Zona 1 (GDD, Parte 2, "Enemigos").
## Patrulla una plataforma y, al ver al Mago, avisa 0,5 s y embiste.
## La embestida se puede esquivar o bloquear (bloqueo perfecto → queda aturdido).

enum Estado { PATRULLA, AVISO, EMBESTIDA, ATURDIDO, MUERTO }

@export var vida: int = 30
## 2 tiles/s con tiles de 64 px.
@export var velocidad_patrulla: float = 128.0
@export var velocidad_embestida: float = 420.0
@export var duracion_aviso: float = 0.5
@export var duracion_embestida: float = 0.9
@export var duracion_aturdido: float = 1.5
@export var descanso_tras_embestida: float = 0.8
@export var rango_vision: float = 420.0
@export var alto_vision: float = 110.0
@export var danio: int = 1
## Segundos mínimos entre una risa y la siguiente (para que no sature).
@export var pausa_entre_risas: float = 2.5
@export var volumen_efectos_db: float = -4.0
@export var gravedad: float = 1200.0
## Desactivar para la emboscada fija del tramo 1 (no patrullan, solo esperan).
@export var patrulla: bool = true
## Marcar según hacia dónde mira el dibujo original del sprite.
@export var sprite_mira_a_la_derecha: bool = true

@export_group("Botín")
## Probabilidad de soltar un cristal de maná al morir.
@export_range(0.0, 1.0) var prob_cristal: float = 0.85
## Si suelta cristal: chance de que sea mediano (+15) o especial (+30). El resto, pequeño (+5).
@export_range(0.0, 1.0) var prob_mediano: float = 0.25
@export_range(0.0, 1.0) var prob_especial: float = 0.05

var _estado: Estado = Estado.PATRULLA
var _dir: int = -1
var _t: float = 0.0
var _t_descanso: float = 0.0
var _jugador: Node2D

@onready var _sprite: AnimatedSprite2D = $Sprite
@onready var _rayo_suelo: RayCast2D = $RayoSuelo
@onready var _area_golpe: Area2D = $AreaGolpe
@onready var _colision: CollisionShape2D = $CollisionShape2D

## Risas de ElevenLabs. Se elige una al azar en cada aviso.
const RISAS: Array[AudioStream] = [
	preload("res://assets/audio/risa_payaso_2.ogg"),
	preload("res://assets/audio/risa_payaso_3.ogg"),
	preload("res://assets/audio/risa_payaso_4.ogg"),
]
const SFX_IMPACTO := preload("res://assets/audio/impacto_enemigo.wav")
const SFX_MUERTE := preload("res://assets/audio/muerte_enemigo.wav")
## Se carga por ruta (no por class_name) para que no dependa de que Godot lo registre.
const CRISTAL := preload("res://scripts/cristal_mana.gd")

var _t_risa: float = 0.0

const GRANATE := Color("5C1D1C")
const ROJO_ARDIENTE := Color("E33224")


func _ready() -> void:
	add_to_group("enemigos")
	collision_layer = 0
	set_collision_layer_value(3, true)
	collision_mask = 0
	set_collision_mask_value(1, true)
	set_collision_mask_value(5, true)
	_rayo_suelo.collision_mask = collision_mask
	_area_golpe.collision_layer = 0
	_area_golpe.collision_mask = 0
	_area_golpe.set_collision_mask_value(2, true)
	_jugador = get_tree().get_first_node_in_group("jugador")


func centro() -> Vector2:
	return _colision.global_position


func _physics_process(delta: float) -> void:
	if _estado == Estado.MUERTO:
		return
	if not is_on_floor():
		velocity.y += gravedad * delta
	_t -= delta
	_t_descanso -= delta
	_t_risa -= delta

	match _estado:
		Estado.PATRULLA:
			_patrullar()
		Estado.AVISO:
			velocity.x = 0.0
			# Tiembla durante el aviso para que se lea el ataque.
			_sprite.position.x = randf_range(-2.0, 2.0)
			if _t <= 0.0:
				_sprite.position.x = 0.0
				_cambiar_estado(Estado.EMBESTIDA)
		Estado.EMBESTIDA:
			velocity.x = _dir * velocidad_embestida
			if _t <= 0.0 or is_on_wall() or _hay_borde():
				velocity.x = 0.0
				_t_descanso = descanso_tras_embestida
				_cambiar_estado(Estado.PATRULLA)
		Estado.ATURDIDO:
			velocity.x = move_toward(velocity.x, 0.0, 1200.0 * delta)
			if _t <= 0.0:
				_t_descanso = descanso_tras_embestida
				_cambiar_estado(Estado.PATRULLA)

	move_and_slide()
	_golpear_al_contacto()
	_animar()
	queue_redraw()


func _patrullar() -> void:
	if patrulla and _t_descanso <= 0.0:
		velocity.x = _dir * velocidad_patrulla
		if is_on_floor() and (is_on_wall() or _hay_borde()):
			_dir *= -1
	else:
		velocity.x = 0.0
	if _t_descanso <= 0.0 and _ve_al_jugador():
		_dir = 1 if _jugador.centro().x > centro().x else -1
		_cambiar_estado(Estado.AVISO)


func _hay_borde() -> bool:
	_rayo_suelo.position.x = 30.0 * _dir
	_rayo_suelo.force_raycast_update()
	return is_on_floor() and not _rayo_suelo.is_colliding()


func _ve_al_jugador() -> bool:
	if _jugador == null or not is_instance_valid(_jugador):
		_jugador = get_tree().get_first_node_in_group("jugador")
		if _jugador == null:
			return false
	var d: Vector2 = _jugador.centro() - centro()
	if absf(d.x) > rango_vision or absf(d.y) > alto_vision:
		return false
	# Lo ve si está delante, o muy cerca aunque esté detrás.
	return signf(d.x) == _dir or absf(d.x) < 120.0


func _cambiar_estado(nuevo: Estado) -> void:
	_estado = nuevo
	match nuevo:
		Estado.AVISO:
			_t = duracion_aviso
			# Se ríe antes de embestir: además de dar miedo, avisa del ataque.
			if _t_risa <= 0.0:
				var risa: AudioStream = RISAS.pick_random()
				# No vuelve a reírse hasta que termine esta risa (las hay de hasta 3 s).
				_t_risa = maxf(pausa_entre_risas, risa.get_length())
				_sonido(risa, 0.0, 0.08)
		Estado.EMBESTIDA:
			_t = duracion_embestida
		Estado.ATURDIDO:
			_t = duracion_aturdido


func _golpear_al_contacto() -> void:
	if _estado == Estado.ATURDIDO:
		return
	for cuerpo in _area_golpe.get_overlapping_bodies():
		if cuerpo.has_method("recibir_danio") and cuerpo.is_in_group("jugador"):
			cuerpo.recibir_danio(danio, self)


# ------------------------------------------------------------------
# Lo llaman el proyectil y el jugador
# ------------------------------------------------------------------

func recibir_danio(cantidad: int, origen: Node2D) -> bool:
	if _estado == Estado.MUERTO:
		return false
	vida -= cantidad
	_destello(Color.WHITE * 2.5)
	if vida > 0:
		_sonido(SFX_IMPACTO, 0.0, 0.1)
	# Si le pegan de espaldas, se da vuelta.
	if origen and _estado == Estado.PATRULLA:
		_dir = 1 if origen.global_position.x > centro().x else -1
	if vida <= 0:
		_morir()
	return true


## Bloqueo perfecto del Mago: abre la ventana de contraataque.
func aturdir() -> void:
	if _estado == Estado.MUERTO:
		return
	velocity.x = -_dir * 250.0
	_cambiar_estado(Estado.ATURDIDO)


func _morir() -> void:
	_estado = Estado.MUERTO
	_sonido(SFX_MUERTE, 0.0, 0.05, true)
	velocity = Vector2.ZERO
	_colision.set_deferred("disabled", true)
	_area_golpe.set_deferred("monitoring", false)
	_sprite.modulate = Color.WHITE
	_sprite.position.x = 0.0
	_soltar_cristal()
	# El cuerpo se desintegra (Asset 4: muerte en dos cuadros) y luego se desvanece.
	var tiene_muerte := _sprite.sprite_frames != null and _sprite.sprite_frames.has_animation("muerte")
	if tiene_muerte:
		_sprite.play("muerte")
	var tw := create_tween()
	tw.tween_interval(0.45 if tiene_muerte else 0.0)
	tw.tween_property(self, "modulate", Color(1, 1, 1, 0), 0.4)
	tw.tween_callback(queue_free)


func _soltar_cristal() -> void:
	if randf() > prob_cristal:
		return
	var tirada := randf()
	var cristal := CRISTAL.new()
	if tirada < prob_especial:
		cristal.tamanio = CRISTAL.Tamanio.ESPECIAL
	elif tirada < prob_especial + prob_mediano:
		cristal.tamanio = CRISTAL.Tamanio.MEDIANO
	var nivel := get_parent()
	cristal.position = nivel.to_local(centro())
	# Diferido: _morir puede ejecutarse en medio de una colisión del proyectil.
	nivel.add_child.call_deferred(cristal)


# ------------------------------------------------------------------
# Presentación
# ------------------------------------------------------------------

func _animar() -> void:
	_sprite.flip_h = (_dir < 0) == sprite_mira_a_la_derecha
	match _estado:
		Estado.AVISO:
			_reproducir("aviso")
			_sprite.modulate = Color(1.6, 0.6, 0.6)
		Estado.EMBESTIDA:
			_reproducir("embestida")
			_sprite.modulate = Color.WHITE
		Estado.ATURDIDO:
			_reproducir("aturdido")
			_sprite.modulate = Color(0.6, 0.6, 0.9)
		_:
			_reproducir("caminar" if absf(velocity.x) > 1.0 else "quieto")
			_sprite.modulate = Color.WHITE


func _reproducir(nombre: StringName) -> void:
	var frames := _sprite.sprite_frames
	if frames == null:
		return
	if frames.has_animation(nombre) and frames.get_frame_count(nombre) > 0:
		_sprite.play(nombre)
	elif frames.has_animation("caminar") and frames.get_frame_count("caminar") > 0:
		_sprite.play("caminar")


func _tiene_sprite() -> bool:
	var frames := _sprite.sprite_frames
	return frames != null and frames.has_animation("caminar") and frames.get_frame_count("caminar") > 0


## Sonido posicional: se oye más fuerte cuanto más cerca está el Mago (la cámara).
## en_el_nivel = true lo deja en el nivel para que siga sonando aunque el enemigo desaparezca.
func _sonido(stream: AudioStream, volumen_extra_db: float = 0.0, variacion_tono: float = 0.0, en_el_nivel: bool = false) -> void:
	var p := AudioStreamPlayer2D.new()
	p.stream = stream
	p.volume_db = volumen_efectos_db + volumen_extra_db
	p.pitch_scale = 1.0 + randf_range(-variacion_tono, variacion_tono)
	p.max_distance = 1400.0
	if en_el_nivel:
		get_parent().add_child(p)
		p.global_position = centro()
	else:
		add_child(p)
		p.position = _colision.position
	p.play()
	p.finished.connect(p.queue_free)


func _destello(color: Color) -> void:
	var tw := create_tween()
	modulate = color
	tw.tween_property(self, "modulate", Color.WHITE, 0.15)


## Figura provisoria mientras no se cargue el sprite (animación "caminar" vacía).
func _draw() -> void:
	if _tiene_sprite():
		return
	var cuerpo := Color(0.6, 0.6, 0.9) if _estado == Estado.ATURDIDO else GRANATE
	if _estado == Estado.AVISO:
		cuerpo = ROJO_ARDIENTE
	draw_rect(Rect2(-26, -112, 52, 112), cuerpo)
	draw_rect(Rect2(-26, -112, 52, 112), Color("151515"), false, 3.0)
	# Ojos hacia donde mira.
	draw_circle(Vector2(10 * _dir, -92), 4.0, Color("F5F2EA"))
	draw_circle(Vector2(20 * _dir, -92), 4.0, Color("F5F2EA"))
