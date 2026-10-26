extends Node
## Authoritative Gloot inventories and equipment. C# delegates through this boundary.
var bags: Dictionary = {}
var slots: Dictionary = {}
var serial: int = 0
var protos: JSON = preload("res://game/data/items.json")

func bag(owner: String) -> Inventory:
	if not bags.has(owner):
		var inventory := Inventory.new()
		inventory.name = "Bag_" + owner.replace(":", "_")
		inventory.protoset = protos
		add_child(inventory)
		bags[owner] = inventory
	return bags[owner]

func add(owner: String, prototype: String, count: int = 1) -> bool:
	if count <= 0 or not protos.data.has(prototype): return false
	var inventory := bag(owner)
	for n in count:
		var item := inventory.create_item(prototype)
		serial += 1
		item.set_property("instance_id", "item:%d" % serial)
		# Unique identifiers deliberately prevent merging equipment; commodity stacks share no identity.
		if item.get_max_stack_size() > 1: item.clear_property("instance_id")
		if not inventory.add_item_autosplitmerge(item): return false
	return true

func count_item(owner: String, prototype: String) -> int:
	var total := 0
	for item in bag(owner).get_items_with_prototype_id(prototype): total += item.get_stack_size()
	return total

func take(owner: String, prototype: String, count: int) -> bool:
	if count < 1 or count_item(owner, prototype) < count: return false
	for item in bag(owner).get_items_with_prototype_id(prototype):
		var amount := mini(count, item.get_stack_size())
		count -= amount
		if amount == item.get_stack_size(): bag(owner).remove_item(item)
		else: item.set_stack_size(item.get_stack_size() - amount)
		if count == 0: break
	return true

func transfer(source: String, target: String, prototype: String, count: int) -> bool:
	if source == target or count < 1 or count_item(source, prototype) < count: return false
	# Move original instances, retaining condition, material, ownership and modifiers.
	for item in bag(source).get_items_with_prototype_id(prototype):
		var amount := mini(count, item.get_stack_size())
		var moving: InventoryItem = item if amount == item.get_stack_size() else bag(source).split_stack(item, amount)
		bag(source).remove_item(moving)
		bag(target).add_item_autosplitmerge(moving)
		count -= amount
		if count == 0: break
	return true

func equip(owner: String, prototype: String, slot_name: String, grasping: bool) -> bool:
	var item := find_item(owner, prototype)
	if item == null or item.get_property("slot", "") != slot_name: return false
	if slot_name == "weapon" and not grasping: return false
	var key := owner + ":" + slot_name
	if not slots.has(key):
		var slot := ItemSlot.new()
		slot.name = "Slot_" + key.replace(":", "_")
		slot.protoset = protos
		add_child(slot)
		slots[key] = slot
	var previous: InventoryItem = slots[key].get_item()
	if previous != null:
		slots[key]._inventory.remove_item(previous)
		bag(owner).add_item(previous)
	return slots[key].equip(item)

func bonus(owner: String, property: String) -> float:
	var total := 0.0
	for key in slots:
		if key.begins_with(owner + ":"):
			var item: InventoryItem = slots[key].get_item()
			if item != null: total += float(item.get_property(property, 0.0))
	return total

func describe(owner: String) -> String:
	var lines := PackedStringArray()
	for item in bag(owner).get_items(): lines.append("%s ×%d" % [item.get_title(), item.get_stack_size()])
	for key in slots:
		if key.begins_with(owner + ":"):
			var item: InventoryItem = slots[key].get_item()
			if item != null: lines.append("[%s] %s" % [key.get_slice(":", 1), item.get_title()])
	return "\n".join(lines)

func export_state() -> String:
	var data := {"serial": serial, "bags": {}, "slots": {}}
	for key in bags: data.bags[key] = bags[key].serialize()
	for key in slots: data.slots[key] = slots[key].serialize()
	return JSON.stringify(data)

func import_state(text: String) -> bool:
	var data = JSON.parse_string(text)
	if not data is Dictionary or not data.has_all(["serial", "bags", "slots"]): return false
	if not validate_snapshot(data): return false
	# Stage all deserializations before replacing any authoritative state.
	var staged := ItemServiceFactory()
	for key in data.bags:
		if not staged.bag(key).deserialize(data.bags[key]):
			staged.free()
			return false
	for key in data.slots:
		var slot := ItemSlot.new()
		slot.name = "Slot_" + key.replace(":", "_")
		slot.protoset = protos
		staged.add_child(slot)
		if not slot.deserialize(data.slots[key]):
			staged.free()
			return false
		staged.slots[key] = slot
	for child in get_children(): child.free()
	bags = staged.bags
	slots = staged.slots
	for child in staged.get_children():
		staged.remove_child(child)
		add_child(child)
	staged.free()
	serial = int(data.serial)
	return true

func ItemServiceFactory() -> Node:
	return get_script().new()

