extends CharacterBody2D

@export var move_speed: float = 200.0
@export var jump_velocity: float = -550.0
@export var gravity: float = 1200.0
@export var fall_limit: float = 800.0

func _physics_process(delta):
	if not is_on_floor():
		velocity.y += gravity * delta

	if Input.is_action_just_pressed("jump") and is_on_floor():
		velocity.y = jump_velocity

	var input_axis = Input.get_axis("mover_left", "mover_right")
	velocity.x = input_axis * move_speed

	if input_axis != 0:
		$Sprite2D.flip_h = input_axis < 0

	move_and_slide()

	if not is_on_floor():
		$Sprite2D.play("jump")
	elif input_axis != 0:
		$Sprite2D.play("walk")
	else:
		$Sprite2D.play("idle")

	if global_position.y > fall_limit:
		get_tree().reload_current_scene()
