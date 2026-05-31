# Sprint 4 — Polish, Rate Limiting & User Profile

**PM:** @pm  
**Duration:** Week 7–8 (2026-05-31 → 2026-06-13)  
**Theme:** Security hardening (rate limiting), dark mode UX, user profile management, and technical debt payoff.  
**Prerequisite:** Sprint 3 DoD fully checked off.

---

## Sprint Goal
By end of Sprint 4: the API enforces a 60 req/min rate limit per IP; users can switch between light and dark mode with the preference remembered across sessions; authenticated users can update their email and change their password from a profile page; the AQ "No data" state is clearly labelled in the UI rather than showing zeros; and the Compare endpoint fetches both cities fully in parallel.

---

## Tasks

### @backend

- [x] **BACK-13** — Rate-limit middleware (BL-5).
  - **AC:** `builder.Services.AddRateLimiter` with a global `FixedWindowRateLimiter`: 60 permits per 60-second window, partitioned by `RemoteIpAddress`.
  - **AC:** Exceeded requests return HTTP 429 with `Retry-After: 60` header and a plain-text body.
  - **AC:** `app.UseRateLimiter()` is inserted before `app.MapControllers()`.
  - **Files:** `src/CivicPulse.API/Program.cs`

- [x] **BACK-14** — User profile endpoints (BL-6).
  - **AC:** `GET /api/profile` (JWT required) returns `{ email, createdAt }`.
  - **AC:** `PUT /api/profile/email` (JWT required) accepts `{ newEmail }`, validates uniqueness (409 on conflict), persists, returns updated profile.
  - **AC:** `PUT /api/profile/password` (JWT required) accepts `{ currentPassword, newPassword }`, validates current password (400 on mismatch), persists new hash, returns 204.
  - **Files:** `src/CivicPulse.API/Controllers/ProfileController.cs`

- [x] **BACK-15** — TD-3: Compare endpoint full parallelism.
  - **AC:** `FetchParallelAsync` for both cities is started before either is awaited; a single `Task.WhenAll` awaits both.
  - **Files:** `src/CivicPulse.API/Controllers/DashboardController.cs`

---

### @frontend

- [x] **FRONT-5** — Dark mode toggle (BL-4).
  - **AC:** A button in the navbar toggles between light and dark mode by setting `data-bs-theme` on `<html>`.
  - **AC:** Preference is persisted to `localStorage` under key `cp_theme` and restored on the next visit without a flash of the wrong theme.
  - **AC:** Custom CSS skeleton and border colours adapt to dark mode.
  - **Files:** `src/CivicPulse.Web/wwwroot/js/chartInterop.js`, `src/CivicPulse.Web/Pages/_Host.cshtml`, `src/CivicPulse.Web/Shared/NavMenu.razor`, `src/CivicPulse.Web/wwwroot/css/app.css`

- [x] **FRONT-6** — Profile page (BL-6).
  - **AC:** Page at `/profile` (auth-gated, redirects to `/login` if unauthenticated) shows current email and account creation date.
  - **AC:** "Update email" section: text input + Save button; inline success/error message.
  - **AC:** "Change password" section: current-password + new-password inputs + Save button; inline success/error.
  - **AC:** Nav shows "Profile" link when authenticated.
  - **Files:** `src/CivicPulse.Web/Pages/Profile.razor`, `src/CivicPulse.Web/Services/ApiClient.cs`, `src/CivicPulse.Web/Shared/NavMenu.razor`

- [x] **FRONT-7** — TD-2: Surface "no AQ data" in `AqiBadge.razor`.
  - **AC:** When `AqiCategory == "Unknown"` the component renders a muted "No sensor data available near this location" notice instead of zeroed-out values.
  - **Files:** `src/CivicPulse.Web/Components/AqiBadge.razor`

---

## Blockers & Dependencies
| Dependency | Blocks | Status |
|------------|--------|--------|
| BACK-14 (profile endpoints) | FRONT-6 (profile page) | Pending |

---

## Definition of Done Checklist
- [x] All tasks above have `[x]`.
- [x] `dotnet test` exits 0 (all 20 tests pass).
- [x] API returns HTTP 429 with `Retry-After: 60` after 60 req/min from same IP.
- [x] Dark mode toggle in navbar — persists via localStorage, no flash on reload.
- [x] `/profile` page loads, updates email (409 on duplicate), changes password (400 on wrong current, 204 on success).
- [x] AqiBadge shows "No sensor data available" when AQI category is "Unknown".
- [x] Compare endpoint now starts both `FetchParallelAsync` calls concurrently.
- [x] `docker compose up --build` starts all 3 containers; API + Blazor UI reachable.
