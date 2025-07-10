# OAuth Provider Setup Guide

## Overview

This guide explains how to configure OAuth authentication for the MCP Server with Google, Microsoft, and GitHub.

## Provider Configuration

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Go to "Credentials" → "Create Credentials" → "OAuth client ID"
5. Choose "Web application"
6. Add authorized redirect URIs:
   - `http://localhost:5080/auth/callback/google` (development)
   - `https://yourdomain.com/auth/callback/google` (production)
7. Copy Client ID and Client Secret

### Microsoft OAuth Setup

1. Go to [Azure Portal](https://portal.azure.com/)
2. Navigate to "Azure Active Directory" → "App registrations"
3. Click "New registration"
4. Set redirect URI: `http://localhost:5080/auth/callback/microsoft`
5. Under "Certificates & secrets", create a new client secret
6. Copy Application (client) ID and client secret
7. Under "API permissions", add:
   - Microsoft Graph → User.Read

### GitHub OAuth Setup

1. Go to GitHub Settings → Developer settings → OAuth Apps
2. Click "New OAuth App"
3. Fill in:
   - Application name: Your app name
   - Homepage URL: Your app URL
   - Authorization callback URL: `http://localhost:5080/auth/callback/github`
4. Copy Client ID and Client Secret

## Configuration

Add to your `appsettings.json` or environment variables:

```json
{
  "McpServer": {
    "OAuth": {
      "Google": {
        "ClientId": "your-client-id.apps.googleusercontent.com",
        "ClientSecret": "your-client-secret"
      },
      "Microsoft": {
        "ClientId": "your-application-id",
        "ClientSecret": "your-client-secret"
      },
      "GitHub": {
        "ClientId": "your-client-id",
        "ClientSecret": "your-client-secret"
      }
    }
  }
}
```

## OAuth Flow

### 1. Client Initiates OAuth Login

```http
GET /auth/login/{provider}?redirect_uri={client_redirect_uri}
```

Example:
```
GET /auth/login/google?redirect_uri=http://myclient.com/auth/complete
```

### 2. Server Redirects to Provider

The server will redirect to the OAuth provider's authorization page.

### 3. User Authorizes

User logs in and grants permissions at the provider's site.

### 4. Provider Redirects Back

Provider redirects to `/auth/callback/{provider}` with authorization code.

### 5. Server Completes Flow

Server exchanges code for tokens, creates/updates user, and redirects to client with auth token.

### 6. Client Uses Token

Client includes the token in MCP requests:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list",
  "params": {
    "_auth": "OAuth google:access_token_here"
  }
}
```

## Security Considerations

1. **HTTPS Required**: Always use HTTPS in production
2. **State Parameter**: Used for CSRF protection
3. **Token Storage**: Store tokens securely
4. **Token Refresh**: Implement token refresh for long-lived sessions
5. **Scope Limitations**: Only request necessary scopes

## Testing OAuth Locally

For local development, you can use:
- `localhost` redirect URIs (Google and GitHub support this)
- ngrok for HTTPS tunnel
- Test users/accounts

## Troubleshooting

### Common Issues

1. **Redirect URI Mismatch**
   - Ensure redirect URI exactly matches registered URI
   - Check for trailing slashes
   - Verify HTTP vs HTTPS

2. **Invalid Client**
   - Verify client ID and secret
   - Check if app is in production/development mode

3. **Scope Errors**
   - Ensure requested scopes are configured in provider
   - Some scopes require app verification

## Client Integration Examples

### JavaScript/TypeScript
```typescript
// Initiate OAuth login
window.location.href = 'http://localhost:5080/auth/login/google?redirect_uri=' + 
  encodeURIComponent(window.location.origin + '/auth/complete');

// Handle callback
const urlParams = new URLSearchParams(window.location.search);
const token = urlParams.get('token');
const error = urlParams.get('error');

if (token) {
  // Store token and use in MCP requests
  localStorage.setItem('mcp_token', token);
}
```

### Python
```python
import requests
import webbrowser

# Initiate OAuth
auth_url = 'http://localhost:5080/auth/login/github'
webbrowser.open(auth_url)

# In callback handler, get token
token = request.args.get('token')

# Use token in MCP requests
response = requests.post('http://localhost:5080/sse', json={
    'jsonrpc': '2.0',
    'id': 1,
    'method': 'tools/list',
    'params': {
        '_auth': f'OAuth github:{token}'
    }
})
```