using System.Security.Cryptography;
using System.Text;

namespace SnipIt.Services;

// The key is independent of executable location, so copies of the app share one editor.
// Explicit test profiles stay isolated from the user's running application.
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex mutex;
    private readonly EventWaitHandle activation;
    private RegisteredWaitHandle? registration;
    internal bool IsPrimary { get; }

    internal SingleInstance()
    {
        var profile = Environment.GetEnvironmentVariable("SNIPIT_DATA_DIRECTORY");
        var identity = string.IsNullOrWhiteSpace(profile)
            ? "default"
            : Path.GetFullPath(profile).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        var name = @"Local\SnipIt.WinUI." + hash;
        // Create the event first so a second launch can signal during primary startup.
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, name + ".Activate");
        mutex = new Mutex(true, name, out var created);
        if (created) IsPrimary = true;
        else
        {
            try { IsPrimary = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { IsPrimary = true; }
        }
    }

    internal void ActivateExisting() => activation.Set();
    internal void Listen(Action activate) => registration = ThreadPool.RegisterWaitForSingleObject(
        activation, (_, _) => activate(), null, Timeout.Infinite, false);

    public void Dispose()
    {
        registration?.Unregister(null);
        if (IsPrimary) mutex.ReleaseMutex();
        mutex.Dispose();
        activation.Dispose();
    }
}
