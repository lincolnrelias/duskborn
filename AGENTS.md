# Project instructions

- Unity MCP is unavailable on the user's free Unity plan (user-confirmed). Do not use Unity MCP, retry its connection, or ask the user to enable it or upgrade.
- Do not directly control the Unity Editor or enter Play Mode. Use source inspection, offline compilation, automated tests, logs, and the project's non-interactive CLI workflow.
- Read `.cursor/rules/unity-cli-workflow.mdc` before Unity-backed validation. Source work and offline validation may proceed while Unity is open; use an isolated project copy if Unity rejects a second process holding the same project.
- Clearly identify visual checks that remain unverified and provide concise manual verification steps when needed.
- Always remove generated terrain (run `.\Tools\unity.ps1 clear-terrain` and purge `NavMesh-TerrainManager*.asset` files) before committing changes to avoid bloated scene files and large Git payloads.
