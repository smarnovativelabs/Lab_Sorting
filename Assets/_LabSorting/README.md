# Lab Sorting prototype

Open Assets/_LabSorting/Prototype/Scenes/SortingTest.unity after generation.

## Structure
- Core: permanent player, interaction, inventory/rack, save and HUD scripts.
- Prefabs/Player/LabPlayer.prefab: permanent player, adapted Janitor KCC motor and look rig; no prototype art dependencies.
- ThirdParty/KCC: Janitor Kinematic Character Controller core. ThirdParty/Highlighting supplies required helpers/materials for the custom Janitor QuickOutline already imported.
- Prototype: disposable room, placeholder materials, five item definitions/prefabs, 50 items and five racks with 15 slots each; prototype smoke-check driver.
- Tests/Editor: Unity Test Framework bridge. Remove or update it when removing Prototype.
- Editor/PrototypeBuilder: creates a new prototype only, refuses to overwrite an existing scene.

## Controls
Desktop: WASD movement, drag right half of screen for look, E pick, F place, Q throw, Tab cycle. Mobile: left joystick, right drag area, buttons and inventory-category chips. Keyboard/mouse remain useful for Editor testing; actual Android hardware is required to validate touch feel/performance.

## Rules
Ten carried items; latest pickup selected, inventory type chips select an existing carried item. First placement assigns the entire rack. Wrong categories/full racks reject placement without consuming an item. Taking the last item resets rack type. Throw safely drops one active item onto a reachable surface. Smooth placement/pick/drop use DOTween; logical ownership commits independently from animation. Clutter colliders do not snag the KCC player but remain interaction targets.

## Persistence
sorting-test-v1.json in Application.persistentDataPath, with .bak recovery. Saves contain stable item/rack IDs, hand order/selection, capacity, player pose and world positions. Autosave after changes, on pause and quit. The prototype and final level need distinct save names and stable IDs. Tests use sorting-automated-test.json, never the normal save.

## Verification
Run unity test . --mode EditMode --filter SortingIntegrationTests --output Logs/SortingTests.xml. The EditMode bridge enters actual Play mode, exercises core transactions/controller and saves a screenshot/report. See Logs for current results; compilation alone is not mobile performance verification.

## Interaction polish and item progress
Pickup uses a 0.18-second local-space tween so the held model follows the moving hand, at 65% of its world scale. Placement restores world scale with a 0.24-second eased move/rotation; safe drop adds a small 0.28-second arc. Colliders stay disabled during travel and re-enable after landing. Save/restore completes or cancels motion explicitly.

SortingFeedback responds to successful pick/place/throw and rejected actions using four original synthesized mono WAV cues in the permanent Audio folder. Nice Vibrations supplies mobile haptic presets; Editor/desktop haptic calls are excluded. Feedback is throttled to avoid stacked sounds or vibrations during magnet collection. Sound and haptic toggles live in the progress panel and persist as LabSorting.Sound / LabSorting.Haptics PlayerPrefs.

Use ITEM PROGRESS or I to open the modal; X, I or Escape closes it. Every type shows sorted / total, remaining and held. Remaining includes loose and carried items; removing a placed item decreases sorted again. Six types fit per page, with Previous/Next for larger catalogs. Opening the panel clears movement/drag, blocks sorting and economy actions and releases the cursor; closing restores the prior block/cursor state. Scene authoring can be repeated with Tools > Lab Sorting > Add Polish and Progress Panel.

## Deliberately later
Final UI/art, monetization SDKs and Android-device profiling.

## Search, magnet and capacity
Select a carried category, then use Search (R) or Magnet (M). Both scan all loose items in the level without a distance or visibility limit; rack items are excluded. Search highlights matches for 8 seconds and shows a nearest-result locator. An active search retains its purchased category until expiry. Magnet collects matching loose items up to free capacity for one payment, keeping the selected category. Full hands, no available matches, missing selection and insufficient funds never charge. Repeated search while already active never charges.

Prototype balance on SortingSession: starting cash 200; Search 10; Magnet 25; +1 slot (U) starts at 50, increasing by 25 per upgrade, from 10 up to 15 slots. First placement of each unique item earns 5 once; picking/replacing cannot farm rewards. All values are Inspector-tunable. Save version 2 persists wallet, capacity and reward history; version 1 saves migrate with 200 cash and already-racked items marked rewarded. Ability timers are transient. UI buttons support touch and keyboard shortcuts remain available.

