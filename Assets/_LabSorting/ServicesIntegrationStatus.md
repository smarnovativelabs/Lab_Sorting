# Ads, IAP and Tenjin update — 2026-11-18

This section supersedes the ad timing and Editor notes below.

## Editor testing (new)

`LabServiceSettings.editorTestServices` (on by default, Editor only, never in device builds):

- **Ads:** UMP is skipped in the Editor and MAX starts with its placeholder ads. Rewarded videos show the MAX stub (press "Reward" then "Close" to receive the reward); interstitials show the stub interstitial.
- **IAP:** Unity IAP's fake store connects, every product shows `$0.01`, and a purchase opens an IMGUI "Buy / Cancel" dialog in the Game view. The fake store has no signed receipt, so Editor test mode only checks that the order contains the product; device builds still verify the Google Play signature.
- Buying Remove Ads in the Editor writes a real local entitlement and stops interstitials in Editor play. Clear it with Tools > Lab Sorting > Services > Editor test: reset Remove Ads purchase.
- Automated tests set the SessionState key `LabSorting.SuppressEditorTestServices`, so they keep exercising the no-SDK path.

Verified 2026-11-18 in Editor play mode: MAX initialized, rewarded stub granted +2 magnets and restarted the interstitial clock, the interstitial showed "Displaying Ad In: 5 … 1 Seconds" and then the stub, Settings open paused the clock, and a fake-store magnet pack delivered +10. The Editor save was restored afterwards.

Automated tests after the change: RewardedAdReturnRecovery, DesignerServices, DesignerArtworkUI (ad countdown checks) and ConnectivityPanelTests passed. MainMenuPersistentServices fails on a stale assertion (`BootstrapChecks` still expects `enableLiveServices` off; it has been on since 631cd34). DesignerOffersReplay and DesignerServiceExperience failed because their scene objects were destroyed early in the run (the real error is hidden by the cleanup exception) while the shared Editor was also busy; neither step touches ad code. Re-run them when the Editor is free.

## Interstitial rules (replaces "completed rack + Settings open")

- Automatic interstitial after `interstitialCooldownSeconds` (120) of active play, preceded by a `interstitialWarningSeconds` (5) on-screen countdown.
- The clock only runs while the player is actually playing: no pause/store/settings/offer/items/completion/review panel, no first-launch tour, no internet gate, and not while the guided tutorial is running.
- The clock restarts after every interstitial and after every rewarded video (completed or closed early).
- If no interstitial has loaded when the clock reaches zero, the game waits and shows the countdown as soon as one is ready. Remove Ads disables interstitials; rewarded videos stay available.
- The completed-rack requirement and the Settings-open trigger were removed.

## Device robustness

- **UMP auto-retry:** a failed consent request now retries by itself while online (5 s, 10 s, 20 s, 40 s, then every 60 s), as in the Motel project. The consent form only appears where a privacy message is required (EEA/UK/Switzerland, or US states if configured); elsewhere UMP returns NotRequired and MAX starts at once.
- Removed the deprecated `MaxSdk.SetSdkKey`; the SDK key comes from AppLovinSettings (identical to LabServiceSettings, verified).
- **Development builds only:** MAX verbose logging (logcat tag `AppLovinSdk`), an AD DEBUGGER button in Settings > Store & Privacy (opens the MAX Mediation Debugger), and a `[dev]` line showing consent, SDK, reward and interstitial state with the seconds to the next interstitial.

## Tenjin

