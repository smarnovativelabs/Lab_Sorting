# Material landing sound and weight tuning — 2026-11-12

Landing feedback now routes Glass/Plastic/Metal through the existing wired material clips; Default and missing material clips use the drop thud. These are reused project audio assets, not newly recorded sounds. Actual first significant Rigidbody contact triggers the sound immediately. Settling uses a fallback cue only if no contact cue played, preventing duplicate sounds. Three reusable landing AudioSources isolate landing pitch from pickup/placement audio and cap simultaneous voices. Sound mute and component disable stop all landing voices. Small/heavy cues use selection/light-impact haptics on mobile; physical haptic feel still needs device review.

ThrowFeel derives a bounded gameplay weight from item volume and material: mass 0.12–2.4kg, pitch 1.12–0.82, lift 0.30–0.22m, yaw speed 0.65–0.2rad/s and tuned damping. Gravity stays the same for every item. Impact speed scales loudness with a bounded gain; large/heavy items sound deeper and spin less. Existing three-body pool, hybrid fallback, save destinations and recovery remain. Rest validation now rejects positive item-to-item penetration while retaining a small support-surface tolerance.

Validation reports: Logs/LandingFeelTests.xml contains passing DesignerMaterialFeedback; Logs/LandingFeelFinalTests.xml contains passing DesignerLandingFeel and DesignerFullFlight. Material tests cover all four categories, first-contact timing, once-only cues, pitch/clip routing, mute, missing-clip fallback and body release. Full-flight checks run with 4032 scene items and cover dense-area release, wall collision, pool cap/fallback and save/pause/restore. Muted-throw verification now waits for actual settle completion with a bounded timeout. Repeated-drop diagnostics exposed PhysX returning contact=true with depth=0; the assertion distinguishes touching from positive penetration. Final focused result is recorded in Logs/LandingSoftFinalTests.xml.

No player build was made. Editor tests verify routing/physics behavior; listening balance, phone-speaker character and physical haptic comfort require device review. Next: designer UI integration and optional designer-supplied landing recordings, then Android profiling when builds are authorized.

# Full-flight Rigidbody throws — 2026-11-12

Full-flight gravity/collision is now the primary throw path. Three reusable Rigidbody/BoxCollider proxies serve the entire 4032-item scene. A fourth simultaneous throw keeps the controlled hybrid fallback; it does not interrupt the three active flights. Settling disables the proxy collider and makes its body kinematic. Scene items do not each receive a Rigidbody.

Flight uses a gentle upward/forward velocity, low bounce, ContinuousDynamic collision detection and slight yaw rotation. X/Z rotation is constrained to avoid excessive tumbling. A quiet, supported, non-penetrating rest keeps the actual final pose; a 1.8-second timeout or invalid fall recovers to the reserved planned destination. Pause completes active flights safely, restore cancels them, mid-flight saves use the safe destination, and settled saves use the actual pose. Reserved destinations prevent simultaneous throws selecting the same fallback space. Collision responses can change the final path and may rest on other objects; strict front-only landing under every possible collision is not guaranteed. Existing silent pending-drop behavior for completely blocked front space is retained.

Validation: the initial run passed CoreSortingAndController, DesignerFrontOnlyDrop and DesignerSoftDropAndRackTargeting; its new wall test initially checked before contact and was corrected from 0.3 to 0.6 seconds. Logs/FullFlightRetest.xml passes the focused full-flight test. Final Logs/FullFlightFinalTests.xml passes all three cases: DesignerFullFlight, DesignerDevicePerformance and DesignerCompactControls. A prior combined run was interrupted by a pending Unity assembly reload and was rerun after compilation completed. Tests cover the three-body cap/fourth fallback, real wall impact, actual settling, save poses, pause/restore, a real dense-scene drop with penetration tolerance <=6mm, all 46 type bounds, ability indexing and four mobile ratios. No build was produced.