## Replacement
Replace prototype models via new item prefabs/definitions in permanent content folders. Keep definitions/IDs and slot transforms explicit. Preserve Core, permanent Player and ThirdParty. The test scene/fixtures and builder reference prototype assets and should be retired together if Prototype is deleted.

## Core baseline (2026-11-03)
Unity 6000.3.13f1 compilation and scene authoring succeeded. Unity Test Framework: 2 tests passed, 0 failed; 37 actual Play-mode checks. Covered 10-item capacity, duplicate pickup rejection, selection, rack assignment/type rejection/reset/reassignment, throw and recovery, state and disk save/load, malformed snapshot rejection, KCC movement, camera drag and blocked input. Separate dependency test confirms LabPlayer.prefab does not reference Prototype assets. Screenshot visually inspected at 1280x720 (Logs/SortingTest.png).

Test harness explicitly advances game time and simulates foreground input for the windowless batch Editor. Temporary no-domain-reload test settings are restored in teardown. Existing unused Feel MMRipple shader reports a legacy fallback warning; no prototype material uses it. Actual Android touch/device performance, wall/step stress testing and final art collision tuning remain unverified; do not describe this as full device QA.

Adaptation notes: removed Janitor-specific modal/tool/settings references; removed the copied KCC hard-coded capsule layer exclusions and unused nested Start function. Movement input and camera look stay independent from sorting. Touch drag is normalized to a 720px screen height. Selected item type persists after placement/drop while another copy remains.

## Economy verification (2026-11-04)
SortingTest scene now includes wired Search, Magnet and Capacity Upgrade buttons, wallet/slot readout, search timer and nearest-result marker. Unity Test Framework: 2 tests passed, 0 failed; 77 actual Play-mode checks. Added coverage for manual selection versus latest pickup, distant matching items beyond walls, type filtering, full and partial inventory, rack-item exclusion, absent selection/matches, insufficient cash, repeated active search, expiry, upgrade pricing/cap, wallet/capacity disk persistence, one-time placement rewards, legacy save migration, invalid wallet rejection and button listeners. The permanent player dependency check also passes. Logs/SortingTest.png was visually inspected at 1280x720 with search active. Physical Android touch/performance remains unverified.

## Polish verification (2026-11-04)
Clean isolated Unity batch run: 2 tests passed, 0 failed, exit code 0; 108 Play-mode checks. Report: Logs/PolishTests.xml (also copied to Logs/SortingTests.xml), log: Logs/PolishTests.log. Covers all prior sorting/economy checks plus moving-hand pickup, held/world scale restoration, feedback events and sound playback requests, mute, magnet cue throttling, in-flight restore/save, per-type totals after pickup/placement/rack removal/bulk collection, modal action blocking, prior block restoration, close/open listeners and multi-page category navigation. Images Logs/ItemsProgress.png and Logs/SortingTest.png were visually inspected at 1280x720. An earlier live Editor test run completed its gameplay checks but was interrupted by an external assembly reload; the clean batch result is the final verification. Actual Android haptic feel, touch ergonomics and device performance still require a physical phone.

## Player height and clearance verification (2026-11-04)
LabPlayer.prefab and SortingTest now use a 1.70m capsule, center Y 0.85m and unchanged 0.30m radius / 1.60m camera height. PrototypeBuilder defaults match. Tools > Lab Sorting > Apply 170cm Player Height updates the saved assets through Unity APIs.

Fresh batch Unity Test Framework run: 3 tests passed, 0 failed; 266 Play-mode checks. Results: Logs/HeightTests.xml, copied to Logs/SortingTests.xml. Runtime fixtures verify actual KCC traversal through 1.00m-wide doors at 2.00m and 1.75m height, blocking at 1.65m height and at 0.55m width, with grounded feet. All 75 rack slot centers and five labels remain visible; actual placement and retrieval work on every tier of all five racks. Floor pickup, inside/outside 3.5m reach, and wall-blocked pickup/placement pass. Serialized prefab and scene dimensions and permanent prefab independence are also tested. Fixtures are temporary and never saved into the authored scene. Final environment doorway geometry and physical Android touch/performance still need testing when available.

