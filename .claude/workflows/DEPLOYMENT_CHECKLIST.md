# Production Deployment Checklist

## 🚀 Pre-Deployment

### Code Readiness
- [ ] All tests passing on main branch
- [ ] No critical security alerts
- [ ] Performance benchmarks acceptable
- [ ] Documentation up to date
- [ ] CHANGELOG.md updated

### Configuration Check
```bash
# Validate all config files
for config in appsettings.*.json; do
    echo "Validating $config"
    jq . "$config" > /dev/null || echo "Invalid JSON in $config"
done

# Check for missing production settings
grep -r "TODO\|FIXME\|XXX" appsettings.Production.json

# Ensure no dev settings in prod
grep -i "debug\|development" appsettings.Production.json
```

### Security Audit
```bash
# Check for secrets
grep -r "password\|secret\|key" --include="*.json" --include="*.cs" | \
    grep -v "Test\|Mock\|Fake"

# Verify HTTPS enforcement
grep -A5 "UseHttpsRedirection" Program.cs

# Check CORS settings
grep -A10 "AddCors" Program.cs
```

## 📦 Build & Package

### Docker Build
```bash
# Build all images
docker build -f Dockerfile --target web -t mcpserver:web-latest .
docker build -f Dockerfile --target console -t mcpserver:console-latest .

# Verify images
docker run --rm mcpserver:web-latest --version
docker run --rm mcpserver:console-latest --version

# Security scan
docker scan mcpserver:web-latest
```

### Health Check Validation
```bash
# Test health endpoint
docker run -d -p 8080:8080 --name test-server mcpserver:web-latest
sleep 5
curl -f http://localhost:8080/health || echo "Health check failed"
docker stop test-server && docker rm test-server
```

### Package Artifacts
```bash
# Create release package
VERSION=$(git describe --tags --always)
mkdir -p releases/$VERSION

# Package binaries
dotnet publish src/McpServer.Web -c Release -o releases/$VERSION/web
dotnet publish src/McpServer.Console -c Release -o releases/$VERSION/console

# Create archives
tar -czf releases/mcpserver-web-$VERSION.tar.gz -C releases/$VERSION web
tar -czf releases/mcpserver-console-$VERSION.tar.gz -C releases/$VERSION console

# Generate checksums
sha256sum releases/*.tar.gz > releases/checksums.txt
```

## 🔧 Infrastructure Preparation

### Database Migrations (if applicable)
```sql
-- Backup current state
CREATE DATABASE mcpserver_backup_$(date +%Y%m%d);

-- Run migrations
dotnet ef database update --project src/McpServer.Infrastructure

-- Verify migration
SELECT * FROM __EFMigrationsHistory;
```

### Load Balancer Configuration
```nginx
upstream mcpserver {
    least_conn;
    server backend1:8080 max_fails=3 fail_timeout=30s;
    server backend2:8080 max_fails=3 fail_timeout=30s;
    
    keepalive 32;
}

server {
    listen 443 ssl http2;
    server_name mcp.example.com;
    
    location /sse {
        proxy_pass http://mcpserver;
        proxy_http_version 1.1;
        proxy_set_header Connection "";
        proxy_buffering off;
        proxy_cache off;
        
        # SSE specific
        proxy_set_header Content-Type text/event-stream;
        proxy_read_timeout 86400;
    }
    
    location /health {
        proxy_pass http://mcpserver;
        proxy_http_version 1.1;
        access_log off;
    }
}
```

### Monitoring Setup
```yaml
# Prometheus configuration
scrape_configs:
  - job_name: 'mcpserver'
    static_configs:
      - targets: ['mcpserver:8080']
    metrics_path: '/metrics'
    scrape_interval: 15s

# Alerts
groups:
  - name: mcpserver
    rules:
      - alert: McpServerDown
        expr: up{job="mcpserver"} == 0
        for: 5m
        
      - alert: HighErrorRate
        expr: rate(mcp_errors_total[5m]) > 0.05
        for: 10m
        
      - alert: HighMemoryUsage
        expr: process_resident_memory_bytes > 1e9
        for: 15m
```

## 🚢 Deployment Steps

### 1. Blue-Green Deployment
```bash
# Deploy to blue environment
kubectl apply -f k8s/deployment-blue.yaml
kubectl wait --for=condition=ready pod -l version=blue

# Run smoke tests
./scripts/smoke-test.sh https://mcp-blue.example.com

# Switch traffic
kubectl patch service mcpserver -p '{"spec":{"selector":{"version":"blue"}}}'

# Monitor for issues (5 minutes)
kubectl logs -f -l version=blue --tail=100

# If issues, rollback
kubectl patch service mcpserver -p '{"spec":{"selector":{"version":"green"}}}'
```

