using Support.Domain.Common;
using Support.Domain.Enums;

namespace Support.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public Role Role { get; private set; }
    public bool IsActive { get; private set; }
    
    // For Passengers - optional PNR for quick lookup
    public string? LastKnownPNR { get; private set; }

    private User() { } // EF Core

    public User(string email, string passwordHash, string fullName, Role role)
    {
        Email = email;
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        IsActive = true;
    }

    public void UpdateLastKnownPNR(string pnr)
    {
        LastKnownPNR = pnr;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}
