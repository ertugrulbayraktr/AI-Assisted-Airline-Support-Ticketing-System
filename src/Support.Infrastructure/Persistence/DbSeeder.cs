using Microsoft.EntityFrameworkCore;
using Support.Application.Interfaces;
using Support.Domain.Entities;
using Support.Domain.Enums;

namespace Support.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        // Check if already seeded
        if (await context.Users.AnyAsync())
        {
            return; // Database already seeded
        }

        // Seed Users
        var adminUser = new User(
            "admin@airline.com",
            passwordHasher.HashPassword("Admin123!"),
            "Admin User",
            Role.Admin);

        var agentUser = new User(
            "agent@airline.com",
            passwordHasher.HashPassword("Agent123!"),
            "Support Agent",
            Role.SupportAgent);

        context.Users.AddRange(adminUser, agentUser);

        // Seed Sample Policy Document
        var cancellationPolicy = new PolicyDocument(
            "Flight Cancellation and Refund Policy",
            @"# Flight Cancellation and Refund Policy

## Overview
This document outlines our policies regarding flight cancellations and refunds.

## Cancellation by Passenger

### Refundable Tickets
- Full refund minus cancellation fee if cancelled 24+ hours before departure
- Cancellation fee: $50 for domestic, $100 for international
- Refund processed within 7-10 business days

### Non-Refundable Tickets
- No refund for cancellation
- May receive travel credit valid for 1 year
- $150 rebooking fee applies

## Cancellation by Airline

### Flight Cancelled
- Full refund regardless of ticket type
- Alternative flight offered at no extra cost
- Compensation for delays over 3 hours: $200-$600 depending on distance

### Flight Delayed
- Delay 2-3 hours: Meal vouchers provided
- Delay 3+ hours: Hotel accommodation if overnight
- Delay 4+ hours: Eligible for compensation

## Medical Emergencies
- Documentation required from licensed medical professional
- Refund or rebooking without fee
- Must provide notice within 48 hours of incident

## Weather-Related Cancellations
- Considered extraordinary circumstances
- Alternative flights offered
- No monetary compensation required by law
- May provide vouchers at airline's discretion

## How to Request Refund
1. Contact customer support via phone or email
2. Provide booking reference (PNR) and passenger details
3. Submit required documentation if applicable
4. Refund processed to original payment method

## Processing Time
- Credit card refunds: 7-10 business days
- Bank transfer: 10-15 business days
- Travel credit: Issued immediately",
            adminUser.Id);

        cancellationPolicy.Publish();

        context.PolicyDocuments.Add(cancellationPolicy);

        await context.SaveChangesAsync();

        Console.WriteLine("✅ Database seeded successfully!");
        Console.WriteLine($"   - Admin: admin@airline.com / Admin123!");
        Console.WriteLine($"   - Agent: agent@airline.com / Agent123!");
        Console.WriteLine($"   - Sample policy document created");
    }
}
