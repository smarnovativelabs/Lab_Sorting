# Designer scene and services status — 2026-11-05

No player/APK build produced. The requested 15-minute soak was cancelled when the user asked for a short verification run.

## Implemented and verified

- Camera smoke test waits for actual game time after look input; core/controller suite passed (691 checks).
- Designer scene Assets/Scenes/Lab_Sorting.unity uses permanent player/session/HUD with safe-area components. Incorrect vertical floor collider replaced with horizontal mesh-bounds collider.
- 26 designer item types, 78 initial items, four shared atlas materials, item shadows disabled. Extracted meshes have bottom-aligned pivots and normalized size. Permanent assets are under Assets/_LabSorting/Designer.
- 26 actual designer rack instances, 234 shelf slots, rack type locking/unlocking, pickup/re-pick/place/throw tested across all 26 types. Screenshots visually inspected for initial rack view and placed item alignment. Layout is an integration layout pending designer approval.
- MainMenu automatically loads Lab_Sorting without a Play button or waiting for connectivity. Build-settings order is MainMenu then Lab_Sorting. No build was generated.
- Advisory InternetConnectivity and shared SafeAreaPanel/Monitor adapted from Janitor. Blank LabServiceSettings asset has live services disabled.

## Verification evidence

- Logs/DesignerQuickTests.xml: seven passed, one stress assertion failed because it checked renderer state before the benchmark camera rendered. Test now explicitly renders before checking; runtime visibility logic unchanged.
- Logs/DesignerStressRetest.xml: that remaining stress test passed. Eight distinct short tests verified across the two runs; not a claim of one all-green full-suite XML.
- Logs/DesignerFocusedTests.xml: two focused designer/boot tests passed independently.
- Logs/DesignerGameplayTests.json: 278 checks, actual designer mesh stress at 3500 items; 3584 to 188 total batches (104 item submissions). CPU render timings varied between runs; batch reduction does not prove mobile FPS improvement.
- Logs/StressPerformance.json: synthetic 3500-item benchmark measured 8.98ms ordinary vs 3.93ms spatial-instanced median camera-render CPU time, 101 total batches / 60 item submissions. Reverse-order repeat and cropped-frustum coverage passed. Editor timing, not Android FPS.
- Logs/SaveHitchPerformance.json: 120 requests at 3500 items, main-thread request median 0.0127ms, P95 0.0292ms, max 0.6447ms; zero Gen0 collections during measured window. Allocation counter failed calibration, so byte allocation is not claimed. Durable cash/progress round trips passed.
- No JobTempAlloc / older-than-four-frame warning found in the short-suite or stress-retest logs. Physics catch-up stayed at maximum three steps after injected 200ms stalls.
- Long-session memory stability is NOT newly verified: 15-minute soak deferred at user request.

## Remaining services work

Firebase Analytics/Crashlytics, AppLovin MAX + Google UMP and Unity IAP SDKs are NOT installed yet. Service adapters, shop and fulfillment are NOT implemented. No live IDs/configurations were supplied; do not reuse Janitor/Bulldozer credentials.

LabServicePackageInstaller is prepared but has not run. Planned store products: Remove Ads and two cash packs; amounts are configurable drafts. Install SDKs through their official package/import paths, keep one EDM resolver, and add durable transaction/idempotency handling before purchase confirmation.

## Next tasks

1. Review actual environment lighting, rack spacing, labels and item scale with designer; integrate the next item batch using existing shared atlases.
2. Profile the actual environment on the selected low-end Android device when device/build testing is authorized; batching alone is insufficient to establish FPS.
3. Complete service SDK/adapters in unconfigured mode, then wire Lab-specific account IDs when supplied.
4. Run the deferred 15-minute soak when the user has time. Do not build without a later request.