Short Editor Physics.Simulate sample: idle mean 0.2414ms / max 0.4420ms (22 samples); flight window mean 0.3160ms / max 1.3688ms (119 samples). Report: Logs/FullFlightPhysics.txt. Window includes the period after settling and is not a sustained/device FPS benchmark. Latest ability microbenchmark: nearest search 0.0768ms, drop planning 0.1499ms, magnet action 0.6872ms, search action 0.1925ms; measured managed bytes per call zero in these samples (Logs/FullFlightAbilities.txt). Action samples and Editor timings are noisy; they are not a zero-allocation guarantee for all gameplay.

Saved designer scene reopened outside Play Mode: 4032 items, persistence=true, autoTutorial=true, held socket X=0.48. Next: integrate designer UI, tune material-specific impact audio/weight, and profile on the minimum Android device when builds are authorized. Earlier sections below describe historical behavior superseded by this section.

# 4032-item abilities, mobile layouts and temporary physics — 2026-11-11

Search activation/end, nearest-result lookup, search markers and magnet now use a type index (46 buckets) instead of repeatedly scanning all 4032 items. The index rebuilds on catalog replacement/restore. Magnet publishes one batch commit/feedback instead of one per collected item. Pickup animations and actual held models remain intact.

Drop bounds now come from BoxCollider local geometry plus transformed renderer-local bounds, without temporarily enabling/scaling/rotating the item or calling Physics.SyncTransforms twice per query. All 46 type bounds were compared against the previous physics measurement. Existing front-cone, supported floor, wall visibility, occupied destination and swept corridor checks remain.

Editor microbenchmark (same 4032-item scene): baseline nearest search 0.1236ms -> 0.0280ms; drop planning 0.1464ms -> 0.0347ms. The nearest/drop measurements average 1000/30 calls. Search action 0.4254ms -> 0.1880ms and magnet action 7.7164ms -> 4.1831ms are single-action samples and are noisier (earlier optimized magnet samples ranged 0.44–4.25ms). Reports: Logs/AbilitiesBaseline.txt and Logs/AbilitiesOptimized.txt. These are Editor measurements, not low-end phone FPS or a zero-GC guarantee.

Held socket moves from local X 0.42 to 0.48 (camera/screen right) in the designer scene and permanent LabPlayer prefab. CarryScale increases 12%; original floor/rack scale is unchanged. Clearance radii are adjusted for the larger held envelope. Designer persistence and first-time tutorial configuration are enabled; tests disable them only on unsaved test instances.

A pool of three BoxCollider/Rigidbody proxies drives a short gravity/collision landing after the controlled throw arc. Bodies are constrained to vertical motion to retain front-only placement; low restitution prevents aggressive bounce. No Rigidbody is added to each of the 4032 items. The oldest landing completes if the pool is full. Simulation ends within 0.22 game seconds at the safe committed destination; active landings are excluded from rendering batches and save their validated destination. Restore cancels proxies, pause completes them, and rapid pickup during the short settle cannot duplicate an item. Pooled body activation teleports both Transform and Rigidbody after changing mode, avoiding stale origin/interpolation poses. Actual floor collision callbacks are verified.

Mobile layout checks render 1280x720, 1440x720, 1560x720 and 1600x720 (16:9, 18:9, 19.5:9, 20:9), with 48px side insets and 24px gesture inset. Action bounds, minimum 42px target size, right-thumb action region and lower-left joystick region pass. Reports/screenshots: Logs/MobileRatioChecks.txt and Logs/MobileRatio*.png. This is simulated layout/reach geometry, not physical thumb-comfort or device performance certification. No build was produced.

