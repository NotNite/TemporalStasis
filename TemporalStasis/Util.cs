namespace TemporalStasis;

internal static class Util {
    /// <summary>Catches various errors thrown by C# networking APIs.</summary>
    public static async Task WrapTcpErrors(Func<Task> func) {
        try {
            await func();
        } catch (IOException) {
            // ignored
        } catch (ObjectDisposedException) {
            // ignored
        } catch (OperationCanceledException) {
            // ignored
        }
    }
}
