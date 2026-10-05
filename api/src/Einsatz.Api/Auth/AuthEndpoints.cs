using System.Security.Claims;
using Einsatz.Api.Data;
using Einsatz.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Einsatz.Api.Auth;

public sealed record LoginRequest(string Login, string Password);
public sealed record UserDto(Guid Id, string Login, string Name, Role Role);

public static class AuthEndpoints
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static string Hash(User user, string password) => Hasher.HashPassword(user, password);

    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/login", async (LoginRequest req, IDbContextFactory<AppDb> dbs, HttpContext http) =>
        {
            await using var db = await dbs.CreateDbContextAsync();
            var login = req.Login.Trim().ToLowerInvariant();
            var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login);
            if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)
                return Results.Problem("Benutzername oder Passwort ist falsch.", statusCode: 401);

            var identity = new ClaimsIdentity(
                [new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Name), new(ClaimTypes.Role, user.Role.ToString())],
                CookieAuthenticationDefaults.AuthenticationScheme);
            await http.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.Ok(new UserDto(user.Id, user.Login, user.Name, user.Role));
        }).RequireRateLimiting("login").AllowAnonymous();

        g.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync();
            return Results.NoContent();
        });

        g.MapGet("/me", async (CurrentUser me, IDbContextFactory<AppDb> dbs) =>
        {
            await using var db = await dbs.CreateDbContextAsync();
            var user = await db.Users.FindAsync(me.Id);
            return user is null ? Results.Unauthorized() : Results.Ok(new UserDto(user.Id, user.Login, user.Name, user.Role));
        }).RequireAuthorization();
    }
}
