using Support.Domain.Entities;

namespace Support.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
    string GeneratePassengerToken(string pnr, string lastName, string email);
}
