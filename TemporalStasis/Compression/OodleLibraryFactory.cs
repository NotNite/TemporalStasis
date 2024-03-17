namespace TemporalStasis.Compression;

public class OodleLibraryFactory(string path) : IOodleFactory {
    public IOodle Create() => new OodleLibrary(path);
}
