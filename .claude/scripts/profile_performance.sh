#!/bin/bash
# Performance profiling script for MCP Server

set -e

echo "🚀 Performance Profiling for MCP Server"

# Configuration
DURATION=${1:-30}
OUTPUT_DIR="performance_reports/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$OUTPUT_DIR"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo "📊 Running performance profile for ${DURATION} seconds..."
echo "📁 Results will be saved to: $OUTPUT_DIR"

# Function to check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# CPU Profiling
if command_exists dotnet-trace; then
    echo -e "\n🔧 Collecting CPU profile..."
    dotnet-trace collect \
        --process-name McpServer.Web \
        --profile cpu-sampling \
        --duration 00:00:${DURATION} \
        --output "$OUTPUT_DIR/cpu_trace.nettrace" &
    CPU_PID=$!
else
    echo -e "${YELLOW}⚠️  dotnet-trace not found. Install with: dotnet tool install -g dotnet-trace${NC}"
fi

# Memory Profiling
if command_exists dotnet-gcdump; then
    echo -e "\n💾 Memory snapshot will be taken at the end..."
fi

# Performance Counters
echo -e "\n📈 Collecting performance counters..."
cat > "$OUTPUT_DIR/collect_metrics.sh" << 'EOF'
#!/bin/bash
OUTPUT_FILE=$1
DURATION=$2

echo "timestamp,cpu_percent,memory_mb,handles,threads,gen0_gc,gen1_gc,gen2_gc" > "$OUTPUT_FILE"

END_TIME=$(($(date +%s) + $DURATION))

while [ $(date +%s) -lt $END_TIME ]; do
    if pgrep -f "McpServer.Web" > /dev/null; then
        PID=$(pgrep -f "McpServer.Web" | head -1)
        
        # Get process stats (macOS compatible)
        CPU=$(ps -p $PID -o %cpu | tail -1 | tr -d ' ')
        MEM=$(ps -p $PID -o rss | tail -1 | awk '{print $1/1024}')
        
        # Get .NET counters if available
        if command -v dotnet-counters >/dev/null 2>&1; then
            # This would need to be adapted for actual counter collection
            GC_STATS="0,0,0"
        else
            GC_STATS="0,0,0"
        fi
        
        echo "$(date +%s),$CPU,$MEM,0,0,$GC_STATS" >> "$OUTPUT_FILE"
    fi
    sleep 1
done
EOF

chmod +x "$OUTPUT_DIR/collect_metrics.sh"
"$OUTPUT_DIR/collect_metrics.sh" "$OUTPUT_DIR/metrics.csv" $DURATION &
METRICS_PID=$!

# Run load test
echo -e "\n🔨 Starting load test..."
cat > "$OUTPUT_DIR/load_test.sh" << 'EOF'
#!/bin/bash
BASE_URL="http://localhost:8080"
DURATION=$1
OUTPUT_DIR=$2

# Simple load test using curl
echo "Starting load test for $DURATION seconds..."

END_TIME=$(($(date +%s) + $DURATION))
REQUEST_COUNT=0
ERROR_COUNT=0

# Log file for responses
RESPONSE_LOG="$OUTPUT_DIR/responses.log"
ERROR_LOG="$OUTPUT_DIR/errors.log"

while [ $(date +%s) -lt $END_TIME ]; do
    # SSE connection test
    START_TIME=$(date +%s.%N)
    
    curl -s -X POST "$BASE_URL/sse" \
        -H "Content-Type: text/event-stream" \
        -H "Accept: text/event-stream" \
        -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' \
        --max-time 5 \
        -o "$RESPONSE_LOG" 2>> "$ERROR_LOG"
    
    EXIT_CODE=$?
    END_TIME_REQ=$(date +%s.%N)
    DURATION=$(echo "$END_TIME_REQ - $START_TIME" | bc)
    
    if [ $EXIT_CODE -eq 0 ]; then
        echo "$(date +%s),$DURATION,success" >> "$OUTPUT_DIR/request_times.csv"
    else
        echo "$(date +%s),$DURATION,error,$EXIT_CODE" >> "$OUTPUT_DIR/request_times.csv"
        ERROR_COUNT=$((ERROR_COUNT + 1))
    fi
    
    REQUEST_COUNT=$((REQUEST_COUNT + 1))
    
    # Small delay to avoid overwhelming
    sleep 0.1
done

echo "Load test complete: $REQUEST_COUNT requests, $ERROR_COUNT errors"
EOF

chmod +x "$OUTPUT_DIR/load_test.sh"

# Only run load test if server is running
if pgrep -f "McpServer.Web" > /dev/null; then
    "$OUTPUT_DIR/load_test.sh" $DURATION "$OUTPUT_DIR" &
    LOAD_PID=$!
else
    echo -e "${YELLOW}⚠️  McpServer.Web not running. Skipping load test.${NC}"
fi

# Wait for all background jobs
echo -e "\n⏳ Waiting for profiling to complete..."
wait $METRICS_PID 2>/dev/null || true
[ ! -z "$CPU_PID" ] && wait $CPU_PID 2>/dev/null || true
[ ! -z "$LOAD_PID" ] && wait $LOAD_PID 2>/dev/null || true

# Take memory dump at the end
if command_exists dotnet-gcdump && pgrep -f "McpServer.Web" > /dev/null; then
    echo -e "\n💾 Taking memory snapshot..."
    PID=$(pgrep -f "McpServer.Web" | head -1)
    dotnet-gcdump collect -p $PID -o "$OUTPUT_DIR/memory_dump.gcdump" 2>/dev/null || \
        echo -e "${YELLOW}Failed to collect memory dump${NC}"
fi

