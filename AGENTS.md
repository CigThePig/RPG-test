# Notes for AI agents

## Performance is not a concern right now

The owner has confirmed Mosslight runs at a stable frame rate on the physical Galaxy S25 (PR #8 APK), and has accepted PR #8's appearance and touch controls on that phone. Unless the owner asks for it or reports a performance problem:

- Don't profile, benchmark or optimize, and don't add performance budgets.
- Don't write performance or memory sections (frame-time percentiles, byte counts, draw calls, allocation totals) in handoffs, docs or PR descriptions.
- Don't list "sustained performance" as pending S25 acceptance.

Write ordinary, sensible code; just don't spend effort measuring or documenting performance. Older docs under `docs/` contain performance reports and budgets. They are historical records, not instructions.