Final verification: Logs/DeviceOptimizationFinalTests.xml passes DesignerDevicePerformance, DesignerFrontOnlyDrop and DesignerSoftDropAndRackTargeting (3/3), including actual floor collision callbacks and all four mobile ratios/thumb regions. Logs/DeviceCoreFinalTests.xml contains the passing DesignerCompactControls case and preserves the old 0.5-second smoke timeout failure. Logs/DeviceCoreRetest.xml passes CoreSortingAndController after allowing 0.95 seconds for the controlled arc plus <=0.22-second physics settle. Five relevant cases are passing across the final runs. Earlier contact failure exposed the pooled kinematic-to-dynamic pose reset and was fixed; it was not waived. Updated screenshots were inspected. Physical Android FPS, thermal throttling, GPU load and physical reach remain device validation tasks.

# Front-only drop — 2026-11-11

Replaces the previous 360-degree landing search. Drop candidates stay in a +/-20 degree horizontal front cone, trying 0.45–4.61m distances in close-first order. A final pivot-angle check prevents offset models landing behind the player. Renderer/collider bounds, floor support, occupancy, arc clearance and camera-to-landing wall visibility are checked. The latter prevents a visually clipped hand from releasing through a wall.

Held-item blocked/cooldown/moving cases no longer produce a rejection warning. A blocked request remains pending (DROP ...) and retries after meaningful view movement or a gameplay revision, at most five probes per second. It does not repeatedly scan when the player is stationary. Changing selection, picking, placing, opening a modal or restoring a save cancels the request. Pending intent is not persisted. A fully blocked front cannot honestly guarantee instant release while also preventing overlaps; the item stays safe until a front landing becomes possible.

Flight corridor now checks loose items too; they are ignored only in the camera visibility ray because the arc may legitimately pass above them. Focus loss and application pause cancel pending intent as well.

Validation: Logs/FrontOnlyDropFinalTests.xml passes all four relevant regression cases: CoreSortingAndController, DesignerCompactControls, DesignerFrontOnlyDrop and DesignerSoftDropAndRackTargeting. Logs/FrontDropLifecycleTests.xml passes the final focused front-drop case including background cancellation. Tests cover front-cone direction at multiple headings, completely blocked front wall (including a hand beyond the wall), silent pending state, automatic release after safe re-aim, no cash/inventory loss, selection/modal/background cancellation, floor support and repeated non-overlapping drops. No build or physical-device performance run was made.

# HUD and carry presentation polish — 2026-11-11

Hover name and matching backplate moved from center Y -46 to -112; the panel is narrower (340x30) and text is 17 px. Existing joystick input is retained, with lightweight round vector base/thumb graphics replacing rectangular visuals. Compact button captions use 14 px text and a subtle shadow. These are replaceable presentation components; designer UI can reuse the existing button bindings and HUD references.

Search now has a stable top direction/name/distance strip and separate world-item beacons. Nearby redundant beacon labels are suppressed rather than shifted onto the wrong object. Projection supports both overlay and screen-space-camera Canvas modes.

Designer hand socket has CarryPresentation: it retracts toward the camera near environment geometry and eases back when clear. Item presentation scale is capped using renderer radius and pivot offset, while original floor/rack scale and colliders remain intact. Magnet arrivals start in a short clearance-checked approach, keep real models visible, and constrain their flight against environment geometry. The old UI chip animation remains disabled.

Drop validation now encompasses visible renderer bounds as well as colliders. Dropped objects finish their rotation while compact, then expand to normal size along the existing validated arc. Floor support and non-overlapping landing checks remain intact.

Validation: Logs/PresentationPolishTests.xml passes CoreSortingAndController, DesignerBulkAndIcons and DesignerSoftDropAndRackTargeting, and preserves the original wall-measurement fixture failure. Logs/PresentationPolishFinalTests.xml is the final passing 2/2 run for DesignerCompactControls and DesignerSoftDropAndRackTargeting. The wall fixture now isolates the motor while measuring real wall-plane clearance; tight-wall presentation reduces carry size when needed. Checks include all 4032 items' carry envelopes, normal/tight wall and near-plane clearance, magnet entry clearance, hover/backplate offset, round joystick wiring, non-overlapping search labels, magnet model delivery, safe drop in eight facing directions, receipt deduplication and save reload. Updated hover/search/magnet screenshots were visually inspected. No build, physical Android run or low-end FPS certification was performed.

