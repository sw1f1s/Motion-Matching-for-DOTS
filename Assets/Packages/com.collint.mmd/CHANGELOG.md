# Changelog

## [0.3.1] - 2026-10-16

### Fixed

- GPU visual binding now maps required bones by path and hierarchy instead of requiring identical Transform arrays. Mesh names, mesh counts, and bone order can differ between compatible models.
- GPU rig baking includes skin bones and their ancestors rather than unrelated mesh and accessory objects.
- Skinning indices are remapped per visual instance without modifying the shared prefab or animation database.
- Invalid skin bindings and incompatible skeletons are rejected before attachment, with failed visual instances cleaned up.

### Samples

- Added regression tests for renamed meshes, additional skinned parts, reordered bones, skinning matrices, and visual cleanup.
- Updated Samples to depend on MMD 0.3.1.

The database format remains 7. Existing 0.3.0 databases do not need to be re-baked.

## [0.3.0] - 2026-10-15

### Added

- Weighted bone masks and animation layers with independent clip playback or Motion Matching within a source group.
- Base group filters, layer priority, fade transitions, and runtime layer controls.
- Layer Only animation groups excluded from base locomotion matching and feature normalization.
- Mask editing with a bone hierarchy, Humanoid presets, branch weights, search, and Undo.

### Changed

- Aligned the mask, layer, and bake inspectors with standard Unity controls and English tooltips.
- Updated the database format from 6 to 7. Re-bake databases created with earlier versions before using this release.

### Samples

- Added the hammer running animation, arm masks, and a configured weapon layer.
- Updated Samples to version 0.3.0 with a dependency on MMD 0.3.0.