func validate_snapshot(data: Dictionary) -> bool:
	if not data.bags is Dictionary or not data.slots is Dictionary or data.serial < 0: return false
	var identities := {}
	var items: Array = []
	for key in data.bags:
		var entry = data.bags[key]
		if not entry is Dictionary or entry.get("protoset", "") != "res://game/data/items.json": return false
		items.append_array(entry.get("items", []))
	for key in data.slots:
		var entry = data.slots[key]
		if not entry is Dictionary: return false
		if entry.has("item"): items.append(entry.item)
	for item in items:
		if not item is Dictionary or item.get("protoset", "") != "res://game/data/items.json" or not protos.data.has(item.get("prototype_id", "")): return false
		var props: Dictionary = item.get("properties", {})
		for name in props:
			if not props[name] is Dictionary or not props[name].has_all(["type", "value"]): return false
		var amount := 1
		if props.has("stack_size"): amount = int(str_to_var(props.stack_size.value))
		if amount < 1 or amount > int(protos.data[item.prototype_id].get("max_stack_size", 1)): return false
		if props.has("instance_id"):
			var id = str_to_var(props.instance_id.value)
			if identities.has(id): return false
			identities[id] = true
	return true

func generate_item(owner: String, prototype: String, material: String, quality: int) -> bool:
	var materials: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://game/data/materials.json"))
	if not materials.has(material) or quality < 1 or quality > 3 or not protos.data.has(prototype): return false
	if int(protos.data[prototype].get("max_stack_size", 1)) != 1: return false
	var item := bag(owner).create_item(prototype)
	serial += 1
	item.set_property("instance_id", "item:%d" % serial)
	item.set_property("material", material)
	item.set_property("quality", quality)
	item.set_property("condition", 100)
	item.set_property("mass", materials[material].mass)
	for stat in ["damage", "defence", "power"]:
		if item.has_property(stat): item.set_property(stat, float(item.get_property(stat)) * float(materials[material].get(stat, 1.0)) * (0.85 + quality * 0.15))
	return bag(owner).add_item(item)

func equipment_appearance(owner: String) -> String:
	var result := {"weapon": "none", "outfit": "none"}
	for key in slots:
		if not key.begins_with(owner + ":"): continue
		var item: InventoryItem = slots[key].get_item()
		if item == null: continue
		if key.ends_with(":weapon"): result.weapon = "staff" if item.get_prototype().get_prototype_id() == "oak_staff" else "dagger"
		if key.ends_with(":body"): result.outfit = "tunic"
	return JSON.stringify(result)

func _exit_tree() -> void:
	# Gloot's cached prototype trees contain parent/child reference cycles. Release them at shutdown.
	var cache = preload("res://addons/gloot/core/prototree/proto_tree_cache.gd")
	for tree in cache._cache.values(): tree.clear()
	cache._cache.clear()

func can_produce(prototype: String, count: int, material: String, quality: int) -> bool:
	if count < 1 or not protos.data.has(prototype): return false
	if material == "": return true
	var materials: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://game/data/materials.json"))
	return materials.has(material) and quality >= 1 and quality <= 3 and int(protos.data[prototype].get("max_stack_size", 1)) == 1

func find_item(owner: String, selector: String) -> InventoryItem:
	for item in bag(owner).get_items():
		if item.get_property("instance_id", "") == selector: return item
	return bag(owner).get_item_with_prototype_id(selector)

func catalog(owner: String) -> String:
	var rows: Array = []
	for item in bag(owner).get_items():
		var id: String = item.get_prototype().get_prototype_id()
		rows.append({"selector": item.get_property("instance_id", id), "id": id, "title": item.get_title(), "count": item.get_stack_size(), "slot": item.get_property("slot", ""), "healing": float(item.get_property("healing", 0)), "properties": item_details(item)})
	return JSON.stringify(rows)

func consume(owner: String, selector: String) -> float:
	var item := find_item(owner, selector)
	if item == null: return 0.0
	var healing := float(item.get_property("healing", 0.0))
	if healing <= 0.0: return 0.0
	item.set_stack_size(item.get_stack_size() - 1)
	return healing

func item_details(item: InventoryItem) -> String:
	var lines := PackedStringArray()
	for property in ["material", "quality", "condition", "damage", "defence", "power", "mass", "healing"]:
		if item.has_property(property): lines.append(property.capitalize() + ": " + str(item.get_property(property)))
	return "\n".join(lines)

func reconcile_equipment(owner: String, grasping: bool) -> void:
	var key := owner + ":weapon"
	if grasping or not slots.has(key): return
	var item: InventoryItem = slots[key].get_item()
	if item != null:
		slots[key]._inventory.remove_item(item)
		bag(owner).add_item(item)

func unequip_all(owner: String) -> void:
	for key in slots:
		if not key.begins_with(owner + ":"): continue
		var item: InventoryItem = slots[key].get_item()
		if item != null:
			slots[key]._inventory.remove_item(item)
			bag(owner).add_item(item)

func has_items(owner: String) -> bool:
	return bags.has(owner) and bags[owner].get_item_count() > 0
