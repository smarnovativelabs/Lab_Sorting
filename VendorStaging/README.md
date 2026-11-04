# Plugin import inventory
Selected plugin files now reside in the root Assets folder. VendorStaging retains provenance documents only. Packages and ProjectSettings have not been initialized, so this is file placement, not a tested Unity import.

Selected: DOTween/Pro/DemiLib, Feel (MMFeedbacks/MMTools/NiceVibrations), Crystal SafeArea, Joystick Pack, ParticleImage from Bulldozer; QuickOutline from Janitor. Demo/Samples/Examples/OlderVersions directories excluded. File metadata retained; manifest lists provenance. Source files unchanged.

Before compilation: initialize Unity project, choose renderer, resolve Feel assembly dependencies (input, render pipeline, Cinemachine and other optional integrations); resolve ParticleImage Collections/Mathematics/Burst references; run DOTween setup; compile and test QuickOutline shaders and Android haptics. No runtime compatibility verified yet.

Deferred: LeanTween (DOTween overlap), separate Haptic (NiceVibrations overlap), AwesomeMask (no current masking requirement), KinematicCharacterController (movement choice pending), LitJson (save format pending), Navmesh Path Draw (not required), bulk VFX packs (select individual effects later), Standard Assets (legacy), Pixel Play (no concrete need yet). Generic Editor folder contains project-specific build hooks and game data tools, so was not copied. No ads/account configuration copied.

Free candidates researched, not downloaded:
- https://assetstore.unity.com/packages/3d/props/tools/free-laboratory-pack-123782
- https://assetstore-fallback.unity.com/packages/3d/environments/sci-fi/free-low-poly-sci-fi-laboratory-environment-demo-cosmic-retro-346024
- https://assetstore.unity.com/packages/tools/gui/safe-area-helper-130488 (already covered locally)
- https://linework.ameye.dev/free-outline/ (alternative if renderer requires it)

Initial full staging ran out of disk; all incomplete copies were removed before lean staging. Disk space must be freed before Unity initialization/import. Commercial source assets retain existing license requirements; do not redistribute standalone.