# Compact controls, targeting and supplies — 2026-11-11

Designer scene now has eight compact vector icon controls, direct visible-item taps with small touch assistance, cyan animated hover corners, and up to six gold search distance markers plus nearest-item direction. Search lasts 12 seconds. Hover does not resize colliders or allocate individual materials. Search remains unlimited range; only visible matching models receive the extra on-screen markers.

DROP searches 288 floor candidates across 360 degrees, retrying from a retracted release point when the visual hand is trapped beside a shelf and animates a modest ballistic-looking arc with rotation from the held pose. It retains wall, floor-support, player and endpoint occupancy validation. It can choose a nearby side/back landing without requiring the player to sidestep. An entirely unsupported/occupied area still retains the item safely; it does not promise an impossible landing or stack it on clutter.

Magnet brings actual item models into the hand with staggered animation and leaves only the selected model visible after arrival. Designer UI-only flying chips are disabled. No new rigidbodies or item shadows were added.

Ordinary placements and tosses grant zero cash. Short goals grant 2, rack completion 1, milestones 2/3/5/10 and the designer tutorial 3. Existing balances are preserved.

Supply panel supports cash purchase of search/magnet/slot credits, rewarded packs (including cash), and consumable IAP packs. Credits and transaction IDs persist together; duplicate receipts cannot grant twice, and IAP confirmation follows a successful durable save. Full-inventory/no-match magnet attempts do not consume cash or credits.

Installed Unity IAP 5.5.0 and official AppLovin MAX 8.6.6. LabServiceSettings remains explicitly unconfigured/disabled: supply SDK initialization is Android-only and excluded in Editor. Supply pack product IDs, existing cash product IDs, Google Play public key, MAX SDK key/rewarded unit ID and test-device IDs must be provided before live testing. Empty configuration never grants simulated ad/store rewards. Live store prices come from fetched metadata. Actual ad completion, cancelled ads, Google Play receipts and pending-purchase redelivery require later Android test-track validation; no build was made.

Validation: Logs/CompactControlsTests.xml records passing CoreSortingAndController, DesignerBulkAndIcons and DesignerSoftDropAndRackTargeting; it also preserves the initial 7/8-direction drop failure. After adding a second release origin, Logs/CompactFinalTests.xml passes both DesignerCompactControls and DesignerSoftDropAndRackTargeting (2/2). The compact case verifies 8/8 facing directions, zero placement cash, actual magnet flight/one final held model, search markers, hover corners, direct touch pickup, persistent supply credits, duplicate receipt replay, disk reload and unavailable-SDK behavior. Screenshots CompactSupplyOffers.png, CompactSearchMarkers.png, CompactHoverControls.png and CompactMagnetFlight.png were visually reviewed. No physical-device touch/FPS test or real purchase/ad was performed.

# Continuous spread and reference feedback corrections — 2026-11-11

This section supersedes the older layered-pile and elevated-drop design below. All 4032 existing item IDs now use a continuous, randomized single-layer floor spread: 2887 downstairs and 1145 upstairs. Layout revision 2 migrates only loose poses from older disk layouts; cash, held items, sorted racks and rewards remain preserved. Logs/ContinuousSpreadReport.txt records placement. No extra runtime rigidbodies or item shadows.

DROP searches 72 nearby forward/side floor candidates, rejects occupied endpoints and retains wall/rack/player/path and footprint support checks. It never lifts a dropped item onto existing clutter. If no safe gap exists it retains the item rather than stacking or intersecting it.

