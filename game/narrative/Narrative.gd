extends Node
signal reward(item: String, count: int)
signal reputation(faction: String, amount: int)
var item_service: Node
var manager := QuestSystemManagerAPI.new()
var definitions: Array[Quest] = []
var flags: Dictionary = {}
var current_line: DialogueLine
var dialogue: DialogueResource
var speaking: bool = false
var ui: PanelContainer
var text_label: Label
var advancing := false
var choices: VBoxContainer

func _ready() -> void:
	add_child(manager)
	register_quests()
	dialogue = DialogueManager.create_resource_from_text(FileAccess.get_file_as_string("res://game/data/warden.dialogue"))

func register_quests() -> void:
	for row in JSON.parse_string(FileAccess.get_file_as_string("res://game/data/quests.json")):
		var q = load("res://game/narrative/EventQuest.gd").new()
		for key in row: q.set(key, row[key])
		definitions.append(q)
		manager.mark_quest_as_available(q)

func accept_quest(id: int) -> bool:
	var q = manager._get_quest_by_id(id)
	if q == null or q.failed or manager.is_quest_completed(q): return false
	if q.prerequisite > 0 and not completed(q.prerequisite): return false
	if manager.is_quest_active(q): return false
	manager.start_quest(q)
	flags["accepted_%d" % id] = true
	if q.event_kind == "item_acquired" and item_service != null:
		var owned: int = item_service.count_item("player", q.target_value)
		q.progress = maxi(q.progress, mini(q.required, owned))
		q.objective_completed = q.progress >= q.required
		finish_quest(q)
	elif q.event_kind == "defeated":
		q.progress = mini(q.required, int(flags.get("defeated:" + q.target_value, 0)))
		q.objective_completed = q.progress >= q.required
		finish_quest(q)
	return true

func completed(id: int) -> bool:
	var q = manager._get_quest_by_id(id)
	return q != null and manager.is_quest_completed(q)

func receive(kind: String, actor: String, target: String, value: String, count: int) -> void:
	if actor != "player": return
	if kind == "defeated":
		flags["defeated:" + value] = int(flags.get("defeated:" + value, 0)) + maxi(0, count)
	for q in manager.get_active_quests().duplicate():
		manager.update_quest(q, {"kind": kind, "actor": actor, "target": target, "value": value, "count": count})
		finish_quest(q)

func finish_quest(q: Quest) -> void:
	if q.objective_completed and not q.failed and manager.is_quest_active(q):
		manager.complete_quest(q)
		if not q.rewarded:
			q.rewarded = true
			reward.emit(q.reward_item, q.reward_count)
			reputation.emit("wardens", 5)

func fail_quest(id: int) -> void:
	var q = manager._get_quest_by_id(id)
	if q != null and not completed(id):
		q.failed = true
		q.updated.emit()

func journal() -> String:
	var lines := PackedStringArray()
	for q in definitions:
		var status: String = "Complete" if manager.is_quest_completed(q) else "Failed" if q.failed else "Active" if manager.is_quest_active(q) else "Available"
		lines.append("%s · %s\n%s (%d/%d)" % [q.quest_name, status, q.quest_objective, q.progress, q.required])
	return "\n\n".join(lines)

func export_state() -> String:
	return JSON.stringify({"flags": flags, "pools": manager.pool_state_as_dict(), "quests": manager.serialize_quests()})

func import_state(text: String) -> bool:
	var data = JSON.parse_string(text)
	if not data is Dictionary or not data.has_all(["flags", "pools", "quests"]): return false
	if not data.flags is Dictionary or not data.pools is Dictionary or not data.quests is Dictionary: return false
	if not data.pools.is_empty():
		var seen := {}
		for pool in ["available", "active", "completed"]:
			if not data.pools.has(pool) or not data.pools[pool] is Array: return false
			for id in data.pools[pool]:
				if seen.has(int(id)) or not definitions.any(func(q): return q.id == int(id)): return false
				if not data.quests.has(str(int(id))): return false
				seen[int(id)] = true
		if seen.size() != definitions.size(): return false
	manager.reset_pool()
	definitions.clear()
	register_quests()
	if not data.pools.is_empty():
		manager.reset_pool()
		manager.restore_pool_state_from_dict(data.pools, definitions.duplicate())
	manager.deserialize_quests(data.quests)
	flags = data.flags
	speaking = false
	if ui != null: ui.hide()
	return true

func talk() -> void:
	if speaking: return
	speaking = true
	if ui == null: build_ui()
	ui.show()
	advance("warden")

func build_ui() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	ui = PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.065, 0.1, 0.1, 0.98)
	style.border_color = Color(0.48, 0.52, 0.34)
	style.set_border_width_all(2)
	style.set_content_margin_all(18)
	ui.add_theme_stylebox_override("panel", style)
	ui.position = Vector2(180, 300)
	ui.custom_minimum_size = Vector2(920, 240)
	layer.add_child(ui)
	var box := VBoxContainer.new()
	ui.add_child(box)
	text_label = Label.new()
	text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text_label.custom_minimum_size = Vector2(660, 70)
	box.add_child(text_label)
	choices = VBoxContainer.new()
	box.add_child(choices)

func advance(cue: String) -> void:
	if advancing: return
	advancing = true
	for child in choices.get_children():
		if child is Button: child.disabled = true
	current_line = await dialogue.get_next_dialogue_line(cue, [self])
	advancing = false
	for child in choices.get_children():
		choices.remove_child(child)
		child.queue_free()
	if current_line == null:
		speaking = false
		ui.hide()
		return
	text_label.text = current_line.character + ": " + current_line.text
	if current_line.responses.is_empty():
		var button := Button.new()
		button.custom_minimum_size.y = 64
		button.text = "Continue"
		button.pressed.connect(func(): advance(current_line.next_id))
		choices.add_child(button)
	else:
		for response in current_line.responses:
			var button := Button.new()
			button.custom_minimum_size.y = 64
			button.text = response.text
			button.disabled = not response.is_allowed
			button.pressed.connect(func(): advance(response.next_id))
			choices.add_child(button)
