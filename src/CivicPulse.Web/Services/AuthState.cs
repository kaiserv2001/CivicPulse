namespace CivicPulse.Web.Services;

public class AuthState
{
    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public bool IsAuthenticated => Token is not null;

    public event Action? OnChange;

    public void SetToken(string token, string email)
    {
        Token = token;
        Email = email;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        Token = null;
        Email = null;
        OnChange?.Invoke();
    }
}
