extends Area2D
## Onda de fuego rasante del Domador (fase 2). Recorre el piso a 350 px/s con 1 tile
## de alto: se esquiva saltando. No se puede bloquear.

var direccion: int = 1
var velocidad: float = 350.0
var danio: int = 1
var limite_izq: float = -INF
var limite_der: float = INF
var _t: float = 0.0
var _apagandose: bool = false

const ROJO := Color("E33224")
const NARANJA := Color("FF5A00")
const AMARILLO := Color("FFC247")


func _ready() -> void:
	collision_layer = 0
	collision_mask = 0
	set_collision_mask_value(2, true)  # solo el Mago
	var forma := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(56, 44)
	forma.shape = rect
	forma.position = Vector2(0, -22)
	add_child(forma)
	body_entered.connect(_al_tocar)


func _physics_process(delta: float) -> void:
	_t += delta
	position.x += direccion * velocidad * delta
	queue_redraw()
	if not _apagandose and (global_position.x < limite_izq or global_position.x > limite_der or _t > 6.0):
		_apagar()


func _al_tocar(cuerpo: Node) -> void:
	if _apagandose or not cuerpo.is_in_group("jugador"):
		return
	if cuerpo.recibir_danio(danio, self, false):
		_apagar()


func _apagar() -> void:
	_apagandose = true
	set_deferred("monitoring", false)
	var tw := create_tween()
	tw.tween_property(self, "modulate:a", 0.0, 0.25)
	tw.tween_callback(queue_free)


func _draw() -> void:
	# Llamas dibujadas por código: lenguas que oscilan, del rojo al amarillo.
	draw_circle(Vector2(0, -16), 46.0, Color(NARANJA, 0.18))
	for capa in [[ROJO, 1.0, 34.0], [NARANJA, 0.75, 26.0], [AMARILLO, 0.45, 16.0]]:
		var color: Color = capa[0]
		var escala: float = capa[1]
		var ancho: float = capa[2]
		var puntos := PackedVector2Array([Vector2(-ancho, 0)])
		for i in 7:
			var x := -ancho + (i + 0.5) * (2.0 * ancho / 7.0)
			var alto := (30.0 + 16.0 * sin(_t * 18.0 + i * 1.7) + 8.0 * sin(_t * 31.0 + i)) * escala
			puntos.append(Vector2(x - direccion * 4.0, -alto))
			puntos.append(Vector2(x + ancho / 7.0, -alto * 0.45))
		puntos.append(Vector2(ancho, 0))
		draw_colored_polygon(puntos, color)
