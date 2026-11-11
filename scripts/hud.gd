extends CanvasLayer
## HUD: vida y maná, fijos en pantalla (no se mueven con la cámara).
##
## Usa IMÁGENES si existen en estas rutas (PNG con fondo transparente, cualquier tamaño):
##   res://assets/ui/vida_llena.png      → ícono de una unidad de vida
##   res://assets/ui/vida_vacia.png      → la misma, perdida (opcional: si no está, se oscurece la llena)
##   res://assets/items/cristal_mana.png → ícono al lado de la barra de maná
##   res://assets/ui/marco_mana.png      → marco que se estira sobre la barra (opcional)
## Lo que no exista se dibuja por código con trazo negro grueso (Heavy Inking).

const RUTA_VIDA_LLENA := "res://assets/ui/vida_llena.png"
const RUTA_VIDA_VACIA := "res://assets/ui/vida_vacia.png"
const RUTA_ICONO_MANA := "res://assets/items/cristal_mana.png"
const RUTA_MARCO_MANA := "res://assets/ui/marco_mana.png"

const CARMESI := Color("9C171F")       # Rojo Carmesí Dramático (GDD)
const CARMESI_OSCURO := Color("4A0A0F")
const CIAN := Color("3FD8EC")          # color del cristal de maná
const CIAN_OSCURO := Color("0A6A79")
const BLANCO_HUESO := Color("F5F2EA")
const TINTA := Color("0E0B0B")

@export var margen: Vector2 = Vector2(28, 24)
@export var alto_icono_vida: float = 44.0
@export var separacion_vida: float = 8.0
@export var ancho_barra: float = 230.0
@export var alto_barra: float = 14.0
@export var alto_icono_mana: float = 46.0

var _vida: int = 0
var _vida_max: int = 0
var _mana: int = 0
var _mana_max: int = 1
var _mana_mostrado: float = 0.0
var _mana_fantasma: float = 0.0
var _pulso: Dictionary = {}
var _t_sacudir_barra: float = 0.0
var _t: float = 0.0

var _tex_vida_llena: Texture2D
var _tex_vida_vacia: Texture2D
var _tex_icono_mana: Texture2D
var _tex_marco_mana: Texture2D

var _lienzo: Control
var _jugador: Node

const DURACION_PULSO := 0.45


func _ready() -> void:
	layer = 10
	_tex_vida_llena = _cargar(RUTA_VIDA_LLENA)
	_tex_vida_vacia = _cargar(RUTA_VIDA_VACIA)
	_tex_icono_mana = _cargar(RUTA_ICONO_MANA)
	_tex_marco_mana = _cargar(RUTA_MARCO_MANA)
	_lienzo = Control.new()
	_lienzo.set_anchors_preset(Control.PRESET_FULL_RECT)
	_lienzo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_lienzo)
	_lienzo.draw.connect(_dibujar)
	_conectar.call_deferred()


func _cargar(ruta: String) -> Texture2D:
	if ResourceLoader.exists(ruta):
		return load(ruta) as Texture2D
	return null


func _conectar() -> void:
	_jugador = get_tree().get_first_node_in_group("jugador")
	if _jugador == null:
		return
	_jugador.vida_cambiada.connect(_al_cambiar_vida)
	_jugador.mana_cambiado.connect(_al_cambiar_mana)
	_jugador.sin_mana.connect(func(): _t_sacudir_barra = 0.3)
	_vida = _jugador.vida
	_vida_max = _jugador.vida_maxima
	_mana = _jugador.mana
	_mana_max = maxi(_jugador.mana_maximo, 1)
	_mana_mostrado = _mana
	_mana_fantasma = _mana


