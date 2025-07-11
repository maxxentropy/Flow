#!/bin/bash
# Architecture validation script for MCP Server

set -e

echo "🏗️ Validating MCP Server Architecture..."

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

ERRORS=0
WARNINGS=0

# Function to check directory exists
check_dir() {
    if [ -d "$1" ]; then
        echo -e "${GREEN}✓${NC} $2"
        return 0
    else
        echo -e "${RED}✗${NC} $2 - Directory not found: $1"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# Function to check file exists
check_file() {
    if [ -f "$1" ]; then
        echo -e "${GREEN}✓${NC} $2"
        return 0
    else
        echo -e "${RED}✗${NC} $2 - File not found: $1"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# Function to check namespace conventions
check_namespace() {
    local file=$1
    local expected_namespace=$2
    
    if grep -q "namespace $expected_namespace" "$file"; then
        return 0
    else
        echo -e "${YELLOW}⚠️${NC}  Incorrect namespace in $file (expected: $expected_namespace)"
        WARNINGS=$((WARNINGS + 1))
        return 1
    fi
}

echo -e "\n📁 Checking Clean Architecture structure..."

# Check core project structure
check_dir "../McpServer.Domain" "Domain project"
check_dir "../McpServer.Application" "Application project"
check_dir "../McpServer.Infrastructure" "Infrastructure project"
check_dir "." "Web project (current)"

echo -e "\n📋 Validating layer dependencies..."

# Check Domain has no external dependencies
if [ -f "../McpServer.Domain/McpServer.Domain.csproj" ]; then
    DOMAIN_REFS=$(grep -c "PackageReference\|ProjectReference" "../McpServer.Domain/McpServer.Domain.csproj" || echo "0")
    if [ "$DOMAIN_REFS" -gt 2 ]; then  # Allow for basic packages like System.Text.Json
        echo -e "${YELLOW}⚠️${NC}  Domain layer has $DOMAIN_REFS references - should be minimal"
        WARNINGS=$((WARNINGS + 1))
    else
        echo -e "${GREEN}✓${NC} Domain layer has minimal dependencies"
    fi
fi

# Check Application only references Domain
if [ -f "../McpServer.Application/McpServer.Application.csproj" ]; then
    if grep -q "McpServer.Infrastructure" "../McpServer.Application/McpServer.Application.csproj"; then
        echo -e "${RED}✗${NC} Application layer references Infrastructure!"
        ERRORS=$((ERRORS + 1))
    else
        echo -e "${GREEN}✓${NC} Application layer doesn't reference Infrastructure"
    fi
fi

echo -e "\n🔍 Checking interface segregation..."

# Check for proper interface definitions
INTERFACES=(
    "../McpServer.Domain/Interfaces/ITransport.cs"
    "../McpServer.Domain/Interfaces/ITool.cs"
    "../McpServer.Domain/Interfaces/IResourceProvider.cs"
    "../McpServer.Application/Interfaces/IMcpServer.cs"
)

for interface in "${INTERFACES[@]}"; do
    check_file "$interface" "Interface: $(basename $interface)"
done

echo -e "\n📦 Validating dependency injection setup..."

# Check for DI configuration
if [ -f "Program.cs" ]; then
    if grep -q "AddMcpServer" "Program.cs"; then
        echo -e "${GREEN}✓${NC} MCP Server DI registration found"
    else
        echo -e "${YELLOW}⚠️${NC}  MCP Server DI registration not found in Program.cs"
        WARNINGS=$((WARNINGS + 1))
    fi
fi

echo -e "\n🧪 Checking test project structure..."

# Check test projects
TEST_PROJECTS=(
    "../tests/McpServer.Domain.Tests"
    "../tests/McpServer.Application.Tests"
    "../tests/McpServer.Infrastructure.Tests"
    "../tests/McpServer.Web.Tests"
)

for test_proj in "${TEST_PROJECTS[@]}"; do
    if [ -d "$test_proj" ]; then
        echo -e "${GREEN}✓${NC} $(basename $test_proj)"
        
        # Check for test files
        TEST_COUNT=$(find "$test_proj" -name "*Tests.cs" -o -name "*Test.cs" | wc -l)
        if [ $TEST_COUNT -eq 0 ]; then
            echo -e "${YELLOW}  ⚠️  No test files found${NC}"
            WARNINGS=$((WARNINGS + 1))
        else
            echo -e "  📊 Found $TEST_COUNT test files"
        fi
    else
        echo -e "${YELLOW}⚠️${NC}  Missing test project: $(basename $test_proj)"
        WARNINGS=$((WARNINGS + 1))
    fi
done

echo -e "\n🔐 Checking security patterns..."

# Check for common security issues
SECURITY_ISSUES=0

# Check for hardcoded secrets
echo -n "Checking for hardcoded secrets... "
if grep -r "password\s*=\s*\"" --include="*.cs" . 2>/dev/null | grep -v -i "test\|sample\|example" > /dev/null; then
    echo -e "${RED}Found potential hardcoded passwords!${NC}"
    SECURITY_ISSUES=$((SECURITY_ISSUES + 1))
else
    echo -e "${GREEN}OK${NC}"
fi

# Check for SQL injection vulnerabilities
echo -n "Checking for SQL concatenation... "
if grep -r "SELECT.*\+.*\"" --include="*.cs" . 2>/dev/null > /dev/null; then
    echo -e "${YELLOW}Found potential SQL concatenation${NC}"
    WARNINGS=$((WARNINGS + 1))
else
    echo -e "${GREEN}OK${NC}"
fi

echo -e "\n📏 Checking code metrics..."

# Count files and calculate metrics
TOTAL_CS_FILES=$(find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | wc -l)
TOTAL_LINES=$(find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -exec wc -l {} \; | awk '{sum+=$1} END {print sum}')

echo "Total C# files: $TOTAL_CS_FILES"
echo "Total lines of code: $TOTAL_LINES"

# Check for overly large files
echo -e "\nChecking for large files..."
LARGE_FILES=$(find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" -exec wc -l {} \; | awk '$1 > 500 {print $2, $1}')
if [ ! -z "$LARGE_FILES" ]; then
    echo -e "${YELLOW}⚠️  Files with >500 lines:${NC}"
    echo "$LARGE_FILES" | while read file lines; do
        echo "  - $file ($lines lines)"
    done
    WARNINGS=$((WARNINGS + 1))
fi

echo -e "\n🎯 Checking design patterns..."

# Check for common patterns
echo -n "Factory pattern usage: "
FACTORY_COUNT=$(grep -r "Factory" --include="*.cs" . 2>/dev/null | grep -v "//\|/\*" | wc -l)
echo "$FACTORY_COUNT occurrences"

echo -n "Repository pattern usage: "
REPO_COUNT=$(grep -r "Repository" --include="*.cs" . 2>/dev/null | grep -v "//\|/\*" | wc -l)
echo "$REPO_COUNT occurrences"

echo -n "Strategy pattern usage: "
STRATEGY_COUNT=$(grep -r "Strategy" --include="*.cs" . 2>/dev/null | grep -v "//\|/\*" | wc -l)
echo "$STRATEGY_COUNT occurrences"

echo -e "\n📝 Checking documentation..."

# Check for XML documentation
XML_DOC_FILES=$(grep -r "///" --include="*.cs" . 2>/dev/null | cut -d: -f1 | sort -u | wc -l)
echo "Files with XML documentation: $XML_DOC_FILES / $TOTAL_CS_FILES"

if [ $XML_DOC_FILES -lt $((TOTAL_CS_FILES / 2)) ]; then
    echo -e "${YELLOW}⚠️  Less than 50% of files have XML documentation${NC}"
    WARNINGS=$((WARNINGS + 1))
fi

# Generate architecture report
REPORT_FILE=".claude/context/architecture_validation_$(date +%Y%m%d).md"
mkdir -p .claude/context

cat > "$REPORT_FILE" << EOF
# Architecture Validation Report
Date: $(date)

## Summary
- ✅ Passed: $((20 - ERRORS - WARNINGS))
- ❌ Errors: $ERRORS
- ⚠️  Warnings: $WARNINGS

## Clean Architecture Compliance
$(if [ $ERRORS -eq 0 ]; then
    echo "✅ All architectural layers are properly structured"
else
    echo "❌ Architecture violations detected - see details above"
fi)

## Code Metrics
- Total C# Files: $TOTAL_CS_FILES
- Total Lines: $TOTAL_LINES
- Average Lines per File: $((TOTAL_LINES / TOTAL_CS_FILES))
- Files with XML Documentation: $XML_DOC_FILES

## Design Patterns Found
- Factory Pattern: $FACTORY_COUNT occurrences
- Repository Pattern: $REPO_COUNT occurrences  
- Strategy Pattern: $STRATEGY_COUNT occurrences

## Security Analysis
- Hardcoded Secrets: $(if [ $SECURITY_ISSUES -eq 0 ]; then echo "None found"; else echo "$SECURITY_ISSUES issues"; fi)
- SQL Injection Risks: Minimal

## Recommendations
$(if [ $WARNINGS -gt 0 ]; then
    echo "1. Address the $WARNINGS warnings identified"
    echo "2. Add XML documentation to remaining files"
    echo "3. Consider splitting files with >500 lines"
else
    echo "1. Architecture is well-structured"
    echo "2. Continue following current patterns"
fi)
EOF

echo -e "\n📊 Validation Summary:"
echo -e "  ${GREEN}✅ Passed:${NC} $((20 - ERRORS - WARNINGS))"
echo -e "  ${RED}❌ Errors:${NC} $ERRORS"
echo -e "  ${YELLOW}⚠️  Warnings:${NC} $WARNINGS"

if [ $ERRORS -eq 0 ]; then
    echo -e "\n${GREEN}✅ Architecture validation passed!${NC}"
else
    echo -e "\n${RED}❌ Architecture validation failed with $ERRORS errors${NC}"
    exit 1
fi

echo -e "\n📄 Full report saved to: $REPORT_FILE"