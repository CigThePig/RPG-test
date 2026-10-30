extends Node
signal notice(text: String)
var deliver_reward: Callable
var claiming := false
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
var dialogue_epoch := 0
var choices: VBoxContainer

func _ready() -> void:
	add_child(manager)
	register_quests()
	# Load the imported DialogueResource. FileAccess cannot read the source .dialogue in an exported PCK.
	dialogue = load("res://game/data/warden.dialogue") as DialogueResource
	if dialogue == null or dialogue.lines.is_empty() or not dialogue.cues.has("warden"):
		push_error("Missing or invalid imported warden dialogue resource")
		assert(false, "Missing or invalid imported warden dialogue resource")
		return
	print("MOSSLIGHT_DIALOGUE_READY")

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
	print("MOSSLIGHT_QUEST_ACCEPTED id=%d" % id)
	if not q.stages.is_empty():
		reconcile_adventure(q)
	elif q.event_kind == "item_acquired" and item_service != null:
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
	if kind == "region_entered": flags["region"] = value
	if kind == "landmark_discovered":
		flags["discovery:" + target] = value
		var lore: String = {"forgotten-archive": "The light powered Reach’s road wards before the ruin fell.", "abandoned-shrine": "The shrine asks that the light be carried home to protect travellers.", "collapsed-storeroom": "Expedition supplies may remain among the collapsed shelves."}.get(value, "")
		if not lore.is_empty(): flags["lore:" + target] = lore
		notice.emit("Discovered: " + landmark_name(value))
	if kind == "defeated":
		flags["defeated:" + value] = int(flags.get("defeated:" + value, 0)) + maxi(0, count)
	for q in manager.get_active_quests().duplicate():
		var previous: int = q.stage_index
		manager.update_quest(q, {"kind": kind, "actor": actor, "target": target, "value": value, "count": count})
		if q.stage_index != previous and q.stage_index < q.stages.size(): notice.emit(q.stages[q.stage_index].text)
		finish_quest(q)

func reconcile_adventure(q: Quest) -> void:
	# Possession is retroactive; reporting always requires a new deliberate interaction.
	if item_service != null and item_service.count_item("player", "relic") > 0:
		q.stage_index = maxi(q.stage_index, 2)
		if flags.get("region", "reach") == "reach": q.stage_index = maxi(q.stage_index, 3)
	elif flags.get("region", "reach") == "ruin":
		q.stage_index = maxi(q.stage_index, 1)
	q.progress = q.stage_index
	q.stage_id = "complete" if q.stage_index >= q.stages.size() else str(q.stages[q.stage_index].id)

func reconcile_world(region: String) -> void:
	# Called only after the unified snapshot has restored actors and Gloot.
	# Older current-generation saves have no region/stage fields in Narrative.
	for q in manager.get_active_quests():
		if not q.stages.is_empty():
			flags["region"] = region
			reconcile_adventure(q)

func accepted(id: int) -> bool:
	return flags.get("accepted_%d" % id, false)

func ready_to_report() -> bool:
	var q = manager._get_quest_by_id(2)
	return q != null and manager.is_quest_active(q) and q.stage_index == 3 and flags.get("region", "reach") == "reach" and item_service != null and item_service.count_item("player", "relic") > 0

func report_to_warden() -> bool:
	if not ready_to_report(): return false
	receive("npc_report", "player", "warden", "reach", 1)
	return completed(2)

func claim_reward(id: int) -> bool:
	var q = manager._get_quest_by_id(id)
	if claiming or q == null or not completed(id) or q.rewarded or not deliver_reward.is_valid(): return false
	claiming = true
	var delivered: bool = deliver_reward.call(q.reward_item, q.reward_count)
	if delivered:
		q.rewarded = true
		reputation.emit("wardens", 5)
		notice.emit("Reward received: %s ×%d" % [q.reward_item, q.reward_count])
	else:
		notice.emit("Inventory full — your reward is waiting with the warden.")
	claiming = false
	return delivered

func claim_pending() -> void:
	for q in definitions:
		if completed(q.id) and not q.rewarded: claim_reward(q.id)

func has_pending() -> bool:
	for q in definitions:
		if completed(q.id) and not q.rewarded: return true
	return false

