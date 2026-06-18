namespace CivicPulse.API;

/// <summary>
/// Credentials for the shared, always-available demo account. Seeded on startup
/// (see Program.cs) and offered as a one-click "Sign in as demo" button on the
/// login page so recruiters can explore without registering their own email.
/// In the in-memory demo this is re-seeded on every boot/wake-from-idle.
/// </summary>
public static class DemoAccount
{
    public const string Email = "demo@civicpulse.app";
    public const string Password = "Demo1234!";
}
