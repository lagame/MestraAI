using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace RPGSessionManager.HealthChecks
{
    public class SignalRHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // In a real application, you would check the status of your SignalR hub.
            // For now, we'll just return a healthy status.
            var isHealthy = true; 

            if (isHealthy)
            {
                return Task.FromResult(HealthCheckResult.Healthy("SignalR hub is healthy."));
            }
            else
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("SignalR hub is unhealthy."));
            }
        }
    }
}

