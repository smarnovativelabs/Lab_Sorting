# Lab Sorting services handoff — 2026-11-12

This section supersedes the historical status below. Android is the implemented target. No APK/player build, live purchase, live ad, attribution dashboard delivery or Crashlytics crash upload has been tested; Lab-specific account credentials and Firebase configuration are still absent.

## Installed and connected

- Unity IAP 5.5.0; existing AppLovin MAX Unity 8.6.6 / Android 13.6.4.
- Google Mobile Ads Unity 11.4.0, Google UMP 4.0.0 and MAX Google adapter 25.4.0.0 / Google Ads Android 25.4.0. MAX owns ad requests; AdMob is a mediated network. One EDM4U resolver is retained.
- Firebase Analytics + Crashlytics Unity 13.16.0 (vendor files reused from Janitor, without its account configuration), including native Firebase Unity local repositories.
- Tenjin Unity 1.20.2 scripts and Android SDK 1.22.0 dependency. Android attribution only; iOS ATT/native integration is not claimed.
- Unity Localization 1.5.14, English/Spanish locales and LabUI String Tables. Gameplay/tutorial/store messages and all 46 designer item names have Spanish translations; composed labels preserve numeric counts and store prices. Language choice persists. Additional translation content can be edited in the tables.
- Offline sorting remains available. Connectivity is advisory and does not pretend a network interface guarantees access to a store/ad service. Store/ad errors are handled by the corresponding SDK; Retry and privacy options are available in Settings > Store & Privacy.

Android dependency resolver completed successfully (Logs/LabSdkResolution.txt). This verifies generated dependency configuration/local Firebase AAR conversion, not a player Gradle build or physical-device compatibility.


## Persistent first-scene ownership (2026-11-12 update)

`Assets/_LabSorting/Prefabs/LabServices.prefab` is wired in MainMenu, Lab_Sorting and SortingTest. LabServiceRoot keeps one persistent instance and removes duplicate scene copies before their SDK Start methods run. It owns SupplyCommerce (Unity IAP/MAX/AdMob mediation), LabConsent, LabTelemetry (Firebase Analytics/Crashlytics/Tenjin) and InternetConnectivity. Offline gameplay loading does not await store/ad availability.

Gameplay scene loading rebinds commerce, telemetry and both supply/privacy panels. Purchases arriving before SortingSession finishes its save load are queued and processed only after initialization; receipt validation, durable delivery and transaction de-duplication remain required before confirmation. Store/ad operations are unavailable without a session. Service configuration still uses LabServiceSettings; live services remain disabled until account configuration is provided.

Use the existing Apply account IDs menu after entering real account configuration. The full Configure and wire services command now finishes by applying persistent root wiring. No live SDK transaction or phone build has been performed.

## Store catalog — create these exact IDs

| Product ID | Store type | Delivery |
| --- | --- | --- |
| lab_sorting_remove_ads | Non-consumable | Disable automatic interstitials permanently; optional rewarded ads remain |
| lab_sorting_cash_500 | Consumable | 500 cash |
| lab_sorting_cash_1500 | Consumable | 1500 cash |
| lab_sorting_magnets_10 | Consumable | 10 magnet uses |
| lab_sorting_searches_25 | Consumable | 25 search uses |
| lab_sorting_slots_5 | Consumable | 5 slot tokens; each used token adds one permanent carry slot |

Prices come only from store metadata (`localizedPriceString`). Unavailable products remain unavailable instead of displaying invented prices. Store products must be activated in Google Play and tested with a licensed tester.

Rewarded video amounts: 25 cash, 2 search uses, 1 magnet use, or 1 slot token. No completion callback means no reward. Repeated callbacks use the same transaction token and do not duplicate grants. Slot offers check capacity plus already-owned slot tokens before accepting another offer. Cash/search/magnet economy and zero placement cash remain unchanged.

Receipt validation uses the Google Play licensing public key. Consumable fulfillment persists the reward and transaction receipt before IAP confirmation; redelivery retries saving without granting twice. Remove Ads is an atomic separate entitlement file with backup, so game progress resets do not remove it. Restore Purchases retrieves pending and confirmed non-consumable ownership. Consumables are not advertised as restorable after consumption/reinstallation. There is no server receipt-validation or ad server-side verification backend in this project.

Interstitials wait at least 180 seconds and require a completed rack plus an explicit settings/pause boundary. They do not appear during sorting. Google UMP must allow ads before MAX initializes. Analytics, attribution and crash reporting have separate persisted settings; UMP permission to request ads is not treated as permission for unrelated tracking. Native Firebase first-install collection defaults are disabled until the relevant choice is enabled.

## Designer/account setup

Open `Assets/_LabSorting/Settings/LabServiceSettings.asset` and supply:

