extends Node
const Config = preload("res://addons/gdfxr/SFXRConfig.gd")
const Generator = preload("res://addons/gdfxr/SFXRGenerator.gd")
const VOICES := 8
const SAME_KIND_LIMIT := 2
var effects: Dictionary = {}
var voices: Array[AudioStreamPlayer] = []
var voice_kinds: Array[String] = []
var voice_priorities: Array[int] = []
# Bounded debug-only counters verify what actually reaches the audio mixer.
var cue_counts: Dictionary = {}
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
		# Short, soft air-slice; no percussive punch or low-frequency hit tail.
		"swing": [Config.WaveType.NOISE, .26, -.13, .03, .23, .48],
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
		# Enemy behaviour: a rising yelp of alarm, a falling growl, hooves, steel and stone.
		"alert": [Config.WaveType.SQUARE_WAVE, .30, .34, .04, .14, .25],
		"roar": [Config.WaveType.SAWTOOTH, .11, -.10, .22, .34, .05],
		"charge": [Config.WaveType.NOISE, .10, .18, .16, .30, .08],
		"guard": [Config.WaveType.SQUARE_WAVE, .46, -.20, .02, .10, .45],
		"guard_break": [Config.WaveType.NOISE, .34, -.48, .05, .30, .30],
		"crash": [Config.WaveType.NOISE, .07, -.20, .08, .34, .05],
		# Senses and the new attacks: a ringing turn of a blow, a wet burst, a soft prick of
		# attention, a snort on waking, a falling whine, a rising chime, a hollow toss.
		"block": [Config.WaveType.SQUARE_WAVE, .52, -.08, .03, .20, .50],
		"splash": [Config.WaveType.NOISE, .20, -.38, .07, .30, .10],
		"heard": [Config.WaveType.SINE_WAVE, .44, .12, .02, .12, .0],
		"wake": [Config.WaveType.SAWTOOTH, .16, .22, .05, .18, .15],
		"whimper": [Config.WaveType.SINE_WAVE, .42, -.34, .08, .24, .0],
		"mend": [Config.WaveType.SINE_WAVE, .36, .30, .10, .32, .0],
		"lob": [Config.WaveType.SINE_WAVE, .22, .30, .05, .20, .0],
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
		config.p_env_punch = .25 if kind in ["hit", "heavy_hit", "player_hit", "bite", "guard_break", "crash", "block", "splash"] else 0.0
		effects[kind] = Generator.new().generate_audio_stream(config)

func priority(kind: String) -> int:
	if kind in ["player_hit", "heavy_hit", "death", "guard_break", "crash"]: return 3
	if kind in ["hit", "avoid", "projectile_impact", "ui", "alert", "roar", "block", "splash"]: return 2
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
	# The air-only sword whoosh sits below actual contact impacts.
	voices[slot].volume_db = -15 if kind == "swing" else -8
	voices[slot].stream = effects[kind]
	voice_kinds[slot] = kind
	voice_priorities[slot] = importance
	voices[slot].play()
	cue_counts[kind] = int(cue_counts.get(kind, 0)) + 1

func debug_cue_count(kind: String) -> int:
	return int(cue_counts.get(kind, 0))

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