SortingItem layer is assigned to item hierarchies at runtime and excluded from the player motor's query mask after Awake. The existing collider callback additionally protects unregistered item children. This prevents dense item colliders entering locomotion queries; picking and throw overlap queries still see items. Grounded braking is faster and tiny residual velocities snap to zero.

Single aimed item uses a cyan outline and a dark-backed cyan name. Search uses gold highlights with a shared-material pulse; no collider scaling or per-item animated material instances. Inventory icons/cards and text contrast were polished using the existing 46 sprites. Reference screenshots and sampled video frames informed the changes; no full reference-art reskin was performed.

Magnet has a context-aware 1.2-second empty/no-match feedback timer and a 0.8-second successful collection timer displayed in the HUD. Empty, full and no-match actions do not charge cash. Selecting another type does not inherit the previous type's timer. Search/magnet remain unlimited range.

Validation: Logs/ReferenceCorrectionTests.xml has 3 passing cases (core/controller, designer icons/migration, soft drop/rack targeting) and the earlier grounding-fixture failure. Logs/ReferenceFocusedTests.xml contains the passing final reference-corrections retest. The fixture now waits for physics ticks before measuring spawn grounding and respects the camera pitch clamp when aiming. All four relevant cases are passing across these runs; the core case includes 691 checks. Final movement drift measured 0, floor-height change approximately 0.000000034 m. All 4032 item bottoms, query-layer exclusion, actual cyan hover, child-collider filtering, no-charge empty/no-match magnet and successful timer HUD were verified. Screenshots Logs/ContinuousHover.png and Logs/ContinuousSpreadHUD.png were inspected. Authored-normal outline fallback warnings remain for unreadable meshes; no runtime Read/Write memory increase was introduced. No player build, long soak or physical Android FPS certification.

---
# Review of rack, item and interaction changes — 2026-11-11

## Bulk distribution, clutter-friendly drop and item icons

4032 existing items/IDs are redistributed into 26 mixed layered piles (155–156 items each) across the authored ground and upper floors. The editor packer uses bounded height cells, varied yaw/sideways tall items, environment collision checks and existing floor/rack-front/spawn exclusions. It rolls back unsuccessful packing before saving. Details: Logs/BulkPileReport.txt. No extra runtime rigidbodies or shadow casters were added.

Designer layoutRevision=1 migrates older disk saves once: loose items use the new authored positions; sorted rack entries, carried items/selection, cash, capacity and reward history still restore. Both snapshot paths and SaveJsonStream persist the revision; subsequent loads retain dropped positions. Existing saves are not deleted or reset. Normal durable-save backup handling remains in place.

DROP searches nearby forward/side candidates, accepts a non-overlapping endpoint above loose clutter, and checks the ground footprint and solid environment. Loose items no longer obstruct the short release path, but endpoint overlap with them is rejected/lifted; walls, racks, player clearance and missing floor remain constraints. This is controlled placement animation, not a rigidbody pile simulation.

The two supplied UI render sheets are sliced through Unity's sprite data provider with capability checks. 46 distinct sprites are mapped to 46 definitions; the blank atlas cell is skipped. Inventory cards, selected-item card and all progress pages use those sprites. Shared source textures are retained, mipmaps disabled for UI. Card size, icon/text spacing and HUD typography were adjusted; the prototype title suffix was removed.

Logs/BulkIconTests.xml: 3/3 passed (CoreSortingAndController, DesignerBulkAndIcons, DesignerSoftDropAndRackTargeting). Verified all 46 icon assignments, unique 4032 IDs, recovery bounds/environment clearance, selected/progress icons, legacy loose-layout migration with cash/hand preservation, revision JSON roundtrip and current-layout drop-position retention; core controls/economy/save regressions and a repeated DROP with an existing ground item also passed. Logs/BulkInventoryHUD.png and Logs/BulkProgressIcons.png visually inspected. No player build or long soak; no Android FPS or pile-physics stability claim.

## Compatible racks and remaining slots

