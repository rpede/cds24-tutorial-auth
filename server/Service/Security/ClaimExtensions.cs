using System.Security.Claims;
using DataAccess.Entities;

namespace Service.Security;

public static class ClaimExtensions
{
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)!.Value;

    public static IEnumerable<Claim> ToClaims(this User user, IEnumerable<string> roles) =>
    [
        new(ClaimTypes.Name, user.UserName!),
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        .. roles.Select(role => new Claim(ClaimTypes.Role, role))
    ];

    public static IEnumerable<Claim> ToClaims(this User user, params string[] roles) =>
        ToClaims(user, roles.AsEnumerable());

    public static ClaimsPrincipal ToPrincipal(this User user, IEnumerable<string> roles) =>
        new ClaimsPrincipal(new ClaimsIdentity(user.ToClaims(roles)));

    public static ClaimsPrincipal ToPrincipal(this User user, params string[] roles) =>
        new ClaimsPrincipal(new ClaimsIdentity(user.ToClaims(roles.AsEnumerable())));
}