## Targeting and hold controls (2026-11-04)
PICK / E and PLACE / F act immediately on press. Release before the 0.42s initial delay for one item; continued holding repeats every 0.28s (Inspector-tunable). Pointer exit, release, disable, focus loss, pause, blocked controls, save restore, invalid targets, capacity/rack limits or category/rack changes end the hold. Reaiming does not restart it without a new press. Place repeats wait for an active item's animation on slow frames. THROW / Q remains one action per click/key press, including when Q stays held. SortingHoldButton owns its pointer ID and the saved PICK/PLACE onClick lists are empty to prevent duplicate release actions.

SortingInteraction uses a 0.09m nonalloc sphere sweep for slight item misses, with a 1.5x retention margin to reduce highlight jitter. A direct item hit wins. Candidate checks exclude carried/moving items and enforce reach and first-hit line of sight, so assistance does not bypass walls or shelves. Rack placement remains an actual raycast target. Permanent scripts stay in Core; disposable interaction fixtures stay in Prototype. InteractionUpgrade applies native Unity scene wiring and PrototypeBuilder uses the same configuration for future scenes.

Final fresh batch result: 3 tests passed, 0 failed; 297 Play-mode checks. Logs/InteractionTests.xml is also copied to Logs/SortingTests.xml. Coverage includes acquisition of small off-center items, retention/release, direct-hit priority, wall blocking, pointer tap/hold/release/exit and unrelated touch IDs, blocked/focus/pause/restore cancellation, capacity and wrong/full rack handling, target changes, and synthetic Input System E/F/Q press/hold/release events. Prior economy, persistence, progress, animation, height, doorway and shelf tests also pass. Frame-clock waits prevent unfocused batch timing drift. Keyboard tests temporarily clone input settings to route synthetic events to the unfocused Game View, then restore the original settings and remove the synthetic device. Final SortingTest screenshot inspected. Physical Android touch feel/performance remains to be tested.

## Milestones, selected category and ability feedback (2026-11-04)
Implemented, scene-wired and verified after disk-space recovery. Final Unity Test Framework run: 3 tests passed, 0 failed; 426 Play-mode checks. Results: Logs/RewardsTests.xml (also copied to Logs/SortingTests.xml), log: Logs/RewardsTests.log. Native scene setup: Logs/RewardsSetup.log. Earlier runs were interrupted by disk exhaustion and the original three-minute suite timeout; the final clean result supersedes them. The full-suite timeout is now 15 minutes and ordinary PASS-log stack traces are temporarily disabled during tests, with prior logging settings restored in teardown. Assertions and error stack traces remain intact.

Four one-time, Inspector-tunable milestones: first 10 unique placements ($25), first fully sorted category ($40), 50% currently sorted ($60), and 100% currently sorted ($100). Claimed IDs persist in save version 3. Version 1/2 loads mark already-achieved milestones without granting retroactive payments; duplicate/unknown IDs are rejected before mutation. Removing/replacing items cannot repay claimed milestones. A next-goal label and queued nonmodal reward toasts sit in SafeArea.

The selected-item card shows category, category count, total carried count/capacity and category color; selected inventory chips have a > marker. An active search button retains the purchased category name even after selection changes. Search feedback pulses its category/match count. Successful magnet collection emits captured source positions and a +N toast; pooled, noninteractive colored chips fly toward the selected-item card with staggered starts. Logical collection/payment remains immediate and capacity-limited, independent of visual playback, and restore clears transient effects. Existing unlimited-range and no-charge failure rules remain.

New tests: Prototype/RewardsChecks.cs covers threshold payouts, all milestones, repeat-placement farming, snapshot/disk persistence, version-2 migration, invalid IDs, selected HUD, search category continuity, magnet effect count/origins/capacity/payment, pooling and mid-flight restore. It is included in SortingIntegrationTests via SortingSmokeChecks. Logs/MilestonesHUD.png and Logs/RewardsHUD.png were generated and visually inspected at 1280x720. Physical Android touch feel, haptic feel and performance remain unverified. Core scripts are permanent; prototype tests remain disposable.

