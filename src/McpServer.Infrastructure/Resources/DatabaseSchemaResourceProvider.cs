using System.Text;
using System.Text.Json;
using McpServer.Application.Resources;
using McpServer.Domain.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace McpServer.Infrastructure.Resources;

/// <summary>
/// Configuration for the database schema resource provider.
/// </summary>
public class DatabaseSchemaResourceOptions
{
    /// <summary>
    /// Gets or sets the available database names.
    /// </summary>
    public List<string> Databases { get; set; } = new();
    
    /// <summary>
    /// Gets or sets whether to include system tables.
    /// </summary>
    public bool IncludeSystemTables { get; set; } = false;
}

/// <summary>
/// Provides database schema information as resources using templates.
/// </summary>
public class DatabaseSchemaResourceProvider : TemplateResourceProvider
{
    private readonly DatabaseSchemaResourceOptions _options;
    private readonly ILogger<DatabaseSchemaResourceProvider> _logger;
    
    // Mock data for demonstration
    private readonly Dictionary<string, List<TableInfo>> _databaseSchemas = new()
    {
        ["customers"] = new List<TableInfo>
        {
            new("users", new[] { "id", "name", "email", "created_at" }),
            new("orders", new[] { "id", "user_id", "total", "status", "created_at" }),
            new("products", new[] { "id", "name", "price", "stock" })
        },
        ["analytics"] = new List<TableInfo>
        {
            new("events", new[] { "id", "event_type", "user_id", "timestamp", "properties" }),
            new("metrics", new[] { "id", "metric_name", "value", "timestamp" })
        }
    };
    
    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseSchemaResourceProvider"/> class.
    /// </summary>
    public DatabaseSchemaResourceProvider(
        ILogger<DatabaseSchemaResourceProvider> logger,
        IOptions<DatabaseSchemaResourceOptions> options) : base(logger)
    {
        _logger = logger;
        _options = options.Value;
        
        // Register templates
        RegisterTemplate(new ResourceTemplate(
            "db://{database}/schema",
            "Database Schema",
            "Complete schema for a database",
            "application/json"
        ));
        
        RegisterTemplate(new ResourceTemplate(
            "db://{database}/tables/{table}",
            "Table Schema",
            "Schema for a specific table",
            "application/json"
        ));
        
        RegisterTemplate(new ResourceTemplate(
            "db://{database}/tables/{table}/columns/{column}",
            "Column Details",
            "Details for a specific column",
            "application/json"
        ));
    }
    
    /// <inheritdoc/>
    public override async Task<IEnumerable<TemplateResourceInstance>> ListTemplateInstancesAsync(CancellationToken cancellationToken = default)
    {
        var instances = new List<TemplateResourceInstance>();
        
        var databases = _options.Databases.Any() ? _options.Databases : _databaseSchemas.Keys.ToList();
        
        foreach (var database in databases)
        {
            // Database schema instances
            instances.Add(new TemplateResourceInstance
            {
                Template = Templates.First(t => t.Name == "Database Schema"),
                Parameters = new Dictionary<string, string> { ["database"] = database },
                DisplayName = $"{database} Database Schema"
            });
            
            // Table schema instances
            if (_databaseSchemas.TryGetValue(database, out var tables))
            {
                foreach (var table in tables)
                {
                    instances.Add(new TemplateResourceInstance
                    {
                        Template = Templates.First(t => t.Name == "Table Schema"),
                        Parameters = new Dictionary<string, string> 
                        { 
                            ["database"] = database,
                            ["table"] = table.Name
                        },
                        DisplayName = $"{database}.{table.Name} Table"
                    });
                    
                    // Column detail instances
                    foreach (var column in table.Columns)
                    {
                        instances.Add(new TemplateResourceInstance
                        {
                            Template = Templates.First(t => t.Name == "Column Details"),
                            Parameters = new Dictionary<string, string>
                            {
                                ["database"] = database,
                                ["table"] = table.Name,
                                ["column"] = column
                            },
                            DisplayName = $"{database}.{table.Name}.{column}"
                        });
                    }
                }
            }
        }
        
        return await Task.FromResult(instances);
    }
    
    /// <inheritdoc/>
    public override async Task<ResourceContent> ReadTemplateResourceAsync(
        IResourceTemplate template,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        switch (template.Name)
        {
            case "Database Schema":
                return await ReadDatabaseSchemaAsync(parameters["database"], cancellationToken);
                
            case "Table Schema":
                return await ReadTableSchemaAsync(
                    parameters["database"], 
                    parameters["table"], 
                    cancellationToken);
                    
            case "Column Details":
                return await ReadColumnDetailsAsync(
                    parameters["database"],
                    parameters["table"],
                    parameters["column"],
                    cancellationToken);
                    
            default:
                throw new NotSupportedException($"Template {template.Name} is not supported");
        }
    }
    