Designer racks share one muted teal compatibility material. With an item selected, racks that can accept its type receive the hint on their existing indicator; full/wrong-type/too-small racks do not. Normal green/red aim feedback takes priority and leaving aim restores the hint. No outline through walls, added meshes/lights or per-item scan. The session updates compatibility on revision, selected-type or modal-state changes. Clearing hands or opening settings removes hints.

Rack labels display exact remaining slots when within the last 10% of capacity (minimum one, maximum five); full racks show FULL. The central hover text also shows this hint for the selected matching type. Retrieving items refreshes both availability and compatibility; empty racks release the old hint/type. Counts use actual size-based or fixed capacity.

Logs/RackHintTests.xml: 3/3 passed in 38.8 seconds: DesignerRackHints, DesignerMaterialFeedback, DesignerSoftDropAndRackTargeting. Checked shared material wiring, compatible empty racks, direct hover priority/restoration, almost-full/full counts, retrieval, inventory reselection, wrong-type exclusion, modal hide, plus sound/glow and safe-drop regressions. No build or long soak; no new mobile FPS claim.

## Material taps and completion polish

Added editable ItemDefinition.placementMaterial (Default/Glass/Plastic/Metal), original short mono tap clips and a quiet completion chime under Audio/Materials. All four WAV files total 52,654 bytes. Designer definitions receive initial material assignments; mixed/boxed items retain Default and designers can override any classification. Missing clips fall back to the existing place clip. The placement event captures the actual placed definition before inventory selection changes can affect feedback.

The existing once-only RackCompleted event drives a 0.75-second subtle mint label glow with a 2.5% scale pulse and a separate quiet chime. Replaced the old reward HUD rack bounce/audio handler to avoid competing animations and duplicate sounds. Reset/disable restores the label and stops the chime; sound mute stops both audio sources. No additional cash rules or item transforms changed. Completion history still uses the existing saved rack ledger.

Logs/MaterialFeedbackTests.xml: 2/2 passed (DesignerMaterialFeedback and DesignerSoftDropAndRackTargeting). Verified clip routing/fallback, actual placed type, completion event, label animation/restoration, mute, refill suppression, snapshot completion flag, and existing drop/rack-targeting behavior. Initial failure from conflicting old/new label animation is preserved in Logs/MaterialFeedbackInitialTests.xml; the duplicate owner was removed before the passing retest. These are live Editor tests; speaker/headphone mix and physical-device haptics still need user/device audition. No build or long soak performed.

## Latest soft interaction update

This supersedes the earlier automatic TOSS behavior described below. DROP stays a nearby floor action (0.65–0.95 m candidate distances), uses a 0.38-second calm motion with no tumbling/bounce, and shares its trajectory with full-size collision sweeps. Landing feedback uses two small short-lived dust particles, quieter sound and light selection haptics. Normal placement feedback is retained.

PLACE appears only with a held item and targeted rack; DROP only with a held item; PICK only with a target item. The nearest rack's full collider bounds accept hover through shelf gaps, but foreground walls and loose items block it. A rack behind a gap cannot steal the nearest rack target.

The five-step tutorial guides movement/look/pick, marks a visible compatible same-floor rack with distance, and waits for actual placement. The PLACE hand replaces the marker when aiming at a compatible rack, avoiding label overlap. Paid tools and dropping are optional. Skip/resume/replay and the once-only reward remain. Three existing small items were moved into clear visible starter positions; all 4032 IDs/items remain. Existing saved positions still restore normally.

Initial short verification: Logs/SoftSortingTests.xml has three passes (designer gameplay, guided tutorial, soft-drop/rack targeting) and one core failure. The failing independent offset-pivot fixture was obstructed by its previously dropped item at the newly shortened landing distance; the fixture now clears that item before testing pivot support. The original failure report is retained. Logs/SoftSortingRetest.xml passes both core controls/safety and the updated guided tutorial (2/2); all four selected cases therefore have passing evidence across the initial suite and focused retest. Logs/SoftTutorialPlace.png was visually inspected after the retest: the rack label is unobstructed and the hand points to PLACE. Designer coverage includes all 46 types and 4032-item cash/progress reload; soft-interaction coverage includes shelf gaps, a farther rack behind a gap, foreground walls, button visibility, gentle motion and single landing feedback.

