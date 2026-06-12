# Contributing

Keep this package small and backend-neutral.

## Design Rules

- Prefer explicit dependencies over global lookup.
- Do not add ServiceLocator.
- Do not add backend project/object/version lookup to the core package.
- Do not add API to the core package.
- Do not add glTF, Addressables, cache policy, or material remapping until there is a concrete package-level need.
- Keep methods pure and idempotent where practical.
- Use Newtonsoft.Json for serialization.
- Add tests for pure logic and cleanup behavior.

## Validation

Before publishing changes:

1. Run EditMode tests for `Deucarian.ObjectLoading.Tests`.
2. Import the direct URL sample into a Unity project.
3. Confirm a known AssetBundle URL can load and unload without leaking instantiated objects.
4. Confirm diagnostics are useful but do not mutate materials.
