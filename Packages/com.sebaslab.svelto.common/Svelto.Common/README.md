# Svelto.Common
Shared code between the Svelto repositories

For Unity Users: to solve the unsafe dependency you need to add the following scopedRegistries in manifest.json:
```
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.openupm",
        "com.sebaslab.svelto.common",
        "com.sebaslab.svelto.ecs",
        "com.sebaslab.svelto.unsafe",
        "org.nuget.system.buffers",
        "org.nuget.system.memory",
        "org.nuget.system.numerics.vectors",
        "org.nuget.system.runtime.compilerservices.unsafe"
      ]
    }
  ]
```

The easiest way to install is through the [openupm CLI](https://openupm.com/docs/getting-started/): it resolves the dependency graph automatically (including the `org.nuget.*` packages) and writes the scoped registry into `Packages/manifest.json` for you:

```
openupm add com.sebaslab.svelto.tasks
```

`com.sebaslab.svelto.common` and `com.sebaslab.svelto.tasks` declare the `org.nuget.system.runtime.compilerservices.unsafe` / `org.nuget.system.memory` dependencies in their `package.json`. If you install by hand-editing the manifest without the registry above, Unity will fail package resolution with an explicit error naming the missing `org.nuget.*` package — not with cryptic compile errors such as `CS0117: 'Unsafe' does not contain a definition for 'Unbox'`.
