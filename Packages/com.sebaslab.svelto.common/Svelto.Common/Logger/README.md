# Svelto.Console

`Svelto.Console` is the shared logging entry point used by Svelto.Common.

## Default behavior

### Unity
- On first use, `Svelto.Console` registers `DefaultUnityLogger`
- `Svelto.Console.Log(...)` and related APIs are formatted by Svelto and forwarded to Unity's logger
- If Unity logger replacement is not enabled, Svelto forwards to the current Unity log handler

### Non-Unity
- On first use, `Svelto.Console` registers `SimpleLogger`
- `SimpleLogger` writes to `System.Console`

## Which API to use

### `Svelto.Console.Log(string txt)`
Use for normal informational logs.

Characteristics:
- no extra stack generation
- Unity: forwarded as a normal Unity log
- Non-Unity: written to `System.Console`

### `Svelto.Console.LogDebug(string txt)`
Use for debug-only logs.

Characteristics:
- compiled only in `DEBUG`
- intended for temporary or noisy diagnostics

### `Svelto.Console.LogWarning(string txt)`
Use for unexpected but recoverable situations.

Characteristics:
- formatted as a warning
- no extra stack generation by default

### `Svelto.Console.LogError(string txt, Dictionary<string, string> extraData = null)`
Use for real errors when no exception object is available.

Characteristics:
- error formatting
- includes Svelto-generated stack information
- optional structured extra data

### `Svelto.Console.LogException(Exception exception, string message = null, Dictionary<string, string> extraData = null)`
Use when you have an actual exception instance.

Characteristics:
- preserves exception context
- includes exception stack information
- optional extra message and extra data

## Logger setup APIs

### Default behavior only
If you just call `Svelto.Console.Log(...)`, no extra setup is required.

This is the safest/default mode.

### `Svelto.Console.DefaultLog.ReplaceUnityLogger(bool keepLogHandlerInEditor = false)`
Use this only when you want all `Debug.Log*` calls to flow through Svelto's Unity bridge.

What it does:
- stores the current Unity log handler in `previousLogHandler`
- replaces `Debug.unityLogger.logHandler` globally
- all Unity logs are then processed through Svelto

Important:
- this is a global process-wide change
- order matters if other code also replaces `Debug.unityLogger.logHandler`
- use only if you explicitly want Unity log interception

### `Svelto.Console.FasterLog.UseGlobally(bool replaceUnityLogger)`
Use this when you want the additional fast/file logger.

What it does:
- keeps `DefaultUnityLogger` registered
- adds `FasterUnityLogger`
- optionally also replaces Unity's global logger if `replaceUnityLogger` is `true`

Important:
- this is additive, not a replacement of the default Svelto logger
- `Svelto.Console` logs can be emitted to more than one sink
- in player builds the faster logger redirects `System.Console.Out` to its own file writer

## Multiple loggers

`Svelto.Console` supports multiple registered loggers.

Current important cases:
- `DefaultUnityLogger` forwards to Unity
- `FasterUnityLogger` writes batched/fast logs to its own sink
- `SimpleLogger` is the non-Unity fallback

All registered loggers receive the same `InternalLog(...)` call.

## Zip export

`Svelto.Console.CompressLogsToZipAndShow(zipName)` forwards the request to all registered loggers.

This matters when more than one logger is active, for example when `FasterUnityLogger` is registered in addition to `DefaultUnityLogger`.

## Caveats

### Unity replacement is global
`ReplaceUnityLogger(...)` changes Unity's global log handler.

That means:
- every `Debug.Log*` is affected
- third-party systems that depend on Unity's handler chain can be affected
- ordering with other handler replacements matters

### Unity-bound logs can still be observed externally
If a log ultimately goes through Unity logging, other systems listening to Unity log callbacks can still observe it.

For example:
- crash reporting integrations
- diagnostics/telemetry tooling
- custom listeners using Unity log callbacks

### FasterLog is not the only logger
`FasterLog.UseGlobally(...)` does not remove `DefaultUnityLogger`.

If you need a single-sink behavior, review the active logger list carefully.

## Examples

### Basic usage
```csharp
Svelto.Console.Log("Loading realm data");
Svelto.Console.LogWarning("Realm responded with partial data");
Svelto.Console.LogError("Failed to deserialize payload");
```

### Exception usage
```csharp
try
{
    DoWork();
}
catch (Exception e)
{
    Svelto.Console.LogException(e, "Error while running DoWork");
}
```

### Replace Unity logger globally
```csharp
Svelto.Console.DefaultLog.ReplaceUnityLogger();
```

### Enable faster/file logger too
```csharp
Svelto.Console.FasterLog.UseGlobally(replaceUnityLogger: false);
```
