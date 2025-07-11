# .claude Directory Structure

This directory contains Claude-specific configuration and optimization files for the Flow MCP Server project.

## Purpose

The `.claude` directory serves as a centralized location for:
- Additional context files that Claude should consider
- Project-specific templates and patterns
- Extended documentation that supplements the main CLAUDE.md file
- Custom rules and guidelines specific to certain components

## Directory Structure

```
.claude/
├── README.md                    # This file
├── patterns/                    # Reusable code patterns and templates
│   ├── tool-template.cs        # Template for new tool implementations
│   ├── resource-provider.cs    # Template for resource providers
│   └── test-template.cs        # Template for test classes
├── examples/                    # Example implementations
│   ├── complex-tool.cs         # Example of complex tool with validation
│   ├── sse-handler.cs          # Example SSE request handling
│   └── integration-test.cs     # Example integration test setup
└── context/                     # Additional context files
    ├── api-decisions.md        # Architectural decision records
    ├── troubleshooting.md      # Known issues and solutions
    └── performance-notes.md    # Performance optimization guidelines
```

## Usage

### For Developers

1. **Reference Templates**: When asking Claude to create new components, reference the templates:
   ```
   "Create a new tool using the pattern in .claude/patterns/tool-template.cs"
   ```

2. **Check Examples**: Before implementing complex features, review examples:
   ```
   "Show me the complex-tool.cs example before I implement my validation logic"
   ```

3. **Consult Context**: For architectural decisions or troubleshooting:
   ```
   "Check the api-decisions.md for our reasoning on transport selection"
   ```

### For Claude

Claude automatically has access to this directory structure. When working on the project:
- Templates provide consistent patterns for new code
- Examples demonstrate best practices
- Context files provide additional project knowledge

## Adding New Files

When adding files to this directory:

1. **Templates** (`patterns/`): Add reusable patterns that enforce consistency
2. **Examples** (`examples/`): Add when you've created a particularly good implementation
3. **Context** (`context/`): Add when documenting decisions or gathering tribal knowledge

## File Naming Conventions

- Use lowercase with hyphens: `tool-template.cs`
- Be descriptive: `sse-transport-example.cs` not `example1.cs`
- Include file type in name: `validation-pattern.cs`

## Maintenance

- Review and update templates when patterns evolve
- Keep examples current with the latest code standards
- Archive outdated context files rather than deleting them
- Document the reasoning for any significant changes

## Integration with CLAUDE.md

This directory supplements but doesn't replace CLAUDE.md:
- CLAUDE.md: High-level architecture and standards
- .claude/: Specific implementations and patterns

Together, they provide Claude with comprehensive project context.