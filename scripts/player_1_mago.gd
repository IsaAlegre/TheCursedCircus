extends CharacterBody2D
## Mago (jugador).
## Movimiento de precisión, magia con retroceso (Proyectil Arcano) y bloqueo perfecto,
## según el GDD (Parte 1, "Mecánicas clave" y Parte 2, "Valores base de referencia").
## Todos los valores numéricos son iniciales: se calibran desde el Inspector.

signal vida_cambiada(vida_actual: int, vida_maxima: int)
signal mana_cambiado(mana_actual: int, mana_maximo: int)
## Se intentó bloquear sin maná suficiente (el HUD sacude la barra).
signal sin_mana

const PROYECTIL := preload("res://scenes/proyectil_arcano.tscn")

# Sonidos (sintetizados para el proyecto, ver assets/audio)
const MUSICA := preload("res://assets/audio/musica_circo.ogg")
const SFX_DISPARO := preload("res://assets/audio/disparo_arcano.wav")
const SFX_DANIO := preload("res://assets/audio/danio_mago.wav")
const SFX_BLOQUEO := preload("res://assets/audio/bloqueo_perfecto.wav")

## Animaciones que el script agrega si todavía no están en el SpriteFrames del nivel.
## (Asset 4 del portafolio de assets; recortadas a 128 px de alto como las existentes.)
const ANIMACIONES_EXTRA := {
	&"run": "res://assets/mago_2_run.png",
	&"fall": "res://assets/fall.png",
	&"crouch": "res://assets/personajes/mago_agachado.png",
	&"cast": "res://assets/personajes/mago_hechizo.png",
	&"block": "res://assets/personajes/mago_bloqueo.png",
	&"hurt": "res://assets/personajes/mago_danio.png",
}

# Capas de física (Proyecto > Configuración > Nombres de capas > Física 2D)
# 1 = mundo, 2 = jugador, 3 = enemigos, 5 = plataformas semisólidas
const CAPA_SEMISOLIDAS := 5

@export_group("Movimiento")
@export var move_speed: float = 200.0
@export var run_speed: float = 340.0
@export var crouch_speed: float = 90.0
@export var gravity: float = 1200.0
@export var max_fall_speed: float = 900.0
@export var jump_velocity: float = -550.0
## Al soltar el salto mientras sube, la velocidad vertical se multiplica por este valor (salto variable).
@export var corte_salto: float = 0.45
## 6 frames a 60 fps = 0,1 s (GDD).
@export var coyote_time: float = 0.1
## Si se aprieta saltar un instante antes de tocar el suelo, el salto igual se ejecuta.
@export var jump_buffer: float = 0.1
@export var fall_limit: float = 800.0

@export_group("Magia")
@export var cadencia: float = 0.3
## Empuje horizontal al disparar en el suelo: ≈48 px por disparo (1,5 tiles de 32 px, GDD).
@export var retroceso_suelo: float = 300.0
## Empuje al disparar en el aire. Disparando hacia abajo frena la caída e impulsa hacia arriba.
@export var retroceso_aire: float = 520.0
@export var friccion_retroceso: float = 900.0
## Cuántos disparos por salto empujan al Mago en el aire. Se recargan al tocar el suelo.
## Sin este límite, disparar hacia abajo sin parar permitiría volar.
@export var impulsos_aereos: int = 2

@export_group("Sonido")
@export var volumen_musica_db: float = -10.0
@export var volumen_efectos_db: float = -4.0

@export_group("Vida y bloqueo")
@export var vida_maxima: int = 3
@export var invulnerabilidad: float = 1.0
## Ventana del bloqueo perfecto: solo protege si el golpe llega dentro de este tiempo.
@export var ventana_bloqueo: float = 0.15
@export var enfriamiento_bloqueo: float = 0.5
@export var empuje_por_danio: Vector2 = Vector2(320, -300)

@export_group("Maná")
@export var mana_maximo: int = 100
## Costo de bloquear (GDD). El Proyectil Arcano no consume maná.
@export var costo_bloqueo: int = 5
## Un bloqueo perfecto devuelve el maná gastado: premia el buen timing.
@export var bloqueo_perfecto_devuelve_mana: bool = true
## Con maná en 0 el Mago queda debilitado: no puede bloquear y se mueve más lento.
@export var multiplicador_debilitado: float = 0.75

