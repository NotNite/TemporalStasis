using System.Reflection;
using System.Runtime.InteropServices;

namespace TemporalStasis.Compression;

public class OodleLibraryFactory : IOodleFactory {
    internal const string OodleLibraryName = "oodle-network-shared";

    /// <param name="path">
    /// Optional path to the Oodle Network Compression dynamic library. Defaults to <c>oodle-network-shared</c>.
    /// </param>
    public OodleLibraryFactory(string? path = null) {
        // This technically adjusts the whole assembly which isn't great, not much I can do about it though
        if (path is not null) {
            NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), (name, assembly, searchPath) => {
                if (name == OodleLibraryName) name = path;
                return NativeLibrary.Load(name, assembly, searchPath);
            });
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The created <see cref="IOodle"/> instance uses the TCP variant of Oodle. If FFXIV ever switches to the UDP
    /// variant of Oodle, this instance will not work and Temporal Stasis must be updated.
    /// </remarks>
    public IOodle Create() => new OodleLibraryTcp();
}