    private async Task<ResourceContent> ReadDatabaseSchemaAsync(string database, CancellationToken cancellationToken)
    {
        if (!_databaseSchemas.TryGetValue(database, out var tables))
        {
            throw new ResourceNotFoundException($"Database '{database}' not found");
        }
        
        var schema = new
        {
            database = database,
            tables = tables.Select(t => new
            {
                name = t.Name,
                columns = t.Columns,
                columnCount = t.Columns.Length
            })
        };
        
        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = $"db://{database}/schema",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private async Task<ResourceContent> ReadTableSchemaAsync(string database, string table, CancellationToken cancellationToken)
    {
        if (!_databaseSchemas.TryGetValue(database, out var tables))
        {
            throw new ResourceNotFoundException($"Database '{database}' not found");
        }
        
        var tableInfo = tables.FirstOrDefault(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase));
        if (tableInfo == null)
        {
            throw new ResourceNotFoundException($"Table '{table}' not found in database '{database}'");
        }
        
        var schema = new
        {
            database = database,
            table = tableInfo.Name,
            columns = tableInfo.Columns.Select((col, idx) => new
            {
                name = col,
                position = idx + 1,
                // Mock additional metadata
                type = GetMockColumnType(col),
                nullable = !col.Equals("id", StringComparison.OrdinalIgnoreCase)
            })
        };
        
        var json = JsonSerializer.Serialize(schema, new JsonSerializerOptions { WriteIndented = true });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = $"db://{database}/tables/{table}",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private async Task<ResourceContent> ReadColumnDetailsAsync(
        string database, 
        string table, 
        string column, 
        CancellationToken cancellationToken)
    {
        if (!_databaseSchemas.TryGetValue(database, out var tables))
        {
            throw new ResourceNotFoundException($"Database '{database}' not found");
        }
        
        var tableInfo = tables.FirstOrDefault(t => t.Name.Equals(table, StringComparison.OrdinalIgnoreCase));
        if (tableInfo == null)
        {
            throw new ResourceNotFoundException($"Table '{table}' not found in database '{database}'");
        }
        
        if (!tableInfo.Columns.Any(c => c.Equals(column, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ResourceNotFoundException($"Column '{column}' not found in table '{database}.{table}'");
        }
        
        var details = new
        {
            database = database,
            table = table,
            column = column,
            type = GetMockColumnType(column),
            nullable = !column.Equals("id", StringComparison.OrdinalIgnoreCase),
            isPrimaryKey = column.Equals("id", StringComparison.OrdinalIgnoreCase),
            isForeignKey = column.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && !column.Equals("id", StringComparison.OrdinalIgnoreCase),
            defaultValue = column.Equals("created_at", StringComparison.OrdinalIgnoreCase) ? "CURRENT_TIMESTAMP" : null,
            constraints = GetMockConstraints(column)
        };
        
        var json = JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
        
        return await Task.FromResult(new ResourceContent
        {
            Uri = $"db://{database}/tables/{table}/columns/{column}",
            MimeType = "application/json",
            Text = json
        });
    }
    
    private static string GetMockColumnType(string columnName)
    {
        return columnName.ToLowerInvariant() switch
        {
            "id" => "INTEGER",
            var n when n.EndsWith("_id") => "INTEGER",
            "name" or "email" or "status" or "event_type" or "metric_name" => "VARCHAR(255)",
            "price" or "total" or "value" => "DECIMAL(10,2)",
            "stock" => "INTEGER",
            "created_at" or "timestamp" => "TIMESTAMP",
            "properties" => "JSON",
            _ => "VARCHAR(255)"
        };
    }
    
    private static List<string> GetMockConstraints(string columnName)
    {
        var constraints = new List<string>();
        
        if (columnName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            constraints.Add("PRIMARY KEY");
            constraints.Add("AUTO_INCREMENT");
        }
        else if (columnName.Equals("email", StringComparison.OrdinalIgnoreCase))
        {
            constraints.Add("UNIQUE");
        }
        else if (columnName.EndsWith("_id", StringComparison.OrdinalIgnoreCase) && 
                 !columnName.Equals("id", StringComparison.OrdinalIgnoreCase))
        {
            constraints.Add("FOREIGN KEY");
        }
        
        return constraints;
    }
    
    private record TableInfo(string Name, string[] Columns);
}