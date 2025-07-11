using System.Diagnostics;
using System.Text;
using System.Text.Json;
using McpServer.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpServer.Infrastructure.Tools;

/// <summary>
/// Analyzes code for architecture violations, security issues, and best practices.
/// This is a meta-tool that helps develop MCP servers!
/// </summary>
[Tool("code_analyzer")]
public class CodeAnalyzerTool : ITool
{
    private readonly ILogger<CodeAnalyzerTool> _logger;
    private readonly string _projectRoot;

    public CodeAnalyzerTool(ILogger<CodeAnalyzerTool> logger)
    {
        _logger = logger;
        _projectRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../.."));
    }

    public string Name => "code_analyzer";

    public string Description => "Analyzes code changes for architecture violations, security issues, and MCP protocol compliance";

    public ToolSchema Schema => new()
    {
        Type = "object",
        Properties = new Dictionary<string, object>
        {
            ["target"] = new 
            { 
                type = "string", 
                description = "What to analyze: 'staged' for git staged files, 'all' for entire codebase, or specific file path",
                @default = "staged"
            },
            ["checks"] = new 
            { 
                type = "array", 
                items = new 
                { 
                    type = "string", 
                    @enum = new[] { "architecture", "security", "performance", "protocol", "all" } 
                },
                description = "Types of checks to perform",
                @default = new[] { "all" }
            },
            ["format"] = new
            {
                type = "string",
                @enum = new[] { "summary", "detailed", "json" },
                description = "Output format",
                @default = "summary"
            }
        },
        Required = new[] { "target" }
    };

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running code analysis for {Target}", request.Arguments?["target"]);

        var target = request.Arguments?["target"]?.ToString() ?? "staged";
        var checks = DeserializeStringArray(request.Arguments?["checks"]) ?? new[] { "all" };
        var format = request.Arguments?["format"]?.ToString() ?? "summary";

        var results = new Dictionary<string, object>();
        var issues = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();

