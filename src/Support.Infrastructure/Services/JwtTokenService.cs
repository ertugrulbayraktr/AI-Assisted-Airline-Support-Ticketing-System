using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Support.Infrastructure.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        return GenerateJwt(claims);
    }

    public string GeneratePassengerToken(string pnr, string lastName, string email)
    {
        var claims = new List<Claim>
        {
            new Claim("pnr", pnr),
            new Claim("lastName", lastName),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, "Passenger")
        };

        return GenerateJwt(claims);
    }

    private string GenerateJwt(List<Claim> claims)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Secret"] ?? "YourSuperSecretKeyMinimum32CharactersLongForHS256Algorithm"));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "AirlineSupport",
            audience: _configuration["Jwt:Audience"] ?? "AirlineSupport",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
