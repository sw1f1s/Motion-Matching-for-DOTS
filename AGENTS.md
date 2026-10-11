# Motion Matching for DOTS — agent instructions

## Project and package

- This repository develops the Unity package `com.collint.mmd` at `Assets/Packages/com.collint.mmd`, with namespaces rooted at `ColInt.MMD`. Unity version is recorded in `ProjectSettings/ProjectVersion.txt`.
- Runtime contains generic motion matching, database, playback, root motion, Physics and Graphics. Core Editor contains baking and diagnostics. Gameplay movement, states, player input, camera, scene, assets and all tests belong to `Assets/Packages/com.collint.mmd/Samples`.
- Samples is bundled and available directly with the package. Edit this single copy; do not create imported copies or duplicate assemblies elsewhere in Assets. Runtime and core Editor do not depend on Sample.
- All package assemblies must compile in a clean consumer project using declared package dependencies. Do not depend on consumer application code, services or editor tools.

- In this development project, Unity compiles the source directly from Assets. Keep its dependencies explicit in the project manifest via Package Manager. Do not also register the same Assets folder as a local UPM package: that duplicates scripts and asset GUIDs. The package.json remains the manifest for distribution through UPM.
- Installed UPM packages use virtual Packages paths; Editor resource lookup must work from the actual script location rather than a fixed development path.

## Required rules

- Before changing C# code, read `Rules/CSharpCodeStyleRules.md` and follow `.editorconfig`.
- Before changing ECS, animation, physics, input, movement or world lifecycle, read `Rules/SimulationFeatureDevelopmentRules.md`.
- Before changing editor tools or diagnostics, read `Rules/EditorDevelopmentRules.md`.
- Read `Rules/ClientServerCommunicationRules.md` only when adding explicitly requested network integration.

## Coding and ECS

- Inspector fields are private with `[SerializeField]`; expose runtime values through read-only properties or getters with private setters. Serializable settings using auto-properties use `[field: SerializeField]` and public setters; preserve this distinction from runtime encapsulation.
- Constants and static readonly fields precede other fields. Keep auto-properties on one line. Empty constructor bodies remain on the declaration line. Use braces for all control-flow bodies, place contents on following lines, and keep each statement on its own line.
- Keep helpers and data types in their own files. Declare IJobEntity jobs as nested partial structs in the system that uses them; job-specific helper methods stay inside the job.
- Components and buffers are unmanaged structs. Unity object references use UnityObjectRef and are dereferenced only on the main thread. Presence/activation/initialization states use marker components rather than duplicate booleans.
- Use SystemAPI.Query directly in foreach for current-entity components. Create explicit EntityQuery fields in OnCreate when required; RequireForUpdate must match actual work. Structural changes use ECB after enumeration or an explicitly disposed snapshot. Keep at most one independent outer foreach per system.
- Systems never look up or update other systems/groups. Groups inherit directly from ComponentSystemGroup and use standard Unity scheduling. Shared world state is ECS data; pause is published through MmdWorldState.
- Animation, physics and rendering run in one world. Root motion changes ECS simulation; Graphics consumes the resulting pose. Animation and render ownership/state remain on the main character entity where possible.
- Each owned resource has explicit lifecycle cleanup, including owner removal, Disabled and world disposal. Release resources before removing/overwriting owning cleanup components. Shared animation data is not owned by Graphics.
- Runtime asynchronous work uses UniTask with owner cancellation and an explicit dependency if introduced. Editor baking uses Task. Ordinary asynchronous NUnit tests use Task; frame-dependent PlayMode waits use Unity 6 Awaitable. Do not introduce coroutine adapters for ordinary async work.
- Preserve Unity GUIDs and serialized values. Update scenes, prefabs, configs, asset paths and fixtures together with renamed fields or files. Do not add legacy formats or compatibility layers without an explicit request.

## Validation and agent workflow

- Run affected EditMode/PlayMode tests in `Samples/Tests` and check the Editor console. Verify clean package installation after dependency or assembly changes. Debugger and Bake inspector are checked manually; do not add automated UI tests.
- When several Editors are open, explicitly select this project's Unity instance before using MCP or Unity Pipeline. These tools are development tooling, never runtime package dependencies.
- Use existing project rules and APIs; do not introduce a framework or dependency only to satisfy an assumed architecture. Keep unrelated changes out of the task.
- Git LFS handles binary Sample content. Never commit Library, Temp, Logs, local credentials or generated build artifacts. MIT covers MMD code; preserve third-party notices and asset license terms.
- Do not publish or push unless the user asks. Do not delegate to other agents unless explicitly requested by the user or applicable instructions.
