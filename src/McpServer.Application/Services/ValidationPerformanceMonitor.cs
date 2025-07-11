using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using McpServer.Domain.Validation;
using Microsoft.Extensions.Logging;
using DomainValidationResult = McpServer.Domain.Validation.ValidationResult;
using FluentValidationResult = FluentValidation.Results.ValidationResult;

namespace McpServer.Application.Services;

/// <summary>
/// Monitors and tracks validation performance metrics.
/// </summary>
public class ValidationPerformanceMonitor
{
    private readonly ILogger<ValidationPerformanceMonitor> _logger;
    private readonly Dictionary<string, ValidationMetrics> _metrics = new();
    private readonly object _lock = new();

    public ValidationPerformanceMonitor(ILogger<ValidationPerformanceMonitor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Executes validation with performance tracking.
    /// </summary>
    public DomainValidationResult ValidateWithMetrics<T>(
        IValidator<T> validator, 
        T instance, 
        string validationType)
    {
        var stopwatch = Stopwatch.StartNew();
        DomainValidationResult? result = null;
        
        try
        {
            var fluentResult = validator.Validate(instance);
            stopwatch.Stop();
            
            // Convert to domain validation result
            result = ConvertResult(fluentResult, validationType);
            
            // Track metrics
            TrackMetrics(validationType, stopwatch.ElapsedMilliseconds, result.IsValid);
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            TrackMetrics(validationType, stopwatch.ElapsedMilliseconds, false, true);
            
            _logger.LogError(ex, "Validation error for type {ValidationType}", validationType);
            return DomainValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets performance metrics for a specific validation type.
    /// </summary>
    public ValidationMetrics GetMetrics(string validationType)
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(validationType, out var metrics) 
                ? metrics 
                : new ValidationMetrics { ValidationType = validationType };
        }
    }

    /// <summary>
    /// Gets all performance metrics.
    /// </summary>
    public Dictionary<string, ValidationMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<string, ValidationMetrics>(_metrics);
        }
    }

    /// <summary>
    /// Resets all metrics.
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }

    private void TrackMetrics(string validationType, long elapsedMs, bool isValid, bool hasError = false)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(validationType, out var metrics))
            {
                metrics = new ValidationMetrics { ValidationType = validationType };
                _metrics[validationType] = metrics;
            }

            metrics.TotalValidations++;
            metrics.TotalTimeMs += elapsedMs;
            
            if (isValid)
                metrics.SuccessfulValidations++;
            else
                metrics.FailedValidations++;
                
            if (hasError)
                metrics.ErrorCount++;

            if (elapsedMs < metrics.MinTimeMs || metrics.MinTimeMs == 0)
                metrics.MinTimeMs = elapsedMs;
                
            if (elapsedMs > metrics.MaxTimeMs)
                metrics.MaxTimeMs = elapsedMs;

            // Log slow validations
            if (elapsedMs > 100)
            {
                _logger.LogWarning(
                    "Slow validation detected for {ValidationType}: {ElapsedMs}ms", 
                    validationType, 
                    elapsedMs);
            }
        }
    }

    private static DomainValidationResult ConvertResult(FluentValidationResult fluentResult, string validationType)
    {
        if (fluentResult.IsValid)
        {
            return DomainValidationResult.Success();
        }

        var errors = fluentResult.Errors.Select(error => new ValidationError
        {
            Message = error.ErrorMessage,
            Path = error.PropertyName,
            ErrorCode = error.ErrorCode,
            Context = new
            {
                AttemptedValue = error.AttemptedValue,
                Severity = error.Severity.ToString()
            },
            Severity = ConvertSeverity(error.Severity)
        }).ToList();

        return new DomainValidationResult
        {
            IsValid = false,
            Errors = errors,
            Context = new Dictionary<string, object>
            {
                ["validationType"] = validationType,
                ["errorCount"] = errors.Count
            }
        };
    }

    private static ValidationSeverity ConvertSeverity(Severity fluentSeverity)
    {
        return fluentSeverity switch
        {
            Severity.Error => ValidationSeverity.Error,
            Severity.Warning => ValidationSeverity.Warning,
            Severity.Info => ValidationSeverity.Warning,
            _ => ValidationSeverity.Error
        };
    }
}

/// <summary>
/// Validation performance metrics.
/// </summary>
public class ValidationMetrics
{
    public string ValidationType { get; init; } = string.Empty;
    public long TotalValidations { get; set; }
    public long SuccessfulValidations { get; set; }
    public long FailedValidations { get; set; }
    public long ErrorCount { get; set; }
    public long TotalTimeMs { get; set; }
    public long MinTimeMs { get; set; }
    public long MaxTimeMs { get; set; }
    
    public double AverageTimeMs => TotalValidations > 0 ? (double)TotalTimeMs / TotalValidations : 0;
    public double SuccessRate => TotalValidations > 0 ? (double)SuccessfulValidations / TotalValidations * 100 : 0;
    public double ErrorRate => TotalValidations > 0 ? (double)ErrorCount / TotalValidations * 100 : 0;
}

/// <summary>
/// Extension methods for validation with performance monitoring.
/// </summary>
public static class ValidationPerformanceExtensions
{
    /// <summary>
    /// Validates a JSON element with performance tracking.
    /// </summary>
    public static DomainValidationResult ValidateWithMetrics(
        this IValidator<JsonElement> validator,
        JsonElement instance,
        string validationType,
        ValidationPerformanceMonitor monitor)
    {
        return monitor.ValidateWithMetrics(validator, instance, validationType);
    }
}