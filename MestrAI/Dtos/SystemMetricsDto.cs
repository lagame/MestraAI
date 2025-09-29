namespace RPGSessionManager.Dtos;

public class SystemMetricsDto
{
    public int TotalActiveSessions { get; set; }
    public int TotalConnectedUsers { get; set; }
    public int TotalNpcsOnline { get; set; }
    public double OverallAverageResponseTimeMs { get; set; }
    public double OverallP95ResponseTimeMs { get; set; }
    public int TotalCacheHits { get; set; }
    public int TotalCacheMisses { get; set; }
}

