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
var dialogue_body: BoxContainer
var story_scroll: ScrollContainer

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
	if kind == "lever_pulled" or kind == "seal_broken":
		flags["mechanism:%s:%s:%s" % [kind, target, value]] = true
	if kind == "lever_pulled" and not flags.has("lore:ruin-levers"):
		flags["lore:ruin-levers"] = "Old wardens built portcullis shortcuts so the light could be carried out quickly. A pulled lever keeps the way open."
	if kind == "brazier_kindled" and not flags.has("lore:ruin-braziers"):
		flags["lore:ruin-braziers"] = "The ruin’s light seals answer its braziers. Each flame you kindle stirs the two beside it; only a ring burning whole breaks the ward."
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
	var shortcuts := 0
	var seals := 0
	for key in flags:
		if str(key).begins_with("mechanism:lever_pulled:"): shortcuts += 1
		if str(key).begins_with("mechanism:seal_broken:"): seals += 1
	if shortcuts + seals > 0: lines.append("Ruin mechanisms: %d shortcut%s raised · %d light seal%s broken" % [shortcuts, "" if shortcuts == 1 else "s", seals, "" if seals == 1 else "s"])
	for key in flags:
		if str(key).begins_with("lore:"): lines.append(flags[key])
	return "\n\n".join(lines)

## Read-only structured objective for the gameplay HUD. Never mutates quest state;
## callers must not parse journal(). Returns {} when nothing needs tracking.
## Priority: first active quest, then an unclaimed reward, then the next quest the
## warden can offer (definition order throughout).
func objective() -> Dictionary:
	for q in definitions:
		if manager.is_quest_active(q) and not q.failed: return objective_row(q, "active")
	for q in definitions:
		if completed(q.id) and not q.rewarded: return objective_row(q, "reward_pending")
	for q in definitions:
		if q.failed or manager.is_quest_active(q) or manager.is_quest_completed(q) or accepted(q.id): continue
		if q.prerequisite > 0 and not completed(q.prerequisite): continue
		return objective_row(q, "available")
	return {}

func objective_row(q: Quest, status: String) -> Dictionary:
	var staged: bool = not q.stages.is_empty()
	var stage: int = mini(q.stage_index, q.stages.size() - 1) if staged else 0
	var text: String = q.quest_objective
	if status == "available": text = "Speak to the warden in Reach."
	elif status == "reward_pending": text = "Reward waiting — make pack space, then speak to the warden."
	elif staged: text = q.stages[stage].text
	return {
		"id": q.id, "title": q.quest_name, "status": status, "objective": text,
		"staged": staged, "stage_id": str(q.stages[stage].id) if staged else "",
		"progress": q.stage_index if staged else q.progress,
		"required": q.stages.size() if staged else q.required,
		"pending_reward": completed(q.id) and not q.rewarded,
		"reward": "%s ×%d" % [q.reward_item, q.reward_count],
	}

## Read-only journal rows for the Journal screen (PR #7 Part 3). One row per quest
## in definition order, with stage history, prerequisites and reward state.
## Never mutates; journal() remains for compatibility.
func journal_rows() -> Array:
	var rows: Array = []
	for q in definitions:
		var status: String = "completed" if manager.is_quest_completed(q) else "failed" if q.failed else "active" if manager.is_quest_active(q) else "available"
		var prerequisite_met: bool = q.prerequisite <= 0 or completed(q.prerequisite)
		if status == "available" and (accepted(q.id) or not prerequisite_met): status = "locked"
		var stages: Array = []
		for i in q.stages.size():
			stages.append({"id": str(q.stages[i].id), "text": q.stages[i].text,
				"done": i < q.stage_index or status == "completed", "current": status == "active" and i == q.stage_index})
		var prerequisite_title := ""
		if q.prerequisite > 0:
			var p = manager._get_quest_by_id(q.prerequisite)
			if p != null: prerequisite_title = p.quest_name
		rows.append({"id": q.id, "title": q.quest_name, "description": q.quest_description, "status": status,
			"objective": q.quest_objective, "staged": not q.stages.is_empty(), "stages": stages,
			"progress": q.stage_index if not q.stages.is_empty() else q.progress,
			"required": q.stages.size() if not q.stages.is_empty() else q.required,
			"prerequisite": q.prerequisite, "prerequisite_title": prerequisite_title, "prerequisite_met": prerequisite_met,
			"reward": "%s ×%d" % [q.reward_item, q.reward_count], "reward_item": q.reward_item, "reward_count": q.reward_count,
			"rewarded": q.rewarded, "pending_reward": status == "completed" and not q.rewarded})
	return rows

## Read-only discoveries, mechanism counts and lore gathered from flags.
func journal_extras() -> Dictionary:
	var discoveries: Array = []
	var lore: Array = []
	var shortcuts := 0
	var seals := 0
	for key in flags:
		var k := str(key)
		if k.begins_with("discovery:"): discoveries.append(landmark_name(flags[key]))
		elif k.begins_with("lore:"): lore.append(flags[key])
		elif k.begins_with("mechanism:lever_pulled:"): shortcuts += 1
		elif k.begins_with("mechanism:seal_broken:"): seals += 1
	return {"discoveries": discoveries, "lore": lore, "shortcuts": shortcuts, "seals": seals}

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

## Presentation supplied by RpgUi: the shared theme and a safe-area rectangle. Dialogue
## advancement, conditions and flags stay here; only layout and styling come from outside.
var ui_theme: Theme
var ui_rect := Rect2()
var speaker_label: Label
var choice_scroll: ScrollContainer

func present(theme: Theme, rect: Rect2) -> void:
	ui_theme = theme
	ui_rect = rect
	apply_presentation()