func finish_quest(q: Quest) -> void:
	if q.objective_completed and not q.failed and manager.is_quest_active(q):
		manager.complete_quest(q)
		print("MOSSLIGHT_QUEST_COMPLETED id=%d" % q.id)
		notice.emit("Quest complete: " + q.quest_name)
		claim_reward(q.id)

func landmark_name(id: String) -> String:
	return id.replace("_", " ").replace("-", " ").capitalize()

func fail_quest(id: int) -> void:
	var q = manager._get_quest_by_id(id)
	if q != null and not completed(id):
		q.failed = true
		q.updated.emit()

func journal() -> String:
	var lines := PackedStringArray()
	for q in definitions:
		var status: String = "Complete" if manager.is_quest_completed(q) else "Failed" if q.failed else "Active" if manager.is_quest_active(q) else "Available"
		if not q.stages.is_empty():
			var objective: String = q.stages[mini(q.stage_index, q.stages.size() - 1)].text if not completed(q.id) else "The light is safe in Reach."
			lines.append("%s · %s\n%s" % [q.quest_name, status, objective])
			for i in range(mini(q.stage_index, q.stages.size())): lines.append("✓ " + q.stages[i].text)
		else:
			lines.append("%s · %s\n%s (%d/%d)" % [q.quest_name, status, q.quest_objective, q.progress, q.required])
		if completed(q.id) and not q.rewarded: lines.append("Reward pending — speak to the warden after making pack space.")
	var discoveries := PackedStringArray()
	for key in flags:
		if str(key).begins_with("discovery:"): discoveries.append(landmark_name(flags[key]))
	if not discoveries.is_empty(): lines.append("Explored: " + ", ".join(discoveries))
	for key in flags:
		if str(key).begins_with("lore:"): lines.append(flags[key])
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
				if seen.has(int(id)) or not data.quests.has(str(int(id))): return false
				seen[int(id)] = true
	manager.reset_pool()
	definitions.clear()
	register_quests()
	if not data.pools.is_empty():
		manager.reset_pool()
		# Quests since removed from quests.json have no definition and are ignored.
		manager.restore_pool_state_from_dict(data.pools, definitions.duplicate())
		# Quests authored after this save was written start out available.
		for q in definitions: manager.mark_quest_as_available(q)
	for q in definitions:
		var saved: Dictionary = data.quests.get(str(q.id), {})
		for key in ["progress", "failed", "rewarded", "objective_completed", "stage_index", "stage_id"]:
			if saved.has(key): q.set(key, saved[key])
		if not q.stages.is_empty():
			q.stage_index = clampi(q.stage_index, 0, q.stages.size())
			for i in q.stages.size():
				if q.stages[i].id == q.stage_id: q.stage_index = i
		if not q.stages.is_empty() and manager.is_quest_completed(q):
			q.stage_index = q.stages.size() # Preserve historical completions; never re-award.
			q.progress = q.stage_index
			q.objective_completed = true
	flags = data.flags
	speaking = false
	advancing = false
	dialogue_epoch += 1
	if ui != null: ui.hide()
	return true

func talk() -> void:
	if speaking or dialogue == null: return
	speaking = true
	print("MOSSLIGHT_WARDEN_DIALOGUE")
	if ui == null: build_ui()
	ui.show()
	advance("warden")

func build_ui() -> void:
	var layer := CanvasLayer.new()
	add_child(layer)
	ui = PanelContainer.new()
	var theme := Theme.new()
	theme.default_font_size = 22
	ui.theme = theme
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
	var epoch := dialogue_epoch
	var next_line = await dialogue.get_next_dialogue_line(cue, [self])
	if epoch != dialogue_epoch: return
	current_line = next_line
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
			if not response.is_allowed: continue
			var button := Button.new()
			button.custom_minimum_size.y = 64
			button.text = response.text
			button.disabled = not response.is_allowed
			button.pressed.connect(func(): advance(response.next_id))
			choices.add_child(button)
	if OS.has_feature("android"):
		await get_tree().process_frame
		if epoch != dialogue_epoch: return
		for button in choices.get_children():
			if button is Button:
				var center: Vector2 = button.global_position + button.size / 2
				print("MOSSLIGHT_DIALOGUE_CHOICE text=%s x=%.1f y=%.1f" % [button.text, center.x, center.y])
