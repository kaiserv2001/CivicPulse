namespace CivicPulse.API;

/// <summary>
/// Identifies a single run of the server process. A fresh id is generated every
/// time the app starts, so the client can detect when the server has restarted
/// (including waking from idle as a new process). In the in-memory demo
/// configuration that also means registered accounts have been wiped, so the
/// client treats an id change as a forced logout.
/// </summary>
public static class ServerInfo
{
    public static readonly string BootId = Guid.NewGuid().ToString("N");
}
