extends Area2D
## Recompensa del Domador: el Hechizo de Fuego flota donde se purificó el jefe.

signal recogido(jugador: Node)

const NARANJA := Color("FF5A00")
const AMARILLO := Color("FFC247")
const BLANCO_HUESO := Color("F5F2EA")

var _t: float = 0.0
var _base_y: float


func _ready() -> void:
	collision_layer = 0
	collision_mask = 0
	set_collision_mask_value(2, true)
	var forma := CollisionShape2D.new()
	var circulo := CircleShape2D.new()
	circulo.radius = 34.0
	forma.shape = circulo
	add_child(forma)
	_base_y = position.y
	body_entered.connect(_al_tocar)


func _process(delta: float) -> void:
	_t += delta
	position.y = _base_y + sin(_t * 2.5) * 8.0
	queue_redraw()


func _al_tocar(cuerpo: Node) -> void:
	if cuerpo.is_in_group("jugador"):
		set_deferred("monitoring", false)
		recogido.emit(cuerpo)
		var tw := create_tween().set_parallel(true)
		tw.tween_property(self, "scale", Vector2(2.5, 2.5), 0.4)
		tw.tween_property(self, "modulate:a", 0.0, 0.4)
		tw.chain().tween_callback(queue_free)


func _draw() -> void:
	var pulso := 1.0 + 0.12 * sin(_t * 6.0)
	draw_circle(Vector2.ZERO, 44.0 * pulso, Color(NARANJA, 0.15))
	draw_circle(Vector2.ZERO, 30.0 * pulso, Color(NARANJA, 0.35))
	# llama
	var puntos := PackedVector2Array()
	for i in 24:
		var a := TAU * i / 24.0
		var r := 20.0 + (10.0 + 4.0 * sin(_t * 12.0 + i)) * maxf(0.0, -sin(a))
		puntos.append(Vector2(cos(a) * 18.0, sin(a) * r))
	draw_colored_polygon(puntos, NARANJA)
	draw_circle(Vector2(0, 4), 10.0, AMARILLO)
	draw_circle(Vector2(0, 6), 4.0, BLANCO_HUESO)