No player build or long soak requested/performed for this update. Android feel/FPS still needs real-device verification; these are Editor tests.

Claude's designer integration is retained: 46 types, 46 racks, 4032 loose items in mixed piles, six shared atlas materials. Capacity varies with item width, depth, rotated bounds and shelf clearance (9–390 per rack). One full rack's worth of each type totals 4032; this is not an exact 3500-item cap.

## Corrections

- Include the 2 mm placement offset in shelf-height acceptance; reject negative/out-of-range point indices.
- Toss collision checks now cover the full tumbling model around its pivot, including centre offset. Initial overlaps and saturated nonalloc buffers reject the route. Loose and sorted item colliders are no longer ignored.
- Check the entire final approach with oriented box sweeps, including settling onto the shelf. Enter inside the front posts, move above the raised front edge, then settle downward. Conservative clearance may disable TOSS for large items or crowded routes; normal PLACE remains available.
- Remove the 8% landing scale expansion, which could overlap neighbouring items in tightly packed racks.
- Refresh the aim on the throw click instead of using the throttled preview target. Restore clears transient toss streak, cached rack bounds and preview timing.
- Correct designer test cameras: overhead view below the roof and full-rack screenshot aimed at the rack. Targeting tests use a clear direct ray plus settled eye position, while retaining the actual TargetItem assertion.
- Remove a delayed scene-open callback from the review runner that could fire during the next test's Play Mode.

## Verification

- Logs/RackPlacementCheck.json: 46 racks × 46 types, 185472 virtual placements, zero reported geometry/support/overlap issues.
- Logs/ReviewTests.xml: initial 11-case short suite, 10 passed; toss failed under the stronger collision checks.
- Logs/ReviewTossTests.xml: corrected toss and safety regressions both passed (42 toss checks plus 7 targeted safety checks).
- Logs/ReviewDesignerTests.xml: final isolated designer regression passed, 1362 checks. All 46 types picked, placed, retrieved and replaced; 390-item full rack, wrong-type rejection, exact shelf positions, full magnet no-charge and 4032-item durable cash/inventory reload verified.
- Thus all 11 distinct selected short cases have passing evidence across the suite and focused retests; the original suite XML is intentionally retained with its initial failure, not rewritten as a fabricated green run.
- An intermediate isolated run was interrupted by Unity AI relay connection.state_change / process-exit logging. The relay was temporarily stopped through RelayService.StopAsync for the final run; gameplay error checking remained enabled. Relay restart was requested afterwards.
- Logs/DesignerGameplay-FullRack.png and DesignerPiles.png visually inspected after the final run.
- Actual 4032-item scene: approximately 4092 total batches before / 244 after spatial instancing, 184 item submissions. Median Camera.Render CPU time 11.16 ms before / 11.56 ms after in this run. Batch reduction is verified, but a speedup is NOT demonstrated by this sample; no Android FPS claim.
- Logs/SaveHitchPerformance.json: synthetic 3500-item / 120-request test passed, main-thread capture median 0.0164 ms, P95 0.0515 ms, max 0.1152 ms, zero Gen0 collections in that measured window. Allocation counter calibration failed, so allocation-byte totals are not claimed.

No player/APK build or 15-minute soak performed. Earlier long-session results do not establish memory stability for these new changes.

## Next work

Profile the real environment on a chosen low-end device when device/build testing is authorized. Review the 4032-item distribution and rack label polish with the designer. Firebase/Ads/IAP SDK integration remains separate pending work; no live credentials were reused.



