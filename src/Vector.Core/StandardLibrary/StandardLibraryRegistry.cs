using Vector.Core.Modules.Native;
using Vector.Core.StandardLibrary.Collections;
using Vector.Core.StandardLibrary.IO;
using Vector.Core.StandardLibrary.Math;

namespace Vector.Core.StandardLibrary;

/// <summary>
/// Creates the explicitly registered native modules that ship with Vector.
/// A fresh registry is returned for each execution/session so runtime registration state is not shared globally.
/// </summary>
public static class StandardLibraryRegistry
{
    public static NativeModuleRegistry CreateDefault()
    {
        var registry = new NativeModuleRegistry();
        MathModule.Register(registry);
        CollectionsModule.Register(registry);
        IOModule.Register(registry);
        return registry;
    }
}