var vida: int
var mana: int
var _mirando: int = 1
var _retroceso_x: float = 0.0
var _t_coyote: float = 0.0
var _t_buffer: float = 0.0
var _t_disparo: float = 0.0
var _t_bloqueo: float = 0.0
var _t_enfriamiento_bloqueo: float = 0.0
var _t_invulnerable: float = 0.0
var _saltando: bool = false
var _impulsos_restantes: int = 0
var _agachado: bool = false
var _muerto: bool = false
# Pose breve que tiene prioridad sobre el movimiento (lanzar, bloquear, recibir daño).
var _pose: StringName = &""
var _t_pose: float = 0.0

@onready var _sprite: AnimatedSprite2D = $Sprite2D
@onready var _colision: CollisionShape2D = $CollisionShape2D
@onready var _camara: Camera2D = get_node_or_null("Camera2D")

var _forma: RectangleShape2D
var _alto_forma: float
var _pos_forma: Vector2
var _pos_sprite: Vector2


func _ready() -> void:
	add_to_group("jugador")
	_configurar_controles()
	# Capas fijadas por código: así no hay que tocar la escena del nivel.
	collision_layer = 0
	set_collision_layer_value(2, true)
	collision_mask = 0
	set_collision_mask_value(1, true)
	set_collision_mask_value(CAPA_SEMISOLIDAS, true)

	vida = vida_maxima
	mana = mana_maximo
	_ir_al_checkpoint()
	# Se duplica la forma para poder achicarla al agacharse sin afectar otras escenas.
	_forma = _colision.shape.duplicate()
	_colision.shape = _forma
	_alto_forma = _forma.size.y
	_pos_forma = _colision.position
	_pos_sprite = _sprite.position
	_completar_animaciones()
	_iniciar_musica()
	vida_cambiada.emit(vida, vida_maxima)
	mana_cambiado.emit(mana, mana_maximo)


## Si ya se tocó un santuario, el Mago reaparece ahí en vez del inicio del nivel.
func _ir_al_checkpoint() -> void:
	var partida := get_node_or_null("/root/Partida")
	if partida == null or not partida.hay_checkpoint:
		return
	global_position = partida.punto_reaparicion
	if _camara:
		_camara.reset_smoothing.call_deferred()


## Centro del cuerpo. Los enemigos lo usan para apuntar y detectar al Mago.
func centro() -> Vector2:
	return _colision.global_position


func _physics_process(delta: float) -> void:
	if _muerto:
		return
	_actualizar_temporizadores(delta)

	var en_suelo := is_on_floor()
	var eje := Input.get_axis("mover_left", "mover_right")

	# --- Gravedad ---
	if not en_suelo:
		velocity.y = minf(velocity.y + gravity * delta, max_fall_speed)

	# --- Coyote time y buffer de salto ---
	if en_suelo:
		_t_coyote = coyote_time
		_saltando = false
		_impulsos_restantes = impulsos_aereos
	if Input.is_action_just_pressed("jump"):
		_t_buffer = jump_buffer

	# --- Agacharse ---
	_set_agachado(en_suelo and Input.is_action_pressed("agacharse"))

	# --- Saltar / atravesar semisólida (abajo + salto) ---
	if _t_buffer > 0.0 and _t_coyote > 0.0:
		if _agachado and _sobre_semisolida():
			_atravesar_semisolida()
		else:
			velocity.y = jump_velocity
			_saltando = true
		_t_buffer = 0.0
		_t_coyote = 0.0

	# Salto variable: soltar el botón corta la subida.
	if _saltando and Input.is_action_just_released("jump") and velocity.y < 0.0:
		velocity.y *= corte_salto
		_saltando = false

	# --- Movimiento horizontal ---
	var velocidad := move_speed
	if _agachado:
		velocidad = crouch_speed
	elif Input.is_action_pressed("correr"):
		velocidad = run_speed
	if esta_debilitado():
		velocidad *= multiplicador_debilitado
	velocity.x = eje * velocidad + _retroceso_x
	_retroceso_x = move_toward(_retroceso_x, 0.0, friccion_retroceso * delta)

	if eje != 0.0:
		_mirando = 1 if eje > 0.0 else -1

	# --- Disparo y bloqueo ---
	if Input.is_action_pressed("disparar") and _t_disparo <= 0.0:
		_disparar()
	if Input.is_action_just_pressed("bloquear") and _t_enfriamiento_bloqueo <= 0.0:
		if gastar_mana(costo_bloqueo):
			_t_bloqueo = ventana_bloqueo
			_t_enfriamiento_bloqueo = enfriamiento_bloqueo
			_mostrar_pose(&"block", 0.25)
		else:
			_t_enfriamiento_bloqueo = 0.3
			sin_mana.emit()
			_destello(Color(0.4, 0.4, 0.45))

	move_and_slide()
	if is_on_wall():
		_retroceso_x = 0.0

	_animar(eje)

	if global_position.y > fall_limit:
		_morir()


