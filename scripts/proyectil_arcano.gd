extends Area2D
## Proyectil Arcano: hechizo inicial del Mago (GDD: no consume maná, daño bajo).
## Se dibuja por código en Violeta Arcano #8A2BE2 hasta tener un sprite propio.

@export var velocidad: float = 700.0
## Vida del Artista Corrupto = 30 → muere con 3 disparos (GDD).
@export var danio: int = 10
@export var duracion: float = 1.2

var direccion: Vector2 = Vector2.RIGHT
var lanzador: Node = null

const VIOLETA := Color("8A2BE2")
const BLANCO_HUESO := Color("F5F2EA")


func _ready() -> void:
	# Detecta el mundo (capa 1) y a los enemigos (capa 3). Nunca al jugador.
	collision_layer = 0
	collision_mask = 0
	set_collision_mask_value(1, true)
	set_collision_mask_value(3, true)
	rotation = direccion.angle()
	body_entered.connect(_al_tocar_cuerpo)


func _physics_process(delta: float) -> void:
	global_position += direccion * velocidad * delta
	duracion -= delta
	if duracion <= 0.0:
		queue_free()


func _al_tocar_cuerpo(cuerpo: Node) -> void:
	if cuerpo == lanzador:
		return
	if cuerpo.has_method("recibir_danio"):
		cuerpo.recibir_danio(danio, self)
	set_deferred("monitoring", false)
	queue_free()


func _draw() -> void:
	draw_line(Vector2(-30, 0), Vector2.ZERO, Color(VIOLETA, 0.45), 8.0)
	draw_circle(Vector2.ZERO, 14.0, Color(VIOLETA, 0.35))
	draw_circle(Vector2.ZERO, 9.0, VIOLETA)
	draw_circle(Vector2.ZERO, 4.0, BLANCO_HUESO)
