extends Node
## Adapted from Slashscreen's Skelerealms GOAPComponent. MIT; see upstream manifest.
## Pure planning module; execution remains in World and uses the same actions as the player.
class PlannerAction extends RefCounted:
 var id: String
 var cost: float = 1.0
 var prerequisites: Dictionary = {}
 var effects: Dictionary = {}
 func _init(key: String, before: Dictionary, after: Dictionary, price: float = 1.0):
  id = key
  prerequisites = before
  effects = after
  cost = price
 func is_achievable() -> bool: return true
 func is_achievable_given(state: Dictionary) -> bool:
  return prerequisites.keys().all(func(key): return state.has(key) and state[key] == prerequisites[key])
 func get_effects() -> Dictionary: return effects

func choose(goal: String, sees_target: bool) -> String:
 var actions: Array = [
  PlannerAction.new("attack", {"target": true}, {"defeated": true}),
  PlannerAction.new("retreat", {"target": true}, {"safe": true}),
  PlannerAction.new("rest", {}, {"rest": true}),
  PlannerAction.new("trade", {}, {"trade": true}),
  PlannerAction.new("patrol", {}, {"patrol": true})]
 var plan := _plan(actions, {goal: true}, {"target": sees_target})
 var result: String = plan.back().id if not plan.is_empty() else "wait"
 return result

func _plan(actions:Array, goal:Dictionary, world_states:Dictionary) -> Array[PlannerAction]:
    var action_pool:Array = actions.filter(func(a:PlannerAction): return a.is_achievable()) # get all of the actions currently achievable.

    var leaves:Array[PlannerNode] = [] # create an array keeping track of all of the possible nodes that could make up our path.
    var start = PlannerNode.new(null, world_states, null, 0) # build the starting node.
    var success = _build_graph(start, leaves, goal, action_pool) # try to find a path.

    if not success: # if we have not found a path, we have failed.
        return []

    leaves.sort_custom(func(a:PlannerNode,b:PlannerNode): return a.cost < b.cost ) #Sort to find valid node with least cost.
    var cheapest:PlannerNode = leaves[0]

    var new_plan:Array[PlannerAction] = [] # create the plan for the AI to use. This will be treated like a queue.
    # walk back up the parent chain that the selected node has kept (sorta like a linked list) and build a queue from that.
    var n = cheapest
    while not n.parent == null: # if it is null, we have reached the root node, since it will have no parents.
        new_plan.push_back(n.action)
        n = n.parent

    #new_plan.reverse()
    return new_plan


## Recursive method to try to find all possible action chains that could satisfy the goal.
func _build_graph(parent:PlannerNode, leaves:Array[PlannerNode], goal:Dictionary, action_pool:Array) -> bool:
    var found_path:bool = false
    # FIXME: We need to be doing breadth first search

    # get all actions that are
    # 1) achievable
    # 2) achievable given prerequisites
    # 3) has not already had effects satisfied <- may cause issues
    var achievable_actions = action_pool.filter(func(x:PlannerAction): return x.is_achievable() and x.is_achievable_given(parent.states) and not _goal_achieved(x.get_effects(), parent.states))
    achievable_actions.sort_custom(func(a,b): return a.cost < b.cost ) #Sort to resolve actions with least cost first.

    # due to the recursive nature of this function, we will be building branching paths from all of the actions until a valid path is found.
    for action:PlannerAction in achievable_actions:
        # if we can achieve this action,
        # duplicate our working set of states.
        var current_state:Dictionary = parent.states.duplicate(true)

        # Continue to accumulate effects in state, for passing on to the next node.
        current_state.merge(action.get_effects(), true) # overwrite to keep the state up to date.

        # create a new child planner node, which will have an accumulation of all the previous costs.
        # this will help us find the shortest path later.
        var next_node:PlannerNode = PlannerNode.new(parent, current_state, action, parent.cost + action.cost)

        if _goal_achieved(goal, current_state):
            # if we have reached the state we are looking for, append the node to the leaves, and set found_path.
            leaves.append(next_node)
            found_path = true
        else:
            # if we have not reached the goal,
            # create a subset of the action pool that removes the current action.
            # this will prevent circular action chains.
            var subset:Array = action_pool.duplicate() # no deep copy, we don't want to clone the nodes.
            subset.erase(action)

            # then, recurse and find the next possible node.
            if _build_graph(next_node, leaves, goal, subset):
                found_path = true

    return found_path


## Determine whether we have satisfied all goals in our state.
func _goal_achieved(goal:Dictionary, current_state:Dictionary) -> bool:
    return goal.keys().all(func(key): return current_state.has(key) and current_state[key] == goal[key])


class PlannerNode:
    var parent:PlannerNode
    var action:PlannerAction
    var cost:float
    var states:Dictionary


    func _init(p:PlannerNode, s:Dictionary, a:PlannerAction, c:float) -> void:
        parent = p
        action = a
        cost = c
        states = s