func _actualizar_temporizadores(delta: float) -> void:
	_t_coyote -= delta
	_t_buffer -= delta
	_t_disparo -= delta
	_t_bloqueo -= delta
	_t_enfriamiento_bloqueo -= delta
	_t_pose -= delta
	if _t_invulnerable > 0.0:
		_t_invulnerable -= delta
		# Parpadeo durante la invulnerabilidad.
		_sprite.visible = _t_invulnerable <= 0.0 or int(_t_invulnerable * 15.0) % 2 == 0


# ------------------------------------------------------------------
# Plataformas semisólidas
# ------------------------------------------------------------------

func _sobre_semisolida() -> bool:
	for i in get_slide_collision_count():
		var c := get_slide_collision(i)
		var cuerpo := c.get_collider() as CollisionObject2D
		if cuerpo and c.get_normal().y < -0.5 and cuerpo.get_collision_layer_value(CAPA_SEMISOLIDAS):
			return true
	return false


func _atravesar_semisolida() -> void:
	set_collision_mask_value(CAPA_SEMISOLIDAS, false)
	position.y += 2.0
	await get_tree().create_timer(0.25).timeout
	set_collision_mask_value(CAPA_SEMISOLIDAS, true)


func _set_agachado(valor: bool) -> void:
	if valor == _agachado:
		return
	_agachado = valor
	if valor:
		_forma.size.y = _alto_forma * 0.6
		_colision.position.y = _pos_forma.y + _alto_forma * 0.2
	else:
		_forma.size.y = _alto_forma
		_colision.position.y = _pos_forma.y


# ------------------------------------------------------------------
# Magia con retroceso
# ------------------------------------------------------------------

func _direccion_de_disparo() -> Vector2:
	# Con el mouse se apunta en cualquier dirección.
	if Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		var hacia_mouse := get_global_mouse_position() - centro()
		if hacia_mouse.length() > 1.0:
			return hacia_mouse.normalized()
	# Con teclado: hacia adelante, o hacia abajo si está en el aire manteniendo "agacharse".
	if not is_on_floor() and Input.is_action_pressed("agacharse"):
		return Vector2.DOWN
	return Vector2(_mirando, 0)


func _disparar() -> void:
	_t_disparo = cadencia
	var dir := _direccion_de_disparo()
	_mostrar_pose(&"cast", 0.2)
	_sonido(SFX_DISPARO, 0.0, 0.08)
	if absf(dir.x) > 0.1:
		_mirando = 1 if dir.x > 0.0 else -1

	var p := PROYECTIL.instantiate()
	p.direccion = dir
	p.lanzador = self
	get_parent().add_child(p)
	p.global_position = centro() + dir * 40.0

	# Retroceso: el Mago es empujado en sentido contrario al disparo.
	var en_suelo := is_on_floor()
	if not en_suelo:
		if _impulsos_restantes <= 0:
			return  # El hechizo sale igual, pero ya no empuja.
		_impulsos_restantes -= 1
	var empuje := -dir * (retroceso_suelo if en_suelo else retroceso_aire)
	_retroceso_x += empuje.x
	if not en_suelo:
		if empuje.y < 0.0:
			# Disparo hacia abajo: frena la caída e impulsa hacia arriba.
			velocity.y = minf(velocity.y, empuje.y)
			_saltando = false
		else:
			velocity.y += empuje.y


# ------------------------------------------------------------------
# Vida, daño y bloqueo perfecto
# ------------------------------------------------------------------