### 2. Rolling Update (Alternative)
```bash
# Update deployment
kubectl set image deployment/mcpserver \
    mcpserver=mcpserver:$VERSION \
    --record

# Monitor rollout
kubectl rollout status deployment/mcpserver

# If issues arise
kubectl rollout undo deployment/mcpserver
```

### 3. Canary Deployment (Alternative)
```bash
# Deploy canary (10% traffic)
kubectl apply -f k8s/deployment-canary.yaml

# Monitor metrics
watch -n 5 'kubectl top pods -l app=mcpserver'

# Gradually increase traffic
for percent in 25 50 75 100; do
    kubectl patch virtualservice mcpserver --type merge \
        -p "{\"spec\":{\"http\":[{\"weight\":$percent}]}}"
    sleep 300  # 5 minutes between increases
    
    # Check error rate
    ERROR_RATE=$(curl -s http://prometheus:9090/api/v1/query?query=mcp_error_rate | jq '.data.result[0].value[1]')
    if (( $(echo "$ERROR_RATE > 0.05" | bc -l) )); then
        echo "High error rate detected, rolling back"
        kubectl delete -f k8s/deployment-canary.yaml
        exit 1
    fi
done
```

## ✅ Post-Deployment Verification

### Functional Tests
```bash
# Test initialize
curl -X POST https://mcp.example.com/sse \
    -H "Content-Type: text/event-stream" \
    -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05"}}'

# Test tools listing
curl -X POST https://mcp.example.com/sse \
    -H "Content-Type: text/event-stream" \
    -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'

# Run full test suite
npm run test:e2e -- --env production
```

### Performance Validation
```bash
# Load test
k6 run --vus 100 --duration 5m scripts/load-test.js

# Expected results:
# - Response time p95 < 100ms
# - Error rate < 0.1%
# - No memory leaks
```

### Security Validation
```bash
# SSL/TLS check
nmap --script ssl-enum-ciphers -p 443 mcp.example.com

# Security headers
curl -I https://mcp.example.com | grep -i "strict-transport-security\|x-content-type\|x-frame"

# OWASP ZAP scan
docker run -t owasp/zap2docker-stable zap-baseline.py \
    -t https://mcp.example.com
```

## 📊 Monitoring Dashboard

### Key Metrics to Watch
1. **Request Rate**: Normal range 100-1000 req/s
2. **Error Rate**: Should be < 0.1%
3. **Response Time**: P95 < 100ms
4. **Memory Usage**: < 1GB per instance
5. **CPU Usage**: < 70% sustained
6. **Active Connections**: Monitor for leaks

### Alert Responses
```yaml
McpServerDown:
  - Check pod status: kubectl get pods
  - Check logs: kubectl logs -l app=mcpserver
  - Restart if needed: kubectl rollout restart deployment/mcpserver

HighErrorRate:
  - Check error logs: kubectl logs -l app=mcpserver | grep ERROR
  - Review recent changes
  - Consider rollback if > 5%

HighMemoryUsage:
  - Check for memory leaks
  - Review connection pooling
  - Scale horizontally if needed
```

## 🔄 Rollback Plan

### Immediate Rollback (< 5 min)
```bash
# Kubernetes
kubectl rollout undo deployment/mcpserver

# Docker Swarm
docker service update --rollback mcpserver

# Direct Docker
docker stop mcpserver && docker run -d --name mcpserver mcpserver:previous-version
```

### Data Rollback (if needed)
```sql
-- Restore from backup
pg_restore -d mcpserver mcpserver_backup_$DATE.sql

-- Or revert migrations
dotnet ef database update PreviousMigration
```

## 📝 Post-Deployment Tasks

### Documentation
- [ ] Update API documentation
- [ ] Update runbooks
- [ ] Document any configuration changes
- [ ] Update architecture diagrams

### Communication
- [ ] Notify stakeholders
- [ ] Update status page
- [ ] Send release notes
- [ ] Schedule retrospective

### Cleanup
- [ ] Remove old Docker images
- [ ] Clean up temporary files
- [ ] Archive deployment logs
- [ ] Update deployment metrics

## 🎯 Success Criteria

- [ ] All health checks passing
- [ ] Error rate < 0.1%
- [ ] Response time P95 < 100ms
- [ ] No critical alerts in first hour
- [ ] Successful smoke tests
- [ ] Positive user feedback

## 🚨 Emergency Contacts

- **On-Call Engineer**: [Phone/Slack]
- **Platform Team**: [Slack Channel]
- **Product Owner**: [Email/Phone]
- **Security Team**: [Email for incidents]