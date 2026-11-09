using ERP.MFG.Application.Abstractions;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Application.Services;

/// <summary>Plant time from the injected <see cref="TimeProvider"/> (tests replace it) and the plant's IANA time zone.</summary>
public sealed class PlantClock(TimeProvider time) : IPlantClock
{
    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    private static TimeZoneInfo ZoneOf(Plant plant) => TimeZoneInfo.FindSystemTimeZoneById(plant.TimeZoneId);

    public DateTime LocalNow(Plant plant)
        => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, ZoneOf(plant)), DateTimeKind.Unspecified);

    public DateOnly Today(Plant plant) => DateOnly.FromDateTime(LocalNow(plant));

    public DateTime ToLocal(Plant plant, DateTime utc)
        => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ZoneOf(plant)), DateTimeKind.Unspecified);

    public DateTime ToUtc(Plant plant, DateTime local)
        => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), ZoneOf(plant));
}
