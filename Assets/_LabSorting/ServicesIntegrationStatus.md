# Designer scene and services integration — in progress

No player build requested or produced. Live account IDs are not created yet; the user will provide them later. Do not copy Janitor/Bulldozer Firebase config, ad IDs, app identity or store IDs into Lab Sorting.

## Current changes, not yet verified in Unity

- Camera smoke check now waits for 0.05 seconds of actual game time after AddLook instead of assuming an EditMode test coroutine yield is a player Update. The previous runner stopped near this assertion; retest before calling it resolved.
- Janitor InternetConnectivity adapted to LabSorting.Services; advisory connectivity, bounded optional HTTPS probes, no offline gameplay block.
- Janitor SafeAreaPanel/Monitor adapted to LabSorting.UI. These are available components, not yet attached to the designer gameplay HUD.
- LabStartup loads Assets/Scenes/Lab_Sorting.unity automatically after starting connectivity. DesignerStartupSetup.Apply creates a new MainMenu loading scene and sets it first, designer scene second, via Unity APIs. This tool has NOT been executed; no MainMenu scene or changed build order is claimed.
- LabServiceSettings defines blank account/product settings with live services disabled. Planned products: Remove Ads (non-consumable), two cash packs (consumables). Amounts 1000/5000 are configurable draft defaults, not registered products.
- LabServicePackageInstaller.Install is prepared for Unity IAP and a single external dependency manager. It has NOT run; manifest/lock were not hand-edited. It refuses import with less than 10 GiB free.
- Designer item batch copied to Art/Items/DesignerBatch01: Lab_Items.fbx plus four textures (~14 MB). Unity import, model extraction, materials and gameplay prefab wiring are pending.

## Inspected references

- Designer environment: Assets/Scenes/Lab_Sorting.unity; Assets/LabSorting/Art/Fbx includes environment, Lab_Rack, Colliders and Banners.
- Janitor: Assets/0_Project Data/2_Scripts/Services — MAX, Google UMP, Firebase Analytics/Crashlytics, Unity IAP 5, receipt verification and purchase ledger.
- Bulldozer: Assets/0_ProjectData/Services — the same SDK family, persistent ownership, offline-friendly connectivity and save-before-purchase-confirm behavior.

Selected stack: Firebase Analytics/Crashlytics, AppLovin MAX + Google UMP consent, Unity IAP 5, one EDM resolver. Do not install LevelPlay alongside MAX. SDKs are NOT installed in Lab Sorting yet and service adapters/shop/reward fulfillment are NOT implemented. Account configuration is intentionally unavailable; installed SDKs must stay unconfigured until Lab Sorting credentials arrive. SDK downloads/imports require disk headroom first.

Official references for the next import:
- https://firebase.google.com/docs/unity/setup
- https://github.com/AppLovin/AppLovin-MAX-Unity-Plugin/releases
- https://github.com/googleads/googleads-mobile-unity/releases
- https://docs.unity.com/en-us/iap

## Resume order

1. Free 10–15 GB on C drive (last check around 127 MB). Existing runs already failed actual writes at zero space; avoid another import/test until space is restored.
2. Run the camera/core regression in isolation; resolve actual failure if it remains. Complete pending frame-pacing/render/frustum and soak verification.
3. Inspect designer scene through Unity: cameras/player, physical floor/walls, rack geometry/slots and scale. Preserve authored art/lighting. Wire the permanent player/session/HUD only after that inspection.
4. Execute native MainMenu setup; verify boot loads the designer scene exactly once without internet or a Play button. Attach safe-area only to appropriate direct Canvas children, avoiding double inset.
5. Install/verify SDKs and exact resolved versions; add Lab-specific adapters. Implement three products with receipt verification, durable atomic cash/entitlement + transaction ledger, retry and duplicate protection before store confirmation. No fabricated successful purchases or real ad traffic in Editor.
6. Configure bounded ads and consent lifecycle, Firebase collection choices, privacy controls, restore and offline UI; test unconfigured startup, duplicate roots, callbacks and failure paths. Leave missing live credentials explicit.
7. Import/extract item prefabs, map shared atlas/materials and rack slots, rerun integration validation and 3500-item performance checks. No APK/player build without a later request.
