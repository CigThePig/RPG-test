extends Quest
## Real event objectives extending the upstream Quest resource API.
@export var event_kind: String = "defeated"
@export var target_value: String = "beast"
@export var required: int = 2
@export var reward_item: String = "coin"
@export var reward_count: int = 15
@export var prerequisite: int = 0
var progress: int = 0
var failed: bool = false
var rewarded: bool = false

func update(args: Dictionary = {}) -> void:
	if failed or objective_completed: return
	if args.get("kind", "") == event_kind and args.get("value", "") == target_value:
		progress = mini(required, progress + int(args.get("count", 1)))
		objective_completed = progress >= required
	super.update(args)
