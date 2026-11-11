class_name CristalMana
extends Area2D
## Cristal de maná que sueltan los enemigos (GDD: pequeño +5, mediano +15, especial +30).
## Flota en el lugar, vuela hacia el Mago cuando pasa cerca y desaparece después de un rato.
##
## Imagen: res://assets/items/cristal_mana.png (PNG con fondo transparente).
## Los tres tamaños usan la misma imagen, más grande cuanto más vale.
## Si se quiere una imagen distinta por tamaño, agregar cristal_mediano.png o cristal_especial.png.

enum Tamanio { PEQUENIO, MEDIANO, ESPECIAL }

const VALORES := {Tamanio.PEQUENIO: 5, Tamanio.MEDIANO: 15, Tamanio.ESPECIAL: 30}
const RUTA_IMAGEN := "res://assets/items/cristal_mana.png"
const IMAGENES_PROPIAS := {
	Tamanio.MEDIANO: "res://assets/items/cristal_mediano.png",
	Tamanio.ESPECIAL: "res://assets/items/cristal_especial.png",
}
## Alto en pantalla (se escala solo, sin importar el tamaño del PNG).
const ALTOS := {Tamanio.PEQUENIO: 30.0, Tamanio.MEDIANO: 40.0, Tamanio.ESPECIAL: 52.0}
const CIAN := Color("3FD8EC")
const TINTA := Color("0E0B0B")

@export var tamanio: Tamanio = Tamanio.PEQUENIO
@export var distancia_iman: float = 140.0
@export var velocidad_iman: float = 600.0
## Distancia al centro del Mago a la que se considera recogido.
@export var distancia_recoger: float = 40.0
## Si está activado, con el maná lleno el cristal no se agarra (queda para después).
@export var solo_si_falta_mana: bool = false
## Segundos hasta desaparecer. Los últimos 3 s parpadea.
@export var duracion: float = 15.0

var _t: float = 0.0
var _jugador: Node2D
var _recogido: bool = false
var _textura: Texture2D


func _ready() -> void:
	var ruta: String = IMAGENES_PROPIAS.get(tamanio, RUTA_IMAGEN)
	if not ResourceLoader.exists(ruta):
		ruta = RUTA_IMAGEN
	if ResourceLoader.exists(ruta):
		_textura = load(ruta)
	collision_layer = 0
	collision_mask = 0
	set_collision_mask_value(2, true)
	var forma := CollisionShape2D.new()
	var circulo := CircleShape2D.new()
	circulo.radius = ALTOS[tamanio] * 0.6
	forma.shape = circulo
	add_child(forma)
	body_entered.connect(_al_tocar)
	_jugador = get_tree().get_first_node_in_group("jugador")
	scale = Vector2.ZERO
	create_tween().tween_property(self, "scale", Vector2.ONE, 0.25) \
			.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)


func _al_tocar(cuerpo: Node) -> void:
	if cuerpo == _jugador and _puede_recoger():
		_recoger()


func _puede_recoger() -> bool:
	if _recogido or _jugador == null or not is_instance_valid(_jugador):
		return false
	return not solo_si_falta_mana or _jugador.mana < _jugador.mana_maximo


func _physics_process(delta: float) -> void:
	if _recogido:
		return
	_t += delta
	duracion -= delta
	if duracion <= 0.0:
		queue_free()
		return
	visible = duracion > 3.0 or int(duracion * 8.0) % 2 == 0
	queue_redraw()

	if not _puede_recoger():
		return
	var destino: Vector2 = _jugador.centro()
	var distancia := global_position.distance_to(destino)
	# Imán: si el Mago está cerca, el cristal vuela hacia él (cada vez más rápido).
	if distancia < distancia_iman:
		var rapidez := velocidad_iman * (1.0 + (1.0 - distancia / distancia_iman))
		global_position = global_position.move_toward(destino, rapidez * delta)
		distancia = global_position.distance_to(destino)
	if distancia < distancia_recoger:
		_recoger()


func _recoger() -> void:
	_recogido = true
	_jugador.recuperar_mana(VALORES[tamanio])
	visible = true
	var tw := create_tween().set_parallel()
	tw.tween_property(self, "scale", Vector2(2.0, 2.0), 0.2)
	tw.tween_property(self, "modulate:a", 0.0, 0.2)
	tw.chain().tween_callback(queue_free)


func _draw() -> void:
	var alto: float = ALTOS[tamanio]
	var flotar := Vector2(0, sin(_t * 4.0) * 4.0)
	# Resplandor que late detrás.
	draw_circle(flotar, alto * 0.75, Color(CIAN, 0.12 + 0.06 * sin(_t * 6.0)))
	if _textura:
		var tam := Vector2(alto * _textura.get_width() / float(_textura.get_height()), alto)
		var color := Color(1.4, 1.4, 1.4) if tamanio == Tamanio.ESPECIAL else Color.WHITE
		draw_texture_rect(_textura, Rect2(flotar - tam * 0.5, tam), false, color)
		return
	# Sin imagen: rombo dibujado.
	var r := alto * 0.3
	var puntos := PackedVector2Array([
		flotar + Vector2(0, -r * 1.6), flotar + Vector2(r, 0),
		flotar + Vector2(0, r * 1.6), flotar + Vector2(-r, 0)])
	draw_colored_polygon(puntos, CIAN)
	puntos.append(puntos[0])
	draw_polyline(puntos, TINTA, 3.0, true)