        try
        {
            // Get files to analyze
            var files = await GetFilesToAnalyze(target, cancellationToken);
            results["filesAnalyzed"] = files.Count;
            results["files"] = files.Select(Path.GetFileName).ToList();

            // Run architecture checks
            if (checks.Contains("all") || checks.Contains("architecture"))
            {
                var archResults = await CheckArchitecture(files, cancellationToken);
                issues.AddRange(archResults.Issues);
                warnings.AddRange(archResults.Warnings);
            }

            // Run security checks
            if (checks.Contains("all") || checks.Contains("security"))
            {
                var secResults = await CheckSecurity(files, cancellationToken);
                issues.AddRange(secResults.Issues);
                warnings.AddRange(secResults.Warnings);
            }

            // Run performance checks
            if (checks.Contains("all") || checks.Contains("performance"))
            {
                var perfResults = await CheckPerformance(files, cancellationToken);
                warnings.AddRange(perfResults.Warnings);
                suggestions.AddRange(perfResults.Suggestions);
            }

            // Run protocol compliance checks
            if (checks.Contains("all") || checks.Contains("protocol"))
            {
                var protocolResults = await CheckProtocolCompliance(files, cancellationToken);
                issues.AddRange(protocolResults.Issues);
                warnings.AddRange(protocolResults.Warnings);
            }

            // Format results
            results["issues"] = issues;
            results["warnings"] = warnings;
            results["suggestions"] = suggestions;
            results["summary"] = new
            {
                issueCount = issues.Count,
                warningCount = warnings.Count,
                suggestionCount = suggestions.Count,
                status = issues.Count > 0 ? "failed" : "passed"
            };

            var output = format switch
            {
                "json" => JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }),
                "detailed" => FormatDetailedOutput(results),
                _ => FormatSummaryOutput(results)
            };

            return new ToolResult
            {
                Content = new[]
                {
                    new ToolContent { Type = "text", Text = output }
                },
                IsSuccess = issues.Count == 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code analysis failed");
            return new ToolResult
            {
                Content = new[]
                {
                    new ToolContent { Type = "text", Text = $"Analysis failed: {ex.Message}" }
                },
                IsError = true
            };
        }
    }

    private async Task<List<string>> GetFilesToAnalyze(string target, CancellationToken cancellationToken)
    {
        if (target == "staged")
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "diff --cached --name-only",
                    WorkingDirectory = _projectRoot,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync(cancellationToken);

            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(f => Path.Combine(_projectRoot, f))
                .Where(f => f.EndsWith(".cs"))
                .ToList();
        }
        else if (target == "all")
        {
            return Directory.GetFiles(_projectRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/"))
                .ToList();
        }
        else
        {
            var path = Path.Combine(_projectRoot, target);
            return File.Exists(path) ? new List<string> { path } : new List<string>();
        }
    }

    private async Task<(List<string> Issues, List<string> Warnings)> CheckArchitecture(List<string> files, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(_projectRoot, file);

            // Check Clean Architecture violations
            if (relativePath.Contains("/Domain/") && content.Contains("using McpServer.Infrastructure"))
            {
                issues.Add($"Architecture violation in {relativePath}: Domain layer references Infrastructure");
            }

            if (relativePath.Contains("/Application/") && content.Contains("using McpServer.Web"))
            {
                issues.Add($"Architecture violation in {relativePath}: Application layer references Presentation");
            }

            // Check for large classes
            var lineCount = content.Split('\n').Length;
            if (lineCount > 500)
            {
                warnings.Add($"Large class in {relativePath}: {lineCount} lines (consider refactoring)");
            }
        }

        return (issues, warnings);
    }

    private async Task<(List<string> Issues, List<string> Warnings)> CheckSecurity(List<string> files, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(_projectRoot, file);

            // Check for hardcoded secrets
            if (content.Contains("password") && content.Contains("=") && content.Contains("\""))
            {
                warnings.Add($"Potential hardcoded password in {relativePath}");
            }

            // Check for SQL injection risks
            if (content.Contains("string.Format") && content.Contains("SELECT"))
            {
                issues.Add($"Potential SQL injection risk in {relativePath}: String concatenation in query");
            }

            // Check for missing input validation
            if (content.Contains("public async Task") && content.Contains("Request") && !content.Contains("Validate"))
            {
                warnings.Add($"Missing validation in {relativePath}: Request handler without validation");
            }
        }

        return (issues, warnings);
    }

    private async Task<(List<string> Warnings, List<string> Suggestions)> CheckPerformance(List<string> files, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        var suggestions = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(_projectRoot, file);

            // Check for async void
            if (content.Contains("async void") && !content.Contains("EventHandler"))
            {
                warnings.Add($"Performance issue in {relativePath}: async void method (use async Task)");
            }

            // Check for .Result or .Wait()
            if (content.Contains(".Result") || content.Contains(".Wait()"))
            {
                warnings.Add($"Performance issue in {relativePath}: Blocking async call (.Result or .Wait())");
            }

            // Check for missing ConfigureAwait
            if (relativePath.Contains("/Infrastructure/") && content.Contains("await") && !content.Contains("ConfigureAwait"))
            {
                suggestions.Add($"Performance suggestion for {relativePath}: Consider using ConfigureAwait(false)");
            }
        }

        return (warnings, suggestions);
    }

    private async Task<(List<string> Issues, List<string> Warnings)> CheckProtocolCompliance(List<string> files, CancellationToken cancellationToken)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            var relativePath = Path.GetRelativePath(_projectRoot, file);

            // Check for protocol message compliance
            if (content.Contains("JsonRpcRequest") && !content.Contains("jsonrpc"))
            {
                warnings.Add($"Protocol warning in {relativePath}: JSON-RPC request without version field");
            }

            // Check error code ranges
            if (content.Contains("ErrorCode") && content.Contains("-32"))
            {
                // MCP uses specific error code ranges
                suggestions.Add($"Protocol check in {relativePath}: Verify error codes follow MCP specification");
            }
        }

        return (issues, warnings);
    }

    private string FormatSummaryOutput(Dictionary<string, object> results)
    {
        var sb = new StringBuilder();
        var summary = (dynamic)results["summary"];
        
        sb.AppendLine($"🔍 Code Analysis Results");
        sb.AppendLine($"Files analyzed: {results["filesAnalyzed"]}");
        sb.AppendLine($"Status: {(summary.status == "passed" ? "✅ PASSED" : "❌ FAILED")}");
        sb.AppendLine();
        
        if (summary.issueCount > 0)
            sb.AppendLine($"❌ Issues: {summary.issueCount}");
        if (summary.warningCount > 0)
            sb.AppendLine($"⚠️  Warnings: {summary.warningCount}");
        if (summary.suggestionCount > 0)
            sb.AppendLine($"💡 Suggestions: {summary.suggestionCount}");

        return sb.ToString();
    }

    private string FormatDetailedOutput(Dictionary<string, object> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine(FormatSummaryOutput(results));
        sb.AppendLine();

        var issues = (List<string>)results["issues"];
        var warnings = (List<string>)results["warnings"];
        var suggestions = (List<string>)results["suggestions"];

        if (issues.Any())
        {
            sb.AppendLine("❌ ISSUES:");
            foreach (var issue in issues)
                sb.AppendLine($"  - {issue}");
            sb.AppendLine();
        }

        if (warnings.Any())
        {
            sb.AppendLine("⚠️  WARNINGS:");
            foreach (var warning in warnings)
                sb.AppendLine($"  - {warning}");
            sb.AppendLine();
        }

        if (suggestions.Any())
        {
            sb.AppendLine("💡 SUGGESTIONS:");
            foreach (var suggestion in suggestions)
                sb.AppendLine($"  - {suggestion}");
        }

        return sb.ToString();
    }

    private string[]? DeserializeStringArray(object? value)
    {
        if (value == null) return null;
        
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => s != null)
                .Cast<string>()
                .ToArray();
        }

        return value as string[];
    }
}