extends Node2D
## Arena del Domador (tramo 6). Dirige la puesta en escena del encuentro:
##   1. Al cruzar la entrada cae un telón que cierra la salida, la cámara se aleja,
##      la música del nivel se apaga y empieza la del jefe.
##   2. El jefe rompe su jaula (presentación) y aparece su nombre y su barra de vida.
##   3. En la fase 2 se suma la segunda capa de la música.
##   4. Al morir: silencio, acorde de purificación y aparece el Hechizo de Fuego.
##      Al tomarlo, el telón se levanta y el Mago puede volver.
## El origen del nodo va en el borde izquierdo de la arena, a la altura del piso.
## El jefe es el hijo "Domador".

const HECHIZO := preload("res://scripts/hechizo_fuego.gd")
const MUSICA_F1 := preload("res://assets/audio/jefe/musica_jefe_fase1.ogg")
const MUSICA_F2 := preload("res://assets/audio/jefe/musica_jefe_fase2.ogg")
const TELON := preload("res://assets/muro_lona_1.png")
const MARCO_BARRA := preload("res://assets/ui/jefe/marco_barra.png")
const RELLENO_F1 := preload("res://assets/ui/jefe/relleno_fase1.png")
const RELLENO_F2 := preload("res://assets/ui/jefe/relleno_fase2.png")
## IM Fell English SC (Google Fonts, licencia OFL: assets/fuentes/OFL.txt)
const FUENTE := preload("res://assets/fuentes/IMFellEnglishSC-Regular.ttf")
## Hueco interior del marco (en píxeles de marco_barra.png, 900 × 220).
const HUECO := Rect2(168, 71, 665, 64)

const CARMESI := Color("9C171F")
const NARANJA := Color("FF5A00")
const BLANCO_HUESO := Color("F5F2EA")
const NEGRO := Color("151515")

@export var ancho: float = 1600.0
## Zoom de la cámara durante el combate (menos de 1 = se aleja).
@export var zoom_combate: float = 0.8
@export var volumen_musica_db: float = -8.0

var _jefe: Node2D
var _jugador: Node2D
var _activa: bool = false
var _terminada: bool = false

var _puerta: StaticBody2D
var _telon: Sprite2D
var _musica_1: AudioStreamPlayer
var _musica_2: AudioStreamPlayer
var _hud: CanvasLayer
var _caja_barra: Control
var _recorte_vida: Control
var _recorte_estela: Control
var _textura_relleno: TextureRect
var _titulo: Label
var _mensaje: Label


func _ready() -> void:
	_jefe = $Domador
	_jefe.limite_izq = global_position.x + 90.0
	_jefe.limite_der = global_position.x + ancho - 90.0
	_jefe.vida_cambiada.connect(_al_cambiar_vida)
	_jefe.fase_cambiada.connect(_al_cambiar_fase)
	_jefe.murio.connect(_al_morir_jefe)
	_crear_activador()
	_crear_puerta()
	_crear_musica()
	_crear_hud()
	# Si el Mago murió durante el combate, el nivel se reinicia: la música del nivel vuelve.
	_restaurar_musica_nivel.call_deferred()
	# Los jefes derrotados no reaparecen (GDD).
	var partida := get_node_or_null("/root/Partida")
	if partida and partida.get("domador_derrotado"):
		_terminada = true
		_jefe.queue_free()


# ------------------------------------------------------------------
# Construcción
# ------------------------------------------------------------------

func _crear_activador() -> void:
	var area := Area2D.new()
	area.collision_layer = 0
	area.collision_mask = 0
	area.set_collision_mask_value(2, true)
	var forma := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(40, 700)
	forma.shape = rect
	area.add_child(forma)
	area.position = Vector2(260, -350)
	add_child(area)
	area.body_entered.connect(_al_entrar)


func _crear_puerta() -> void:
	_puerta = StaticBody2D.new()
	_puerta.collision_layer = 1
	var forma := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(40, 2000)
	forma.shape = rect
	forma.disabled = true
	_puerta.add_child(forma)
	_puerta.position = Vector2(20, -1000)
	add_child(_puerta)
	_telon = Sprite2D.new()
	_telon.texture = TELON
	_telon.position = Vector2(20, -900)
	_telon.visible = false
	add_child(_telon)


