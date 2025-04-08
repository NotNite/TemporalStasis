namespace TemporalStasis.Compression;

public interface IOodleFactory {
    /// <remarks><see cref="IOodle"/> instances are stateful. Use separate instances when needed.</remarks>
    public IOodle Create();
}
