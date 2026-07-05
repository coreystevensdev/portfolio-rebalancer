using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PortfolioRebalancer.Tests.Api;

internal static class TestJwt
{
    // 32+ char secret -- matches what the test factory configures.
    internal const string Secret = "test-secret-key-at-least-32-chars!!";

    internal static string Generate(string userId = "test-user-id")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, userId)],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