func _crear_musica() -> void:
	_musica_1 = AudioStreamPlayer.new()
	_musica_2 = AudioStreamPlayer.new()
	for par in [[_musica_1, MUSICA_F1], [_musica_2, MUSICA_F2]]:
		var pista := (par[1] as AudioStreamOggVorbis).duplicate() as AudioStreamOggVorbis
		pista.loop = true
		par[0].stream = pista
		par[0].volume_db = -80.0
		add_child(par[0])


func _crear_hud() -> void:
	_hud = CanvasLayer.new()
	_hud.layer = 11
	add_child(_hud)
	var raiz := Control.new()
	raiz.set_anchors_preset(Control.PRESET_FULL_RECT)
	raiz.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hud.add_child(raiz)

	# Barra de vida del jefe, arriba al centro (sobre el cielo, sin tapar el piso):
	# marco ilustrado con el líquido adentro y el nombre debajo.
	var escala := 0.6
	_caja_barra = Control.new()
	_caja_barra.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_caja_barra.size = Vector2(900, 290)
	_caja_barra.scale = Vector2(escala, escala)
	_caja_barra.position = Vector2(-450 * escala, -10)
	_caja_barra.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_caja_barra.modulate.a = 0.0
	raiz.add_child(_caja_barra)
	var marco_y := 0.0
	var hueco := Rect2(HUECO.position + Vector2(0, marco_y), HUECO.size)
	var fondo_hueco := ColorRect.new()
	fondo_hueco.color = Color(NEGRO, 0.92)
	fondo_hueco.position = hueco.position
	fondo_hueco.size = hueco.size
	_caja_barra.add_child(fondo_hueco)
	# Estela: se vacía más despacio que la vida, para que se lea cuánto daño se hizo.
	_recorte_estela = _recorte(hueco)
	var estela := ColorRect.new()
	estela.color = Color(BLANCO_HUESO, 0.55)
	estela.size = hueco.size
	_recorte_estela.add_child(estela)
	_recorte_vida = _recorte(hueco)
	_textura_relleno = TextureRect.new()
	_textura_relleno.texture = RELLENO_F1
	_textura_relleno.size = hueco.size
	_textura_relleno.stretch_mode = TextureRect.STRETCH_SCALE
	_recorte_vida.add_child(_textura_relleno)
	var marco := TextureRect.new()
	marco.texture = MARCO_BARRA
	marco.position = Vector2(0, marco_y)
	marco.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_caja_barra.add_child(marco)
	var nombre := _etiqueta("El Domador Corrompido", 40)
	nombre.position = Vector2(hueco.position.x, 205)
	nombre.size = Vector2(hueco.size.x, 50)
	nombre.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_caja_barra.add_child(nombre)

	# Título de presentación y mensajes, al centro
	_titulo = _etiqueta("El Domador Corrompido", 72)
	_titulo.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_titulo.position = Vector2(-500, 210)
	_titulo.size = Vector2(1000, 90)
	_titulo.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_titulo.modulate.a = 0.0
	raiz.add_child(_titulo)
	_mensaje = _etiqueta("", 36)
	_mensaje.set_anchors_preset(Control.PRESET_CENTER)
	_mensaje.position = Vector2(-400, -120)
	_mensaje.size = Vector2(800, 100)
	_mensaje.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_mensaje.modulate.a = 0.0
	raiz.add_child(_mensaje)


func _recorte(rect: Rect2) -> Control:
	var c := Control.new()
	c.clip_contents = true
	c.position = rect.position
	c.size = rect.size
	c.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_caja_barra.add_child(c)
	return c


func _etiqueta(texto: String, tamanio: int) -> Label:
	var l := Label.new()
	l.text = texto
	l.add_theme_font_override("font", FUENTE)
	l.add_theme_font_size_override("font_size", tamanio)
	l.add_theme_color_override("font_color", BLANCO_HUESO)
	l.add_theme_color_override("font_outline_color", NEGRO)
	l.add_theme_constant_override("outline_size", 10)
	l.add_theme_color_override("font_shadow_color", Color(CARMESI, 0.8))
	l.add_theme_constant_override("shadow_offset_x", 0)
	l.add_theme_constant_override("shadow_offset_y", 3)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


# ------------------------------------------------------------------
# Desarrollo del encuentro
# ------------------------------------------------------------------