## Lo llaman los enemigos. Devuelve true si el golpe hizo daño.
func recibir_danio(cantidad: int, origen: Node2D) -> bool:
	if _muerto or _t_invulnerable > 0.0:
		return false
	if _t_bloqueo > 0.0:
		_bloqueo_perfecto(origen)
		return false

	vida = maxi(vida - cantidad, 0)
	vida_cambiada.emit(vida, vida_maxima)
	_t_invulnerable = invulnerabilidad

	var lado := 1.0 if centro().x >= origen.global_position.x else -1.0
	_retroceso_x = empuje_por_danio.x * lado
	velocity.y = empuje_por_danio.y
	_saltando = false
	_mostrar_pose(&"hurt", 0.35)
	_sonido(SFX_DANIO)
	_destello(Color("9C171F"))  # Rojo Carmesí Dramático (GDD)
	_sacudir_camara(6.0, 0.15)

	if vida <= 0:
		_morir()
	return true


func _bloqueo_perfecto(origen: Node2D) -> void:
	_t_bloqueo = 0.0
	_t_enfriamiento_bloqueo = 0.0  # Premio: se puede volver a bloquear enseguida.
	if bloqueo_perfecto_devuelve_mana:
		recuperar_mana(costo_bloqueo)
	_t_invulnerable = 0.3
	_destello(Color("F5F2EA"))  # Blanco Hueso (GDD)
	_sonido(SFX_BLOQUEO, 2.0)
	_sacudir_camara(10.0, 0.2)
	# Ventana de contraataque: el atacante queda aturdido.
	if origen and origen.has_method("aturdir"):
		origen.aturdir()


# ------------------------------------------------------------------
# Maná y curación (los usan los cristales, los santuarios y el HUD)
# ------------------------------------------------------------------

func esta_debilitado() -> bool:
	return mana <= 0


## Descuenta maná si alcanza. Devuelve false si no hay suficiente.
func gastar_mana(cantidad: int) -> bool:
	if mana < cantidad:
		return false
	mana -= cantidad
	mana_cambiado.emit(mana, mana_maximo)
	return true


## Suma maná sin pasarse del máximo. Devuelve false si ya estaba lleno.
func recuperar_mana(cantidad: int) -> bool:
	if mana >= mana_maximo:
		return false
	mana = mini(mana + cantidad, mana_maximo)
	mana_cambiado.emit(mana, mana_maximo)
	return true


func curar(cantidad: int) -> void:
	if vida >= vida_maxima:
		return
	vida = mini(vida + cantidad, vida_maxima)
	vida_cambiada.emit(vida, vida_maxima)
	_destello(Color("8A2BE2"))


## Santuario: vida y maná al máximo.
func restaurar_todo() -> void:
	if vida < vida_maxima:
		curar(vida_maxima)
	if mana < mana_maximo:
		recuperar_mana(mana_maximo)


func _morir() -> void:
	if _muerto:
		return
	_muerto = true
	velocity = Vector2.ZERO
	# Se reinicia el nivel: los enemigos vuelven, y el Mago aparece en el último
	# santuario tocado (ver _ir_al_checkpoint) con vida y maná completos (GDD).
	get_tree().call_deferred("reload_current_scene")


# ------------------------------------------------------------------
# Presentación
# ------------------------------------------------------------------

func _animar(eje: float) -> void:
	_sprite.flip_h = _mirando < 0
	_sprite.scale.y = 1.0
	_sprite.position.y = _pos_sprite.y

	if _t_pose > 0.0 and _reproducir(_pose, ""):
		return

	if not is_on_floor():
		if velocity.y < 0.0:
			_reproducir("jump", "idle")
		else:
			_reproducir("fall", "jump")
	elif _agachado:
		if not _reproducir("crouch", ""):
			# Sin sprite de agachado todavía: se aplasta el sprite actual.
			_reproducir("idle", "idle")
			_sprite.scale.y = 0.7
			_sprite.position.y = _pos_sprite.y + 19.0
	elif eje != 0.0:
		if Input.is_action_pressed("correr"):
			_reproducir("run", "walk")
		else:
			_reproducir("walk", "idle")
	else:
		_reproducir("idle", "idle")


func _mostrar_pose(nombre: StringName, duracion: float) -> void:
	_pose = nombre
	_t_pose = duracion


