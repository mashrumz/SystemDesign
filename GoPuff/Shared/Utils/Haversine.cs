namespace Shared.Utils;

/// <summary>
/// Haversine formula — great-circle distance between two points on Earth.
/// </summary>
public static class Haversine
{
    private const double EarthRadiusMiles = 3958.8;

    /// <summary>Returns the distance in miles between two lat/lon points.</summary>
    public static double DistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMiles * c;
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}