func apply_presentation() -> void:
	if ui == null: return
	if ui_theme != null: ui.theme = ui_theme
	if ui_rect.has_area():
		ui.position = ui_rect.position
		ui.size = ui_rect.size
		ui.custom_minimum_size = Vector2.ZERO
	if dialogue_body != null and ui_theme != null:
		var scale := maxf(1.0, ui_theme.default_font_size / 20.0)
		dialogue_body.vertical = ui_rect.size.x / scale < 960.0

var portrait_frame: PanelContainer
var portrait_rect: TextureRect

## Optional speaker portrait supplied by RpgUi; presentation only.
func set_portrait(texture: Texture2D) -> void:
	if ui == null: build_ui()
	portrait_rect.texture = texture
	portrait_frame.visible = texture != null

func build_ui() -> void:
	var layer := CanvasLayer.new()
	# Above the gameplay UI layer: dialogue owns presentation while it is active.
	layer.layer = 2
	add_child(layer)
	ui = PanelContainer.new()
	ui.name = "DialoguePanel"
	if ui_theme == null:
		var theme := Theme.new()
		theme.default_font_size = 22
		ui.theme = theme
	ui.position = Vector2(180, 300)
	ui.custom_minimum_size = Vector2(920, 240)
	layer.add_child(ui)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 10)
	ui.add_child(box)
	dialogue_body = BoxContainer.new()
	dialogue_body.name = "DialogueBody"
	dialogue_body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	dialogue_body.add_theme_constant_override("separation", 18)
	box.add_child(dialogue_body)
	# Story column: speaker portrait medallion, engraved name plate and the line.
	var story_column := HBoxContainer.new()
	story_column.name = "StoryColumn"
	story_column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	story_column.size_flags_vertical = Control.SIZE_EXPAND_FILL
	story_column.size_flags_stretch_ratio = 0.48
	story_column.add_theme_constant_override("separation", 14)
	dialogue_body.add_child(story_column)
	portrait_frame = PanelContainer.new()
	portrait_frame.name = "Portrait"
	portrait_frame.theme_type_variation = "IconWell"
	portrait_frame.custom_minimum_size = Vector2(84, 84)
	portrait_frame.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	portrait_frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	portrait_frame.visible = false
	story_column.add_child(portrait_frame)
	portrait_rect = TextureRect.new()
	portrait_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	portrait_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	portrait_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	portrait_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	portrait_frame.add_child(portrait_rect)
	var story_stack := VBoxContainer.new()
	story_stack.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	story_stack.size_flags_vertical = Control.SIZE_EXPAND_FILL
	story_stack.add_theme_constant_override("separation", 10)
	story_column.add_child(story_stack)
	var plate_row := HBoxContainer.new()
	story_stack.add_child(plate_row)
	var plate := PanelContainer.new()
	plate.name = "SpeakerPlate"
	plate.theme_type_variation = "Speaker"
	plate.mouse_filter = Control.MOUSE_FILTER_IGNORE
	plate_row.add_child(plate)
	speaker_label = Label.new()
	speaker_label.name = "Speaker"
	speaker_label.theme_type_variation = "DialogueSpeaker"
	plate.add_child(speaker_label)
	text_label = Label.new()
	text_label.name = "Line"
	text_label.theme_type_variation = "DialogueLine"
	text_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	text_label.custom_minimum_size = Vector2(0, 56)
	story_scroll = load("res://game/ui/TouchScroll.cs").new()
	story_scroll.name = "Story"
	story_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	story_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	story_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	story_stack.add_child(story_scroll)
	text_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	story_scroll.add_child(text_label)
	# Touch-owned scrolling: a swipe over responses never selects one.
	choice_scroll = load("res://game/ui/TouchScroll.cs").new()
	choice_scroll.name = "Responses"
	choice_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	choice_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	choice_scroll.scroll_deadzone = 12
	choice_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	choice_scroll.size_flags_stretch_ratio = 0.52
	dialogue_body.add_child(choice_scroll)
	choices = VBoxContainer.new()
	choices.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	choices.add_theme_constant_override("separation", 10)
	choice_scroll.add_child(choices)
	apply_presentation()

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
		# Input dispatch may still query the pressed button after its signal returns.
		# Hide now; let SceneTree release it after dispatch instead of detaching it here.
		if child is Control: child.hide()
		child.queue_free()
	if current_line == null:
		speaking = false
		ui.hide()
		return
	speaker_label.text = current_line.character
	text_label.text = current_line.text
	choice_scroll.scroll_vertical = 0
	story_scroll.scroll_vertical = 0
	if current_line.responses.is_empty():
		var button := Button.new()
		button.custom_minimum_size.y = 64
		button.name = "Continue"
		button.text = "Continue"
		button.theme_type_variation = "PrimaryButton"
		button.pressed.connect(func(): advance(current_line.next_id))
		choices.add_child(button)
	else:
		for response in current_line.responses:
			if not response.is_allowed: continue
			var button := Button.new()
			button.custom_minimum_size.y = 64
			button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
			button.theme_type_variation = "DialogueChoice"
			button.alignment = HORIZONTAL_ALIGNMENT_LEFT
			button.text = response.text
			button.disabled = not response.is_allowed
			button.pressed.connect(func(): advance(response.next_id))
			choices.add_child(button)
	# Keyboard/controller focus starts on the first response; Back cannot dismiss dialogue.
	for child in choices.get_children():
		if child is Button and child.visible and not child.disabled:
			child.call_deferred("grab_focus")
			break
	if OS.has_feature("android"):
		await get_tree().process_frame
		if epoch != dialogue_epoch: return
		for button in choices.get_children():
			if button is Button:
				var center: Vector2 = button.global_position + button.size / 2
				print("MOSSLIGHT_DIALOGUE_CHOICE text=%s x=%.1f y=%.1f" % [button.text, center.x, center.y])