func _process(delta: float) -> void:
	_t += delta
	_mana_mostrado = lerpf(_mana_mostrado, float(_mana), 1.0 - exp(-12.0 * delta))
	if _mana_fantasma > _mana_mostrado:
		_mana_fantasma = move_toward(_mana_fantasma, _mana_mostrado, _mana_max * 0.5 * delta)
	else:
		_mana_fantasma = _mana_mostrado
	for i in _pulso.keys():
		_pulso[i] -= delta
		if _pulso[i] <= 0.0:
			_pulso.erase(i)
	_t_sacudir_barra -= delta
	_lienzo.queue_redraw()


func _al_cambiar_vida(actual: int, maximo: int) -> void:
	for i in maxi(maximo, _vida_max):
		if (i < actual) != (i < _vida):
			_pulso[i] = DURACION_PULSO
	_vida = actual
	_vida_max = maximo


func _al_cambiar_mana(actual: int, maximo: int) -> void:
	_mana = actual
	_mana_max = maxi(maximo, 1)


# ------------------------------------------------------------------
# Dibujo
# ------------------------------------------------------------------

func _dibujar() -> void:
	if _jugador == null:
		return
	_dibujar_vida()
	_dibujar_mana()


func _dibujar_vida() -> void:
	for i in _vida_max:
		var centro := margen + Vector2(i * (alto_icono_vida + separacion_vida) + alto_icono_vida * 0.5, alto_icono_vida * 0.5)
		var escala := 1.0
		var destello := 0.0
		var lleno := i < _vida
		if _pulso.has(i):
			var p: float = 1.0 - _pulso[i] / DURACION_PULSO
			escala = 1.0 + 0.45 * sin(p * PI)
			destello = 1.0 - p
			centro += Vector2(randf_range(-4, 4), randf_range(-4, 4)) * (1.0 - p)
		elif lleno and _vida == 1:
			escala = 1.0 + 0.1 * maxf(0.0, sin(_t * 7.0))  # el último late

		if _tex_vida_llena:
			_vida_con_imagen(centro, alto_icono_vida * escala, lleno, destello)
		else:
			_vida_dibujada(centro, alto_icono_vida * escala, lleno, destello)


func _vida_con_imagen(centro: Vector2, alto: float, lleno: bool, destello: float) -> void:
	var tex := _tex_vida_llena
	var color := Color.WHITE
	if not lleno:
		if _tex_vida_vacia:
			tex = _tex_vida_vacia
		else:
			color = Color(0.22, 0.18, 0.18, 0.85)
	color = color.lerp(Color(3, 3, 3), destello * 0.6)
	var tam := Vector2(alto * tex.get_width() / float(tex.get_height()), alto)
	_lienzo.draw_texture_rect(tex, Rect2(centro - tam * 0.5, tam), false, color)


## Corazón de tinta: contorno negro grueso, relleno carmesí con brillo,
## y agrietado cuando está vacío.
func _vida_dibujada(centro: Vector2, alto: float, lleno: bool, destello: float) -> void:
	var k := alto / 36.0
	_lienzo.draw_colored_polygon(_puntos_corazon(centro, k * 1.18), TINTA)  # trazo grueso
	if lleno:
		_lienzo.draw_colored_polygon(_puntos_corazon(centro, k), CARMESI.lerp(BLANCO_HUESO, destello))
		_lienzo.draw_colored_polygon(_puntos_corazon(centro + Vector2(2, 3) * k, k * 0.62), CARMESI_OSCURO.lerp(BLANCO_HUESO, destello))
		_lienzo.draw_circle(centro + Vector2(-7, -6) * k, 3.0 * k, Color(BLANCO_HUESO, 0.75))
	else:
		_lienzo.draw_colored_polygon(_puntos_corazon(centro, k), Color("2A2222").lerp(BLANCO_HUESO, destello))
		var grieta := PackedVector2Array([
			centro + Vector2(0, -9) * k, centro + Vector2(-3, -3) * k,
			centro + Vector2(3, 2) * k, centro + Vector2(-1, 8) * k, centro + Vector2(0, 13) * k])
		_lienzo.draw_polyline(grieta, TINTA, 2.5 * k)


