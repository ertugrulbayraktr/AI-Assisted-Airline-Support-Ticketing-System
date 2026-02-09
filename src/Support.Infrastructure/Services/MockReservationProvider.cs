using Support.Application.Interfaces;
using Support.Application.Models;
using System.Text.Json;

namespace Support.Infrastructure.Services;

public class MockReservationProvider : IReservationProvider
{
    private List<ReservationInfo>? _reservations;
    private readonly string _dataFilePath;

    public MockReservationProvider(string? dataFilePath = null)
    {
        _dataFilePath = dataFilePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "reservations.json");
    }

    private async Task EnsureDataLoadedAsync()
    {
        if (_reservations != null) return;

        if (File.Exists(_dataFilePath))
        {
            var json = await File.ReadAllTextAsync(_dataFilePath);
            _reservations = JsonSerializer.Deserialize<List<ReservationInfo>>(json) ?? new List<ReservationInfo>();
        }
        else
        {
            // Fallback to in-memory mock data
            _reservations = GetMockReservations();
        }
    }

    public async Task<ReservationInfo?> GetReservationAsync(string pnr, string lastName, CancellationToken cancellationToken = default)
    {
        await EnsureDataLoadedAsync();

        return _reservations?.FirstOrDefault(r =>
            r.PNR.Equals(pnr, StringComparison.OrdinalIgnoreCase) &&
            r.Passengers.Any(p => p.LastName.Equals(lastName, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<bool> VerifyPnrAsync(string pnr, string lastName, CancellationToken cancellationToken = default)
    {
        var reservation = await GetReservationAsync(pnr, lastName, cancellationToken);
        return reservation != null;
    }

    private List<ReservationInfo> GetMockReservations()
    {
        return new List<ReservationInfo>
        {
            new ReservationInfo
            {
                PNR = "ABC123",
                Passengers = new List<Passenger>
                {
                    new Passenger { FirstName = "John", LastName = "Doe", Email = "john.doe@example.com" }
                },
                Segments = new List<FlightSegment>
                {
                    new FlightSegment
                    {
                        FlightNumber = "AA100",
                        Departure = "JFK",
                        Arrival = "LAX",
                        DepartureTime = DateTime.UtcNow.AddDays(7),
                        ArrivalTime = DateTime.UtcNow.AddDays(7).AddHours(6)
                    }
                },
                FlightStatus = "on-time",
                BaggageStatus = "checked",
                FareRules = new FareRules
                {
                    IsRefundable = true,
                    IsChangeable = true,
                    CancellationFee = 50.00m
                }
            },
            new ReservationInfo
            {
                PNR = "XYZ789",
                Passengers = new List<Passenger>
                {
                    new Passenger { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@example.com" }
                },
                Segments = new List<FlightSegment>
                {
                    new FlightSegment
                    {
                        FlightNumber = "BA200",
                        Departure = "LHR",
                        Arrival = "CDG",
                        DepartureTime = DateTime.UtcNow.AddDays(2),
                        ArrivalTime = DateTime.UtcNow.AddDays(2).AddHours(1)
                    }
                },
                FlightStatus = "delayed",
                BaggageStatus = "pending",
                FareRules = new FareRules
                {
                    IsRefundable = false,
                    IsChangeable = true,
                    CancellationFee = 100.00m
                }
            }
        };
    }
}
