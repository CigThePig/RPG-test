extends Quest
## Real event objectives extending the upstream Quest resource API.
@export var event_kind: String = "defeated"
@export var target_value: String = "beast"
@export var required: int = 2
@export var reward_item: String = "coin"
@export var reward_count: int = 15
@export var prerequisite: int = 0
@export var stages: Array = []
var stage_index: int = 0
var stage_id: String = ""
var progress: int = 0
var failed: bool = false
var rewarded: bool = false

func update(args: Dictionary = {}) -> void:
	if failed or objective_completed: return
	if not stages.is_empty():
		if stage_index >= stages.size(): return
		var stage: Dictionary = stages[stage_index]
		if args.get("kind", "") == stage.kind and args.get("value", "") == stage.value and args.get("target", "") == stage.get("target", ""):
			stage_index += 1
			progress = stage_index
			objective_completed = stage_index >= stages.size()
			stage_id = "complete" if objective_completed else str(stages[stage_index].id)
	elif args.get("kind", "") == event_kind and args.get("value", "") == target_value:
		progress = mini(required, progress + int(args.get("count", 1)))
		objective_completed = progress >= required

	super.update(args)
