# OAuth Client Usage Guide

## Quick Start

1. **Configure OAuth Providers**
   - Copy `appsettings.OAuth.json` to your config directory
   - Add your OAuth provider credentials (see `/docs/OAuth-Setup.md`)

2. **Run the Server**
   ```bash
   cd src/McpServer.Web
   dotnet run
   ```

3. **Open the Example Client**
   - Open `/examples/oauth-client.html` in your browser
   - Or navigate to http://localhost:5080/examples/oauth-client.html

## How OAuth Authentication Works

### 1. Login Flow

```
Client → Server → OAuth Provider → Server → Client
```

1. Client clicks "Login with Google/Microsoft/GitHub"
2. Server redirects to OAuth provider
3. User authorizes the app
4. Provider redirects back with auth code
5. Server exchanges code for tokens
6. Server creates/updates user account
7. Server redirects to client with session token

### 2. Using the Token

After login, the client receives a session token. Include it in MCP requests:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list",
  "params": {
    "_auth": "Session session:userId:hash"
  }
}
```

### 3. Token Formats

The MCP server supports multiple authentication schemes:

- **Session**: `Session session:userId:hash` (from OAuth flow)
- **OAuth**: `OAuth provider:access_token` (direct OAuth token)
- **ApiKey**: `ApiKey your-api-key` (static API keys)
- **Bearer**: `Bearer jwt-token` (JWT tokens)

## Client Implementation

### JavaScript Example

```javascript
// Initiate OAuth login
function loginWithGoogle() {
    const redirectUri = encodeURIComponent(window.location.href);
    window.location.href = `http://localhost:5080/auth/login/google?redirect_uri=${redirectUri}`;
}

// Handle OAuth callback
const urlParams = new URLSearchParams(window.location.search);
const token = urlParams.get('token');
if (token) {
    // Store token
    localStorage.setItem('mcp_token', token);
    
    // Use in MCP requests
    const response = await fetch('http://localhost:5080/sse', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            jsonrpc: '2.0',
            id: 1,
            method: 'tools/list',
            params: {
                _auth: `Session ${token}`
            }
        })
    });
}
```

### Python Example

```python
import webbrowser
import requests
from urllib.parse import urlencode

# Start OAuth flow
def login_with_github():
    params = {
        'redirect_uri': 'http://localhost:8000/callback'
    }
    auth_url = f"http://localhost:5080/auth/login/github?{urlencode(params)}"
    webbrowser.open(auth_url)

# Handle callback (in your web framework)
def handle_callback(request):
    token = request.GET.get('token')
    if token:
        # Use token in MCP requests
        response = requests.post('http://localhost:5080/sse', json={
            'jsonrpc': '2.0',
            'id': 1,
            'method': 'tools/call',
            'params': {
                'name': 'echo',
                'arguments': {'message': 'Hello!'},
                '_auth': f'Session {token}'
            }
        })
```

## User Management

### Get Current User

```http
GET /auth/me
Authorization: Bearer {session-token}
```

Response:
```json
{
  "id": "user-id",
  "email": "user@example.com",
  "name": "User Name",
  "avatar": "https://...",
  "roles": ["user"],
  "externalLogins": ["Google", "GitHub"]
}
```

### Logout

```http
POST /auth/logout
Authorization: Bearer {session-token}
```

## Security Notes

1. **HTTPS Required**: Always use HTTPS in production
2. **Token Storage**: Store tokens securely (HttpOnly cookies recommended)
3. **CORS**: Configure CORS appropriately for your client domains
4. **Session Expiry**: Implement proper session management
5. **Refresh Tokens**: Use refresh tokens for long-lived sessions

## Troubleshooting

### "Invalid or expired state"
- OAuth state tokens expire after 5 minutes
- Complete the OAuth flow promptly

### "Unknown provider"
- Check provider name spelling (google, microsoft, github)
- Ensure provider is configured in appsettings

### "Authentication failed"
- Check OAuth provider credentials
- Verify redirect URI matches exactly
- Check server logs for detailed errors

## Advanced Usage

### Direct OAuth Token Usage

Instead of using session tokens, you can use OAuth access tokens directly:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/list",
  "params": {
    "_auth": "OAuth google:ya29.a0AfH6SMBx..."
  }
}
```

### Multiple External Logins

Users can link multiple OAuth providers to the same account:
1. Login with primary provider
2. Login with secondary provider
3. If email matches, accounts are automatically linked

### Custom Claims

OAuth providers include additional data that becomes user claims:
- GitHub: username, profile URL
- Google: email verification status
- Microsoft: tenant information