# Generate analysis report
echo -e "\n📊 Generating analysis report..."
cat > "$OUTPUT_DIR/analysis_report.md" << EOF
# Performance Profile Report
Generated: $(date)
Duration: ${DURATION} seconds

## Summary

### CPU Profile
- Trace file: cpu_trace.nettrace
- View with: \`dotnet-trace report cpu_trace.nettrace\`

### Memory Analysis
- Dump file: memory_dump.gcdump
- View with: \`dotnet-gcdump report memory_dump.gcdump\`

### Performance Metrics
\`\`\`
$(if [ -f "$OUTPUT_DIR/metrics.csv" ]; then
    echo "Average CPU: $(awk -F, 'NR>1 {sum+=$2; count++} END {print sum/count"%"}' "$OUTPUT_DIR/metrics.csv")"
    echo "Average Memory: $(awk -F, 'NR>1 {sum+=$3; count++} END {print int(sum/count)"MB"}' "$OUTPUT_DIR/metrics.csv")"
fi)
\`\`\`

### Load Test Results
\`\`\`
$(if [ -f "$OUTPUT_DIR/request_times.csv" ]; then
    TOTAL=$(wc -l < "$OUTPUT_DIR/request_times.csv")
    ERRORS=$(grep -c "error" "$OUTPUT_DIR/request_times.csv" || echo "0")
    SUCCESS=$((TOTAL - ERRORS))
    echo "Total Requests: $TOTAL"
    echo "Successful: $SUCCESS"
    echo "Errors: $ERRORS"
    echo "Success Rate: $(awk "BEGIN {print ($SUCCESS/$TOTAL)*100}")%"
    
    if [ $SUCCESS -gt 0 ]; then
        AVG_TIME=$(awk -F, '$3=="success" {sum+=$2; count++} END {print sum/count}' "$OUTPUT_DIR/request_times.csv")
        echo "Average Response Time: ${AVG_TIME}s"
    fi
fi)
\`\`\`

## Recommendations

Based on the profiling results:

1. **CPU Optimization**
   - Check cpu_trace.nettrace for hot paths
   - Look for excessive allocations in tight loops

2. **Memory Optimization**
   - Analyze memory_dump.gcdump for large objects
   - Check for memory leaks or retained references

3. **Response Time**
   - Review request_times.csv for outliers
   - Investigate slow requests in errors.log

## Next Steps

1. Open trace in Visual Studio or PerfView:
   \`\`\`bash
   dotnet-trace report $OUTPUT_DIR/cpu_trace.nettrace
   \`\`\`

2. Analyze memory dump:
   \`\`\`bash
   dotnet-gcdump report $OUTPUT_DIR/memory_dump.gcdump
   \`\`\`

3. Visualize metrics:
   \`\`\`bash
   python3 -m http.server --directory $OUTPUT_DIR 8000
   # Then open http://localhost:8000/metrics.csv
   \`\`\`
EOF

# Create visualization script
cat > "$OUTPUT_DIR/visualize.py" << 'EOF'
#!/usr/bin/env python3
import pandas as pd
import matplotlib.pyplot as plt
import sys

if len(sys.argv) < 2:
    print("Usage: python visualize.py <output_dir>")
    sys.exit(1)

output_dir = sys.argv[1]

# Load metrics
try:
    metrics = pd.read_csv(f"{output_dir}/metrics.csv")
    metrics['timestamp'] = pd.to_datetime(metrics['timestamp'], unit='s')
    
    fig, axes = plt.subplots(2, 2, figsize=(12, 8))
    
    # CPU usage
    axes[0, 0].plot(metrics['timestamp'], metrics['cpu_percent'])
    axes[0, 0].set_title('CPU Usage %')
    axes[0, 0].set_xlabel('Time')
    
    # Memory usage
    axes[0, 1].plot(metrics['timestamp'], metrics['memory_mb'])
    axes[0, 1].set_title('Memory Usage (MB)')
    axes[0, 1].set_xlabel('Time')
    
    # Request times
    if os.path.exists(f"{output_dir}/request_times.csv"):
        requests = pd.read_csv(f"{output_dir}/request_times.csv", 
                               names=['timestamp', 'duration', 'status', 'error'])
        successful = requests[requests['status'] == 'success']
        
        axes[1, 0].hist(successful['duration'], bins=50)
        axes[1, 0].set_title('Response Time Distribution')
        axes[1, 0].set_xlabel('Duration (s)')
        
        # Success rate over time
        requests['timestamp'] = pd.to_datetime(requests['timestamp'], unit='s')
        requests['success'] = (requests['status'] == 'success').astype(int)
        rolling_success = requests.set_index('timestamp')['success'].rolling('5s').mean() * 100
        
        axes[1, 1].plot(rolling_success.index, rolling_success.values)
        axes[1, 1].set_title('Success Rate % (5s window)')
        axes[1, 1].set_xlabel('Time')
    
    plt.tight_layout()
    plt.savefig(f"{output_dir}/performance_charts.png")
    print(f"Charts saved to {output_dir}/performance_charts.png")
    
except Exception as e:
    print(f"Error generating charts: {e}")
EOF

chmod +x "$OUTPUT_DIR/visualize.py"

echo -e "\n${GREEN}✅ Performance profiling complete!${NC}"
echo -e "\n📁 Results saved to: $OUTPUT_DIR"
echo -e "\n📊 View the report: cat $OUTPUT_DIR/analysis_report.md"

# Instructions for viewing results
echo -e "\n🔍 To analyze results:"
echo "  1. CPU Profile: dotnet-trace report $OUTPUT_DIR/cpu_trace.nettrace"
echo "  2. Memory Dump: dotnet-gcdump report $OUTPUT_DIR/memory_dump.gcdump"
echo "  3. Visualize: python3 $OUTPUT_DIR/visualize.py $OUTPUT_DIR"