using Microsoft.JSInterop;

namespace CivicPulse.API.Services;

public class AuthState
{
    public string? Token { get; private set; }
    public string? Email { get; private set; }
    public bool IsAuthenticated => Token is not null;

    public event Action? OnChange;

    public void SetToken(string token, string email, IJSRuntime? js = null)
    {
        Token = token;
        Email = email;
        if (js is not null)
            _ = js.InvokeVoidAsync("authStorage.save", token, email).AsTask();
        OnChange?.Invoke();
    }

    public void SetEmail(string email)
    {
        Email = email;
        OnChange?.Invoke();
    }

    public void Clear(IJSRuntime? js = null)
    {
        Token = null;
        Email = null;
        if (js is not null)
            _ = js.InvokeVoidAsync("authStorage.clear").AsTask();
        OnChange?.Invoke();
    }

    /// <summary>
    /// Clears in-memory state and awaits removal of the persisted token from
    /// localStorage, so a sign-out is not undone by <see cref="TryRestoreAsync"/>
    /// on the next load.
    /// </summary>
    public async Task ClearAsync(IJSRuntime js)
    {
        Token = null;
        Email = null;
        await js.InvokeVoidAsync("authStorage.clear");
        OnChange?.Invoke();
    }

    public async Task TryRestoreAsync(IJSRuntime js)
    {
        if (IsAuthenticated) return;
        try
        {
            var stored = await js.InvokeAsync<StoredAuth?>("authStorage.load");
            if (stored?.Token is not null && stored.Email is not null)
            {
                Token = stored.Token;
                Email = stored.Email;
                OnChange?.Invoke();
            }
        }
        catch { /* JS not available during prerender */ }
    }

    private sealed record StoredAuth(string? Token, string? Email);
}
