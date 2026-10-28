extends Node
const Config = preload("res://addons/gdfxr/SFXRConfig.gd")
const Generator = preload("res://addons/gdfxr/SFXRGenerator.gd")
const VOICES := 8
const SAME_KIND_LIMIT := 2
var effects: Dictionary = {}
var voices: Array[AudioStreamPlayer] = []
var voice_kinds: Array[String] = []
var voice_priorities: Array[int] = []
var ambience := AudioStreamPlayer.new()
var music := AudioStreamPlayer.new()

# Presets are synthesized once at startup, not on the combat path.
func _ready() -> void:
	add_child(ambience)
	add_child(music)
	for i in range(VOICES):
		var voice := AudioStreamPlayer.new()
		voice.volume_db = -8
		add_child(voice)
		voices.append(voice)
		voice_kinds.append("")
		voice_priorities.append(0)
	var specs := {
		"swing": [Config.WaveType.NOISE, .36, -.45, .04, .20, .70],
		"whiff": [Config.WaveType.NOISE, .23, -.32, .03, .18, .60],
		"attack": [Config.WaveType.SAWTOOTH, .18, -.22, .03, .16, .35],
		"bite": [Config.WaveType.NOISE, .27, -.65, .03, .14, .25],
		"hit": [Config.WaveType.NOISE, .14, -.45, .035, .20, .22],
		"heavy_hit": [Config.WaveType.NOISE, .09, -.28, .06, .26, .13],
		"player_hit": [Config.WaveType.NOISE, .12, -.35, .04, .22, .16],
		"dodge": [Config.WaveType.NOISE, .30, -.35, .035, .22, .65],
		"avoid": [Config.WaveType.SINE_WAVE, .39, .16, .03, .20, .0],
		"release": [Config.WaveType.SINE_WAVE, .31, .38, .06, .24, .0],
		"projectile_impact": [Config.WaveType.NOISE, .24, -.55, .035, .20, .25],
		"death": [Config.WaveType.SAWTOOTH, .13, -.30, .06, .30, .12],
		"cast": [Config.WaveType.SINE_WAVE, .35, -.30, .06, .22, .0],
		"step": [Config.WaveType.NOISE, .12, -.20, .02, .08, .35],
		"ui": [Config.WaveType.SINE_WAVE, .30, .10, .025, .15, .0]
	}
	for kind in specs:
		var spec: Array = specs[kind]
		var config := Config.new()
		config.wave_type = spec[0]
		config.p_base_freq = spec[1]
		config.p_freq_ramp = spec[2]
		config.p_env_sustain = spec[3]
		config.p_env_decay = spec[4]
		config.p_hpf_freq = spec[5]
		config.p_env_punch = .25 if kind in ["hit", "heavy_hit", "player_hit", "bite"] else 0.0
		effects[kind] = Generator.new().generate_audio_stream(config)

func priority(kind: String) -> int:
	if kind in ["player_hit", "heavy_hit", "death"]: return 3
	if kind in ["hit", "avoid", "projectile_impact", "ui"]: return 2
	return 1

func cue(kind: String) -> void:
	if not effects.has(kind): return
	var same := 0
	var slot := -1
	var replace := -1
	var importance := priority(kind)
	for i in range(voices.size()):
		if voices[i].playing:
			if voice_kinds[i] == kind: same += 1
			if voice_priorities[i] < importance and (replace < 0 or voice_priorities[i] < voice_priorities[replace]): replace = i
		elif slot < 0:
			slot = i
	if same >= SAME_KIND_LIMIT: return
	if slot < 0: slot = replace
	if slot < 0: return
	voices[slot].stop()
	voices[slot].stream = effects[kind]
	voice_kinds[slot] = kind
	voice_priorities[slot] = importance
	voices[slot].play()

func play_ambience(stream: AudioStream) -> void:
	ambience.stream = stream
	ambience.play()

func play_music(stream: AudioStream) -> void:
	music.stream = stream
	music.play()

func debug_state() -> Dictionary:
	var playing := 0
	for voice in voices:
		if voice.playing: playing += 1
	return {"voices": voices.size(), "playing": playing, "resources": effects.size(), "muted": AudioServer.is_bus_mute(0)}

func active_count(kind: String) -> int:
	var count := 0
	for i in range(voices.size()):
		if voices[i].playing and voice_kinds[i] == kind: count += 1
	return count

func stop_effects() -> void:
	for i in range(voices.size()):
		voices[i].stop()
		voice_kinds[i] = ""
		voice_priorities[i] = 0

func export_effects(directory: String = "user://audio") -> void:
	DirAccess.make_dir_recursive_absolute(directory)
	for key in effects: effects[key].save_to_wav(directory.path_join(key + ".wav"))
