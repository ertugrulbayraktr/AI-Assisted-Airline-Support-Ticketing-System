using Support.Application.Models;

namespace Support.Application.Interfaces;

public interface IReservationProvider
{
    Task<ReservationInfo?> GetReservationAsync(string pnr, string lastName, CancellationToken cancellationToken = default);
    Task<bool> VerifyPnrAsync(string pnr, string lastName, CancellationToken cancellationToken = default);
}