func _al_entrar(cuerpo: Node) -> void:
	if _activa or _terminada or not cuerpo.is_in_group("jugador"):
		return
	_activa = true
	_jugador = cuerpo
	_cerrar_puerta(true)
	_zoom(zoom_combate)
	_bajar_musica_nivel()
	_musica_1.play()
	_musica_2.play()  # suena muda y sincronizada hasta la fase 2
	create_tween().tween_property(_musica_1, "volume_db", volumen_musica_db, 1.5)
	_jefe.iniciar()
	var tw := create_tween()
	tw.tween_interval(0.6)
	tw.tween_property(_titulo, "modulate:a", 1.0, 0.5)
	tw.tween_interval(1.6)
	tw.tween_property(_titulo, "modulate:a", 0.0, 0.6)
	tw.parallel().tween_property(_caja_barra, "modulate:a", 1.0, 0.6)


func _al_cambiar_vida(vida: int, maxima: int) -> void:
	var ancho := HUECO.size.x * float(vida) / float(maxima)
	create_tween().tween_property(_recorte_vida, "size:x", ancho, 0.12)
	var tw := create_tween()
	tw.tween_interval(0.45)
	tw.tween_property(_recorte_estela, "size:x", ancho, 0.5).set_ease(Tween.EASE_IN)


func _al_cambiar_fase(_fase: int) -> void:
	_textura_relleno.texture = RELLENO_F2
	create_tween().tween_property(_musica_2, "volume_db", volumen_musica_db - 2.0, 1.5)


func _al_morir_jefe() -> void:
	_terminada = true
	var tw := create_tween().set_parallel(true)
	tw.tween_property(_musica_1, "volume_db", -80.0, 0.4)
	tw.tween_property(_musica_2, "volume_db", -80.0, 0.4)
	tw.tween_property(_caja_barra, "modulate:a", 0.0, 1.0)
	tw.chain().tween_callback(func():
		_musica_1.stop()
		_musica_2.stop())
	var partida := get_node_or_null("/root/Partida")
	if partida and "domador_derrotado" in partida:
		partida.domador_derrotado = true
	var orbe := HECHIZO.new()
	orbe.position = to_local(_jefe.centro())
	orbe.recogido.connect(_al_recoger_hechizo)
	add_child.call_deferred(orbe)


func _al_recoger_hechizo(jugador: Node) -> void:
	if jugador.has_method("obtener_hechizo_fuego"):
		jugador.obtener_hechizo_fuego()
	var partida := get_node_or_null("/root/Partida")
	if partida and "tiene_fuego" in partida:
		partida.tiene_fuego = true
	_mostrar_mensaje("Obtuviste el Hechizo de Fuego\nQ para cambiar de hechizo · cuesta 25 de maná")
	_cerrar_puerta(false)
	_zoom(1.0)
	_restaurar_musica_nivel()


func _mostrar_mensaje(texto: String) -> void:
	_mensaje.text = texto
	var tw := create_tween()
	tw.tween_property(_mensaje, "modulate:a", 1.0, 0.4)
	tw.tween_interval(3.5)
	tw.tween_property(_mensaje, "modulate:a", 0.0, 0.8)


func _cerrar_puerta(cerrar: bool) -> void:
	_puerta.get_child(0).set_deferred("disabled", not cerrar)
	_telon.visible = true
	var tw := create_tween().set_ease(Tween.EASE_IN if cerrar else Tween.EASE_OUT).set_trans(Tween.TRANS_QUAD)
	tw.tween_property(_telon, "position:y", -96.0 if cerrar else -900.0, 0.5 if cerrar else 1.2)
	if cerrar and _jugador and _jugador.has_method("sacudir_camara"):
		tw.tween_callback(func(): _jugador.sacudir_camara(6.0, 0.2))


func _zoom(valor: float) -> void:
	var camara: Camera2D = _jugador.get_node_or_null("Camera2D") if _jugador else null
	if camara:
		create_tween().tween_property(camara, "zoom", Vector2(valor, valor), 1.2).set_trans(Tween.TRANS_SINE)


func _bajar_musica_nivel() -> void:
	var m := get_tree().root.get_node_or_null("Musica")
	if m:
		create_tween().tween_property(m, "volume_db", -80.0, 1.0)


func _restaurar_musica_nivel() -> void:
	var m := get_tree().root.get_node_or_null("Musica")
	if m == null:
		return
	if not m.playing:
		m.play()
	if m.volume_db < -40.0:
		create_tween().tween_property(m, "volume_db", -10.0, 2.0)
