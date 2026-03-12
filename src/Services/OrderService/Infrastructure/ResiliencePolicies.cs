using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace OrderService.Infrastructure;

public static class ResiliencePolicies
{
    /// <summary>
    /// Retry: 3 intentos con backoff exponencial + jitter.
    /// Espera: ~1s, ~2s, ~4s (con variación aleatoria).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
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
                    context.GetLogger()?.LogWarning(
                        "Retry {Attempt} after {Delay}ms — {StatusCode}",
                        attempt, timespan.TotalMilliseconds,
                        outcome.Result?.StatusCode);
                });
    }

    /// <summary>
    /// Circuit Breaker: Se abre tras 3 fallos consecutivos,
    /// permanece abierto 30 segundos, luego prueba con half-open.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    Console.WriteLine($"[CircuitBreaker] OPEN — pausing for {breakDelay.TotalSeconds}s. Reason: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                },
                onReset: () =>
                {
                    Console.WriteLine("[CircuitBreaker] CLOSED — recovered.");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("[CircuitBreaker] HALF-OPEN — testing...");
                });
    }

    /// <summary>
    /// Timeout por request individual: 10 segundos.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            seconds: 10,
            timeoutStrategy: TimeoutStrategy.Optimistic);
    }

    /// <summary>
    /// Policy Wrap: Retry → Circuit Breaker → Timeout
    /// El orden importa: el retry re-ejecuta, el CB protege, el timeout limita cada llamada.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy()
    {
        return Policy.WrapAsync(
            GetRetryPolicy(),
            GetCircuitBreakerPolicy(),
            GetTimeoutPolicy());
    }

    private static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue("logger", out var logger))
            return logger as ILogger;
        return null;
    }
}
