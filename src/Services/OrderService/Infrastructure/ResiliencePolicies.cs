using System.Net.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace OrderService.Infrastructure;

/// <summary>
/// Políticas de resiliencia Polly para el HttpClient hacia ProductService.
/// Orden al encadenar en Program.cs: Retry → Circuit Breaker → Timeout.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry: hasta 3 intentos con backoff exponencial + jitter.
    /// Cubre errores HTTP transitorios (5xx, 408) y timeouts de Polly.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger = null)
    {
        var jitter = new Random();

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(jitter.Next(0, 1000)),
                onRetry: (outcome, timespan, attempt, context) =>
                {
                    logger?.LogWarning(
                        "Retry {Attempt} after {Delay}s due to: {Reason}",
                        attempt,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Circuit Breaker: abre tras 3 fallos consecutivos y espera 30s antes de half-open.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger? logger = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    logger?.LogError(
                        "Circuit OPEN for {BreakDelay}s. Reason: {Reason}",
                        breakDelay.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit RESET — tráfico normal reanudado");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit HALF-OPEN — permitiendo una prueba");
                });
    }

    /// <summary>
    /// Timeout: máximo 10 segundos por request (optimistic = respeta CancellationToken).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            seconds: 10,
            timeoutStrategy: TimeoutStrategy.Optimistic);
    }
}