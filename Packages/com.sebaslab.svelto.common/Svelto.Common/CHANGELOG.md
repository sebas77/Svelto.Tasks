# Changelog
All notable changes to this project will be documented in this file. Changes are listed in random order of importance.

## [3.5.2] - 07-2024

* Improve FasterList Enumerator safety
* Add new methods to and improve ThreadUtility
* Various other small improvements

## [3.5.1] - 01-2024

* Optimise ThreadUtility.SleepWithOneEyeOpen
* CompressLogsToZipAndShow works on Android too
* Improve ConsoleLogger
* Improve Svelto Streams
* added FixedTypedArray32

## [3.5.0] - 09-2023

* Refactor: split NB/MB struct from their internal logic that must be used only by the framework. Eventually NB and MB structs must be ref, as they are not supposed to be held (they may become invalid over the time). However due to the current DOTS patterns this is not possible. In future a sentinel pattern will allow to lease these buffers with the assumption that they can't be modified while held (and if a modification happens an exception will throw)

## [3.4.3] - 05-2023

* fix platform profiler compilation bugs
* Svelto.Console improvements
* Improve the new stream related classes. They are used successfully in my current project, unfortunately no much doc available yet
* few FasterList improvements~~~~

## [3.4.0] - 03-2023

* removed static caches used in performance critical paths as they were causing unexpected performance issues (the fetching of static data is slower than i imagined)
* add Native prefix in front of the native memory utilities method names
* largely improved the console logger system
* minor improvements to the platform profiler structs
* improvements to the ThreadSafeObjectPool class (some refactoring too)
* added several datastructures previously belonging to Svelto.ECS
* all the FastClear methods are gone. The standard clear method now is aware of the type used and will clear it in the fastest way possible
* MemClear is added in case memory needs to be cleared explicitly
* added new SveltoStream, Unmanaged and Managed stream classes, their use case will be documented one day
* renamed the Svelto.Common.DataStructures namespace to Svelto.DataStructures
* added FixedTypedArray* methods. Fixed size arrays embedded in structs are now possible
* FasterList extension to convert to Span and ByteSpan
* Fix reported bugs
* Minor Svelto Dictionary improvements
* Added ValueContainer, a simple int, TValue dictionary based on sparse set. It has very specific use cases at the moment. Mainly to be used for the new ECS OOP Abstraction resoruce manager


## [3.3.0] - 04-2022

Note: this is quite a big release, so I'm not going to list every single change.

* improved HashHelper algorithm
* added Burst compatible NativeRefWrapperType
* added utility functions to know if a type is unmanaged
* FasterList: removed the confusing ExpandTo, the new methods are now called: SetCountTo and EnsureCountIsAtLeast
* Fixed FasterListEnumerator bug
* Made FasterDictionary easier to debug
* FasterDictionary: Renamed GetOrCreate to GetOrAdd to use similar names to the ConcurrentDictionary methods
* FasterDictionary: Added RecycleOrAdd methods
* FasterDictionary: deleted the confusing ResizeTo method the new methods are now: EnsureCapacity and IncreaseCapacityBy
* Made SveltoDictionary easier to debug
* SveltoDictionary: Similar changes to FasterDictionary
* SveltoDictionary: slightly improve performance
* Tons of other optimizations and bug fixes~~~~

## [3.2.0]

* ICompositionRoot OnContextDestroyed now receive a flag to know if the OnContextInitialized ever had the chance to be called
* Fix some naming case issues, not following the Svelto convention
* Improve FasterList interface
* Add Intersect/Exclude/Union methods to FasterDictionary to work with Sets
* Improve all the SveltoDictionary and derivates interfaces
* Changed (again and still not final) the logic behind the PlatformProfiler markers
* Refactor MemoryUtilities functionalities

## [3.1.3]

### Fixed

* fixed serious mistake in the RefWrapper equality logic that could affect certains kind of keys in the new FasterDictionary (most notable strings)

## [3.1.0]

### Changed

* UnityContext class won't call OnContextDestroyed on application quit
* RefWrapper, which is the way to have always struct keys in a fasterdictionary, now implicitly converts to the reference type it refers to
* Added a property to know if an IBuffer implementation buffer is valid
* FasterDictionary now wraps SveltoDictionary
* FasterDictionary doesn't accept reference types as key anymore, use RefWrapper to convert them
* renamed FasterDictionaryNode to SveltoDictionaryNode
* removed string based pools from the ObjectPool class
* added a very simple implementation of a sparseset

### Fixed

