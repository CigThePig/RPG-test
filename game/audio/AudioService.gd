extends Node
const Config = preload("res://addons/gdfxr/SFXRConfig.gd")
const Generator = preload("res://addons/gdfxr/SFXRGenerator.gd")
var effects: Dictionary = {}
var player := AudioStreamPlayer.new()
var ambience := AudioStreamPlayer.new()
var music := AudioStreamPlayer.new()

func _ready() -> void:
	add_child(player)
	add_child(ambience)
	add_child(music)
	player.volume_db = -8
	for kind in ["hit", "cast", "step", "ui"]:
		var config := Config.new()
		config.wave_type = Config.WaveType.SINE_WAVE if kind == "cast" else Config.WaveType.SQUARE_WAVE
		config.p_base_freq = 0.35 if kind == "cast" else 0.12 if kind == "hit" else 0.2
		config.p_env_sustain = 0.06 if kind == "cast" else 0.025
		config.p_env_decay = 0.12 if kind == "cast" else 0.06
		config.p_freq_ramp = -0.3
		effects[kind] = Generator.new().generate_audio_stream(config)

func cue(kind: String) -> void:
	if effects.has(kind):
		player.stream = effects[kind]
		player.play()

func play_ambience(stream: AudioStream) -> void:
	ambience.stream = stream
	ambience.play()

func play_music(stream: AudioStream) -> void:
	music.stream = stream
	music.play()

func export_effects(directory: String = "user://audio") -> void:
	DirAccess.make_dir_recursive_absolute(directory)
	for key in effects: effects[key].save_to_wav(directory.path_join(key + ".wav"))
