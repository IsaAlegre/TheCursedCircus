extends Node
## Autoload "Partida": guarda los datos que tienen que sobrevivir
## cuando el nivel se reinicia al morir (por ahora, el último santuario).
## Registrar en Proyecto > Configuración del Proyecto > Globales (Autoload) con el nombre "Partida".

var hay_checkpoint: bool = false
var punto_reaparicion: Vector2 = Vector2.ZERO
## Ruta del santuario activado, para que siga encendido después de reaparecer.
var id_santuario: String = ""
## Experimento BOSS: los jefes derrotados no reaparecen y el Fuego se conserva al morir (GDD).
var domador_derrotado: bool = false
var tiene_fuego: bool = false


func guardar_checkpoint(posicion: Vector2, id: String) -> void:
	hay_checkpoint = true
	punto_reaparicion = posicion
	id_santuario = id


## Llamar al empezar una partida nueva (desde el menú principal, cuando exista).
func borrar_checkpoint() -> void:
	hay_checkpoint = false
	punto_reaparicion = Vector2.ZERO
	id_santuario = ""
	domador_derrotado = false
	tiene_fuego = false