- Now sets the Google Play store type before Connect (`SetAppStoreType(AppStoreType.googleplay)`), matching the Motel and Janitor projects.
- AppLovin impression data sent to Tenjin now includes country, network placement and creative ID (same fields as Tenjin's own AppLovin integration). Firebase `ad_impression` also carries ad format and ad unit.
- Added `com.android.installreferrer:installreferrer:2.2` (Google Play Install Referrer) to the Tenjin dependencies and mainTemplate.gradle for install attribution.
- Tenjin is a native Android SDK, so it stays device-only; Editor play does not send Tenjin events.

## Still required outside Unity (device)

- MAX dashboard: ad units `90ff4a080a00633c` (interstitial) and `86fe615b70b36b8a` (rewarded) must belong to the Android app `com.peri.games.quarantine.lab.sort.simulator`; register the test phone's GAID under Test Mode.
- AdMob: publish the GDPR privacy message for `ca-app-pub-9058006185556068~2175955707` (needed only for EEA/UK users).
- Play Console: upload a build to at least internal testing, create and activate the six product IDs, add the tester's Google account as a license tester. Without this the store returns no products and every price shows "Unavailable".
- Rebuild the APK: the last build (2026-11-16) predates these changes.

# Android account handoff — 2026-11-16

Device APK produced: `Builds/Android/QuarantineLabSort-Android-Test.apk` (110,313,288 bytes), development/debug signed, ARM64, minimum API 25, target API 36, version 0.1.0 (1). Gradle assembleDebug succeeded after recovering from disk exhaustion and corrupt generated Gradle transform caches. APK v2 signature, package/title, billing/internet permissions and the configured AdMob app ID were verified from the packaged APK. Device QA is still pending. See `Builds/Android/DEVICE-TEST-NOTES.txt` and `BUILD-INFO.txt` for test prerequisites and SHA-256.

Latest follow-up: Google Play licensing public key assigned; Unity configuration validation now reports zero blockers (Logs/LabServiceValidation.txt). Actual signed Google Play purchase validation still requires a licensed Android store installation; the Unity Editor receipt validator is a platform stub.

Connectivity regression passed: Logs/ConnectivityPanelTests.xml, 1 test / 0 failures, with 12 behavior checks. The Editor-only injected network signals exercise the real polling coroutine and gameplay panel, including adapter off/on, router outage with Wi-Fi still connected, automatic recovery within eight seconds, Retry, timeScale zero, background/resume, and repeated cycles. Fixed exponential failure backoff that could delay internet recovery by up to 120 seconds: failed-probe retry delay is now capped at five seconds. No PC adapter was disabled and physical Android network switching remains a device QA check. Re-run from Tools > Lab Sorting > Services > Test connectivity panel.

Current configuration supersedes the historical notes below. Title: **Quarantine Lab sort Simulator**. Package: `com.peri.games.quarantine.lab.sort.simulator`. Live services are enabled for Android; Editor runs still skip live SDK initialization.

The supplied Tenjin key and matching Firebase configuration are assigned. MAX SDK key and `https://perigames.com/privacy` were copied from I_Am_Janitor as requested. The supplied Google Play licensing public key is assigned. Receipt verification remains mandatory; purchase availability still depends on active Google Play products and a successful store connection.

| Android ad placement | ID |
| --- | --- |
| MAX interstitial | `90ff4a080a00633c` |
| MAX rewarded | `86fe615b70b36b8a` |
| AdMob app | `ca-app-pub-9058006185556068~2175955707` |
| AdMob interstitial | `ca-app-pub-9058006185556068/4203353245` |
| AdMob rewarded | `ca-app-pub-9058006185556068/1527033103` |

MAX owns runtime ad requests. The producer must map the AdMob ad units to the corresponding MAX placements in the dashboard, confirm the correct Google bidding/waterfall setup, and publish the UMP privacy message for this AdMob app. AdMob IDs are saved in LabServiceSettings for that handoff; they are not separate concurrent direct-ad requests. Both Unity SDK settings assets and the Android manifest use the new AdMob app ID.

## Google Play products for the producer

Create and activate these exact one-time product IDs under the package above. Set prices and regional availability in Play Console; the game displays the returned localized store price. These are not subscriptions.

| Product ID | Type | Delivery |
| --- | --- | --- |
| `lab_sorting_remove_ads` | Non-consumable | Permanently removes automatic interstitials; optional rewarded videos remain |
| `lab_sorting_cash_500` | Consumable | 500 cash |
| `lab_sorting_cash_1500` | Consumable | 1,500 cash |
| `lab_sorting_searches_25` | Consumable | 25 searches |
| `lab_sorting_magnets_10` | Consumable | 10 magnets |
| `lab_sorting_slots_5` | Consumable | 5 carry-slot tokens |

Current rewarded-video grants are 50 cash, 3 searches, 2 magnets, or 2 slot tokens. Current catalog quantities were preserved.

## Consent, events, and verification

UMP runs on Android startup before MAX initialization. Ad loads, retries, and presentation require CanRequestAds and pause while a privacy form is busy. Settings > Store & Privacy exposes ad privacy, retry, restore and privacy policy. Firebase Analytics, Tenjin attribution and Crashlytics are app-managed and always on for Android live builds; players have no switch for them (the old Attribution button is hidden at runtime). Tenjin initializes before Firebase and independently of it, so a Firebase failure does not block install attribution.

Existing Firebase/Tenjin hooks cover session, tutorial, sorting progress, rack completion, search/magnet use, milestones, purchases, rewarded completion, and MAX impression revenue. IAP verifies signed receipts and durably saves rewards before confirmation; duplicate transaction IDs do not grant twice. No purchase validation bypass was added.

The licensing key has now been supplied; rerun Unity configuration validation after account changes. Device validation remains required: UMP initial/returning-user flow and privacy changes, MAX test-device rewarded/interstitial ads, Firebase DebugView, Tenjin sessions/revenue, and licensed-test purchases/restores/redelivery from a Play internal-test install. No Android build or live dashboard delivery is claimed.

Verification on this update: Unity applied the account settings and regenerated Firebase Android resources; `git diff --check` passed. The focused rewarded-ad regression could not finish through the Editor connection. A batch-mode retry exited with code 198 because no valid Unity headless Editor license was available (Logs/AndroidServiceBatch.log); there is no passing test result for this update. The focused test can be rerun through ReviewTestRunner.RunAndroidServices once the Editor is available.

References: [Google UMP Unity flow](https://developers.google.com/admob/unity/privacy), [MAX Google network setup](https://legacy-support.axon.ai/en/max/unity/preparing-mediated-networks/).

# Historical Lab Sorting services handoff — 2026-11-12

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