## Agrega al SpriteFrames las animaciones de ANIMACIONES_EXTRA que falten.
## Se trabaja sobre una copia para no modificar el recurso guardado en la escena.
func _completar_animaciones() -> void:
	if _sprite.sprite_frames == null:
		return
	var frames := _sprite.sprite_frames.duplicate() as SpriteFrames
	for nombre in ANIMACIONES_EXTRA:
		if frames.has_animation(nombre):
			continue
		var textura := load(ANIMACIONES_EXTRA[nombre]) as Texture2D
		if textura == null:
			continue
		frames.add_animation(nombre)
		frames.add_frame(nombre, textura)
	_sprite.sprite_frames = frames


## Reproduce la animación si existe en el SpriteFrames; si no, la alternativa.
func _reproducir(nombre: StringName, alternativa: StringName) -> bool:
	var frames := _sprite.sprite_frames
	if frames and frames.has_animation(nombre):
		_sprite.play(nombre)
		return true
	if alternativa != &"" and frames and frames.has_animation(alternativa):
		_sprite.play(alternativa)
	return false


func _destello(color: Color) -> void:
	var tw := create_tween()
	_sprite.modulate = color * 2.0
	tw.tween_property(_sprite, "modulate", Color.WHITE, 0.25)


func _sacudir_camara(intensidad: float, duracion: float) -> void:
	if _camara == null:
		return
	var tw := create_tween()
	var pasos := 6
	for i in pasos:
		var desplazamiento := Vector2(randf_range(-1, 1), randf_range(-1, 1)) * intensidad
		tw.tween_property(_camara, "offset", desplazamiento, duracion / pasos)
	tw.tween_property(_camara, "offset", Vector2.ZERO, duracion / pasos)


# ------------------------------------------------------------------
# Sonido
# ------------------------------------------------------------------

## Reproduce un efecto. variacion_tono cambia levemente la altura para que no suene repetido.
func _sonido(stream: AudioStream, volumen_extra_db: float = 0.0, variacion_tono: float = 0.0) -> void:
	var p := AudioStreamPlayer.new()
	p.stream = stream
	p.volume_db = volumen_efectos_db + volumen_extra_db
	p.pitch_scale = 1.0 + randf_range(-variacion_tono, variacion_tono)
	add_child(p)
	p.play()
	p.finished.connect(p.queue_free)


## La música se cuelga de la raíz del árbol (fuera del nivel), así no se corta
## ni vuelve a empezar cada vez que el nivel se reinicia al morir.
func _iniciar_musica() -> void:
	var raiz := get_tree().root
	if raiz.has_node("Musica"):
		return
	var musica := AudioStreamPlayer.new()
	musica.name = "Musica"
	var pista := MUSICA.duplicate() as AudioStreamOggVorbis
	pista.loop = true
	musica.stream = pista
	musica.volume_db = volumen_musica_db
	musica.autoplay = true
	raiz.add_child.call_deferred(musica)


# ------------------------------------------------------------------
# Controles
# ------------------------------------------------------------------

## Agrega las acciones nuevas si no existen en el Mapa de Entrada del proyecto.
## Se pueden ver y cambiar en Proyecto > Configuración del Proyecto > Mapa de Entrada.
func _configurar_controles() -> void:
	_agregar_teclas("agacharse", [KEY_S, KEY_DOWN])
	_agregar_teclas("correr", [KEY_SHIFT])
	_agregar_teclas("disparar", [KEY_J])
	_agregar_mouse("disparar", MOUSE_BUTTON_LEFT)
	_agregar_teclas("bloquear", [KEY_K])
	_agregar_mouse("bloquear", MOUSE_BUTTON_RIGHT)


func _agregar_teclas(accion: StringName, teclas: Array) -> void:
	if not InputMap.has_action(accion):
		InputMap.add_action(accion)
	for tecla in teclas:
		var ev := InputEventKey.new()
		ev.physical_keycode = tecla
		if not InputMap.action_has_event(accion, ev):
			InputMap.action_add_event(accion, ev)


func _agregar_mouse(accion: StringName, boton: MouseButton) -> void:
	if not InputMap.has_action(accion):
		InputMap.add_action(accion)
	var ev := InputEventMouseButton.new()
	ev.button_index = boton
	if not InputMap.action_has_event(accion, ev):
		InputMap.action_add_event(accion, ev)
