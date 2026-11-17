extends Node
## Autoload "Partida": el progreso del jugador.
## Sobrevive al reinicio del nivel al morir y, además, se guarda en disco
## (user://partida.cfg) para poder elegir "Continuar" desde la pantalla de título.
## Registrar en Proyecto > Configuración del Proyecto > Globales (Autoload) con el nombre "Partida".

const ARCHIVO := "user://partida.cfg"

var hay_checkpoint: bool = false
var punto_reaparicion: Vector2 = Vector2.ZERO
## Ruta del santuario activado, para que siga encendido después de reaparecer.
var id_santuario: String = ""
## Los jefes derrotados no reaparecen y el Fuego se conserva al morir (GDD).
## Al cambiar, se guarda solo en disco.
var domador_derrotado: bool = false:
	set(valor):
		domador_derrotado = valor
		guardar()
var tiene_fuego: bool = false:
	set(valor):
		tiene_fuego = valor
		guardar()
## La Barricada de Telones (tramo 5) ya se quemó: no vuelve a aparecer.
var barricada_quemada: bool = false:
	set(valor):
		barricada_quemada = valor
		guardar()

var _cargando: bool = false


func _ready() -> void:
	cargar()


func guardar_checkpoint(posicion: Vector2, id: String) -> void:
	hay_checkpoint = true
	punto_reaparicion = posicion
	id_santuario = id
	guardar()


## Partida nueva: borra el progreso en memoria y en disco.
## La llama el prólogo al empezar "Nuevo juego".
func borrar_checkpoint() -> void:
	_cargando = true  # evita guardar mientras se resetea
	hay_checkpoint = false
	punto_reaparicion = Vector2.ZERO
	id_santuario = ""
	domador_derrotado = false
	tiene_fuego = false
	barricada_quemada = false
	_cargando = false
	if FileAccess.file_exists(ARCHIVO):
		DirAccess.remove_absolute(ProjectSettings.globalize_path(ARCHIVO))


## La pantalla de título la usa para mostrar "Continuar".
func hay_partida_guardada() -> bool:
	return FileAccess.file_exists(ARCHIVO)


func guardar() -> void:
	if _cargando:
		return
	var cfg := ConfigFile.new()
	cfg.set_value("checkpoint", "activo", hay_checkpoint)
	cfg.set_value("checkpoint", "posicion", punto_reaparicion)
	cfg.set_value("checkpoint", "santuario", id_santuario)
	cfg.set_value("progreso", "domador_derrotado", domador_derrotado)
	cfg.set_value("progreso", "tiene_fuego", tiene_fuego)
	cfg.set_value("progreso", "barricada_quemada", barricada_quemada)
	cfg.save(ARCHIVO)


func cargar() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(ARCHIVO) != OK:
		return
	_cargando = true
	hay_checkpoint = cfg.get_value("checkpoint", "activo", false)
	punto_reaparicion = cfg.get_value("checkpoint", "posicion", Vector2.ZERO)
	id_santuario = cfg.get_value("checkpoint", "santuario", "")
	domador_derrotado = cfg.get_value("progreso", "domador_derrotado", false)
	tiene_fuego = cfg.get_value("progreso", "tiene_fuego", false)
	barricada_quemada = cfg.get_value("progreso", "barricada_quemada", false)
	_cargando = false
