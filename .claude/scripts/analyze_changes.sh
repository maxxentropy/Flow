#!/bin/bash
# Pre-commit analysis script for MCP Server changes

set -e

echo "🔍 Analyzing changes for MCP Server..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get list of changed files
CHANGED_FILES=$(git diff --cached --name-only)

if [ -z "$CHANGED_FILES" ]; then
    echo "No staged changes to analyze"
    exit 0
fi

echo "📋 Changed files:"
echo "$CHANGED_FILES" | sed 's/^/  - /'

# Check for breaking changes in protocol interfaces
echo -e "\n🔍 Checking protocol interfaces..."
PROTOCOL_CHANGES=$(echo "$CHANGED_FILES" | grep -E "(Protocol|Message|Transport)" || true)
if [ ! -z "$PROTOCOL_CHANGES" ]; then
    echo -e "${YELLOW}⚠️  Protocol changes detected - ensure backward compatibility${NC}"
    echo "$PROTOCOL_CHANGES" | sed 's/^/  - /'
fi

# Analyze complexity of C# files
echo -e "\n📊 Analyzing code complexity..."
for file in $CHANGED_FILES; do
    if [[ $file == *.cs ]]; then
        # Count methods and check for large files
        if [ -f "$file" ]; then
            LINE_COUNT=$(wc -l < "$file")
            METHOD_COUNT=$(grep -c "public\|private\|protected\|internal" "$file" || true)
            
            if [ $LINE_COUNT -gt 500 ]; then
                echo -e "${YELLOW}⚠️  $file has $LINE_COUNT lines (consider splitting)${NC}"
            fi
            
            if [ $METHOD_COUNT -gt 20 ]; then
                echo -e "${YELLOW}⚠️  $file has $METHOD_COUNT methods (consider refactoring)${NC}"
            fi
        fi
    fi
done

# Check for missing tests
echo -e "\n🧪 Checking test coverage..."
for file in $CHANGED_FILES; do
    if [[ $file == *.cs ]] && [[ $file != *.Tests.cs ]] && [[ $file != *Test.cs ]]; then
        # Extract just the filename without path and extension
        BASE_NAME=$(basename "$file" .cs)
        TEST_FILE=$(find . -name "${BASE_NAME}Tests.cs" -o -name "${BASE_NAME}Test.cs" | head -1)
        
        if [ -z "$TEST_FILE" ]; then
            echo -e "${RED}❌ No test file found for $file${NC}"
        fi
    fi
done

# Check for TODOs and FIXMEs
echo -e "\n📝 Checking for TODOs and FIXMEs..."
TODO_COUNT=0
for file in $CHANGED_FILES; do
    if [ -f "$file" ]; then
        FILE_TODOS=$(grep -n "TODO\|FIXME\|HACK" "$file" 2>/dev/null || true)
        if [ ! -z "$FILE_TODOS" ]; then
            echo -e "${YELLOW}Found in $file:${NC}"
            echo "$FILE_TODOS" | sed 's/^/  /'
            TODO_COUNT=$((TODO_COUNT + $(echo "$FILE_TODOS" | wc -l)))
        fi
    fi
done

# Check dependency changes
if echo "$CHANGED_FILES" | grep -q "\.csproj$"; then
    echo -e "\n📦 Project file changes detected - remember to:"
    echo "  - Update package versions in all projects"
    echo "  - Run 'dotnet restore'"
    echo "  - Check for security vulnerabilities with 'dotnet list package --vulnerable'"
fi

# Summary
echo -e "\n📊 Summary:"
echo "  - Files changed: $(echo "$CHANGED_FILES" | wc -l)"
echo "  - TODOs found: $TODO_COUNT"

if [ $TODO_COUNT -gt 5 ]; then
    echo -e "${YELLOW}⚠️  Consider addressing some TODOs before committing${NC}"
fi

echo -e "\n${GREEN}✅ Analysis complete!${NC}"