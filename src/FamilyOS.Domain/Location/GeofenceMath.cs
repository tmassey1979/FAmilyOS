namespace FamilyOS.Domain.Location;

/// <summary>Deterministic geofence math shared by location processing and tests.</summary>
public static class GeofenceMath
{
    private const double EarthRadiusMeters = 6_371_000;
    public const double ExitHysteresisMeters = 25;

    public static double DistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var dLat = (latitude2 - latitude1) * Math.PI / 180;
        var dLon = (longitude2 - longitude1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLat / 2), 2)
            + Math.Cos(latitude1 * Math.PI / 180) * Math.Cos(latitude2 * Math.PI / 180)
            * Math.Pow(Math.Sin(dLon / 2), 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// Uses a larger exit boundary than entry boundary to reduce GPS jitter.
    /// </summary>
    public static bool IsInside(bool wasInside, double distanceMeters, double radiusMeters)
        => distanceMeters <= radiusMeters + (wasInside ? ExitHysteresisMeters : 0);
}