1. Lab Sorting Android package name (must match the store app and Firebase).
2. MAX SDK key, Android rewarded and interstitial unit IDs, Google AdMob app ID, and registered test-device IDs for QA.
3. Tenjin Android API key.
4. Google Play licensing public key, HTTPS privacy policy URL, and the six product IDs above (already assigned locally).
5. Put the correct `google-services.json` at `Assets/google-services.json`, then set `firebaseConfigurationAdded`.
6. Configure the AdMob privacy/CMP messages and MAX Google network in the relevant dashboards.
7. Run Tools > Lab Sorting > Services > Apply account IDs, then Validate configuration. Enable `enableLiveServices` only when ready for device/store testing. For telemetry QA, enable the relevant choices in Settings > Store & Privacy, then verify Firebase DebugView/Crashlytics and Tenjin on a device.

The validator checks required fields, duplicate product IDs, Firebase package match and basic licensing-key encoding. It cannot establish dashboard correctness. The full Configure and wire services menu also assigns the default catalog/rewards; use Apply account IDs for subsequent account-only updates.

## Events

Custom gameplay/service events include lab_session_start/pause, tutorial_started/replayed/completed, sorting_progress (every 25 placements, not every frame), search_used, magnet_used, capacity_upgraded, rack_completed, milestone_completed, lab_completion_viewed, purchase_started/confirmed/delivered, reward_ad_started/completed, interstitial_shown and ad_revenue. Firebase `ad_impression` includes revenue/currency/network; Tenjin receives allowed custom events, MAX impression revenue and confirmed Google Play transactions using store price/currency and signed receipt fields. Firebase also receives lab_purchase_delivered with store value/currency; this does not fabricate prices or grant content. No ad IDs, receipt payloads or account keys are printed in these events. Native Crashlytics handles uncaught exceptions when diagnostics are enabled.

Final focused Logs/ServiceIntegrationFinalTests.xml passes DesignerServices after revenue-event wiring. Verification: Logs/ServiceIntegrationRetest.xml passes DesignerServices and CoreSortingAndController (691 smoke checks). Logs/ServiceIntegrationTests.xml contains the passing MainMenuAutomaticallyLoadsDesignerScene case and retains the earlier focus/localization timing failures. Final services tests cover duplicate reward callbacks, receipt replay after snapshot and actual disk reload, durable Remove Ads ownership, full-capacity offers, UI opening, English/Spanish switching, one connectivity owner, and loaded SDK assemblies. Screenshot Logs/LabServicesPanel.png was inspected. Final saved designer scene has 4032 items, persistence enabled, real Remove Ads entitlement false, and live services disabled. Android minimum API remains 25. No build was produced.

## References

- [AppLovin: Google mediated network setup](https://support.applovin.com/en/max/unity/preparing-mediated-networks)
- [Firebase Unity release notes](https://firebase.google.com/support/release-notes/unity)
- [Official Tenjin Unity SDK](https://github.com/tenjin/tenjin-unity-sdk)

# Designer scene and services status — 2026-11-11

No player/APK build produced. The requested 15-minute soak was cancelled when the user asked for a short verification run.

## Implemented and verified

- Camera smoke test waits for actual game time after look input; core/controller suite passed (691 checks).
- Designer scene Assets/Scenes/Lab_Sorting.unity uses permanent player/session/HUD with safe-area components. Incorrect vertical floor collider replaced with horizontal mesh-bounds collider.
- Updated 2026-11-10: 46 designer item types (Batch 01 + Batch 02), 4,032 loose items in 48 mixed floor piles, six shared atlas materials, item shadows disabled. Extracted meshes have bottom-aligned pivots and normalized size (max 42 cm). Permanent assets are under Assets/_LabSorting/Designer.
- Updated 2026-11-10: 46 registered designer racks with size-based points (see README "Size-based rack points"). Each type's item count equals one full rack. Layout remains an integration layout pending designer approval.
- MainMenu automatically loads Lab_Sorting without a Play button or waiting for connectivity. Build-settings order is MainMenu then Lab_Sorting. No build was generated.
- Advisory InternetConnectivity and shared SafeAreaPanel/Monitor adapted from Janitor. Blank LabServiceSettings asset has live services disabled.

## Latest review

See [ReviewStatus.md](ReviewStatus.md) for the 2026-11-11 fixes and current test evidence: 46 types, 46 racks, 4032 items, all 11 selected short cases verified across the suite and focused retests. No new build or long soak.

## Earlier verification (2026-11-05)

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

1. Review actual environment lighting, rack spacing, labels, item scale and pile placement with designer. Batch 02 is integrated (2026-11-10).
2. Profile the actual environment on the selected low-end Android device when device/build testing is authorized; batching alone is insufficient to establish FPS.
3. Complete service SDK/adapters in unconfigured mode, then wire Lab-specific account IDs when supplied.
4. Run the deferred 15-minute soak when the user has time. Do not build without a later request.