func _puntos_corazon(centro: Vector2, k: float) -> PackedVector2Array:
	var puntos := PackedVector2Array()
	for n in 40:
		var a := TAU * n / 40.0
		var x := 16.0 * pow(sin(a), 3)
		var y := -(13.0 * cos(a) - 5.0 * cos(2 * a) - 2.0 * cos(3 * a) - cos(4 * a))
		puntos.append(centro + Vector2(x, y) * k)
	return puntos


func _dibujar_mana() -> void:
	var debilitado := _mana <= 0
	var y_centro := margen.y + alto_icono_vida + 14.0 + alto_icono_mana * 0.5
	var x := margen.x
	if _t_sacudir_barra > 0.0:
		x += randf_range(-4, 4)

	# Ícono del cristal.
	var ancho_icono := alto_icono_mana * 0.7
	if _tex_icono_mana:
		var tam := Vector2(alto_icono_mana * _tex_icono_mana.get_width() / float(_tex_icono_mana.get_height()), alto_icono_mana)
		ancho_icono = tam.x
		var color_icono := Color(0.35, 0.35, 0.38) if debilitado else Color.WHITE
		_lienzo.draw_texture_rect(_tex_icono_mana, Rect2(Vector2(x, y_centro - tam.y * 0.5), tam), false, color_icono)
	else:
		var c := Vector2(x + ancho_icono * 0.5, y_centro)
		var r := alto_icono_mana * 0.3
		var rombo := PackedVector2Array([c + Vector2(0, -r * 1.5), c + Vector2(r, 0), c + Vector2(0, r * 1.5), c + Vector2(-r, 0)])
		_lienzo.draw_colored_polygon(rombo, CIAN_OSCURO if debilitado else CIAN)
		rombo.append(rombo[0])
		_lienzo.draw_polyline(rombo, TINTA, 3.0)

	# Barra.
	var pos := Vector2(x + ancho_icono + 6.0, y_centro - alto_barra * 0.5)
	var fondo := Rect2(pos, Vector2(ancho_barra, alto_barra))
	_lienzo.draw_rect(fondo.grow(4.0), TINTA)                      # trazo grueso
	_lienzo.draw_rect(fondo, Color("12181A"))
	var ancho_fantasma := ancho_barra * clampf(_mana_fantasma / _mana_max, 0.0, 1.0)
	_lienzo.draw_rect(Rect2(pos, Vector2(ancho_fantasma, alto_barra)), Color(BLANCO_HUESO, 0.5))
	var ancho_mana := ancho_barra * clampf(_mana_mostrado / _mana_max, 0.0, 1.0)
	if ancho_mana > 0.5:
		_lienzo.draw_rect(Rect2(pos, Vector2(ancho_mana, alto_barra)), CIAN_OSCURO)
		_lienzo.draw_rect(Rect2(pos, Vector2(ancho_mana, alto_barra * 0.6)), CIAN)
		_lienzo.draw_rect(Rect2(pos + Vector2(0, 2), Vector2(ancho_mana, 2)), Color(1, 1, 1, 0.45))
		# Brillo en la punta de la barra.
		var punta := pos + Vector2(ancho_mana, alto_barra * 0.5)
		_lienzo.draw_circle(punta, alto_barra * (0.9 + 0.15 * sin(_t * 6.0)), Color(CIAN, 0.35))
	if _tex_marco_mana:
		_lienzo.draw_texture_rect(_tex_marco_mana, fondo.grow(8.0), false)
	elif debilitado and int(_t * 4.0) % 2 == 0:
		_lienzo.draw_rect(fondo.grow(4.0), CARMESI, false, 4.0)

	if debilitado:
		_lienzo.draw_string(ThemeDB.fallback_font, pos + Vector2(0, alto_barra + 20.0), "DEBILITADO",
				HORIZONTAL_ALIGNMENT_LEFT, -1, 14, CARMESI)
