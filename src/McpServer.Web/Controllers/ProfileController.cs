using McpServer.Domain.Security;
using Microsoft.AspNetCore.Mvc;

namespace McpServer.Web.Controllers;

/// <summary>
/// Controller for managing user profiles.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly IUserProfileService _profileService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<ProfileController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileController"/> class.
    /// </summary>
    public ProfileController(
        IUserProfileService profileService,
        ISessionService sessionService,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _sessionService = sessionService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current user's profile.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        var profile = await _profileService.GetProfileAsync(userId);
        if (profile == null)
        {
            return NotFound();
        }

        return Ok(profile);
    }

    /// <summary>
    /// Gets a user's profile by ID.
    /// </summary>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetProfile(string userId)
    {
        var profile = await _profileService.GetProfileAsync(userId);
        if (profile == null)
        {
            return NotFound();
        }

        // Return limited info for other users
        var currentUserId = await GetUserIdFromSession();
        if (currentUserId != userId)
        {
            return Ok(new
            {
                profile.UserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                profile.Bio,
                profile.Location,
                profile.Website
            });
        }

        return Ok(profile);
    }

    /// <summary>
    /// Updates the current user's profile.
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] ProfileUpdateRequest request)
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            var profile = await _profileService.UpdateProfileAsync(userId, request);
            return Ok(profile);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Updates the current user's avatar.
    /// </summary>
    [HttpPut("me/avatar")]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.AvatarUrl))
        {
            return BadRequest(new { error = "Avatar URL is required" });
        }

        var success = await _profileService.UpdateAvatarAsync(userId, request.AvatarUrl);
        if (!success)
        {
            return NotFound();
        }

        return Ok(new { success = true });
    }

    /// <summary>
    /// Gets the current user's custom claims.
    /// </summary>
    [HttpGet("me/claims")]
    public async Task<IActionResult> GetMyClaims()
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        var claims = await _profileService.GetClaimsAsync(userId);
        return Ok(claims);
    }

    /// <summary>
    /// Adds a custom claim to the current user.
    /// </summary>
    [HttpPost("me/claims")]
    public async Task<IActionResult> AddClaim([FromBody] AddClaimRequest request)
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest(new { error = "Claim type and value are required" });
        }

        var success = await _profileService.AddClaimAsync(userId, request.Type, request.Value);
        return Ok(new { success });
    }

    /// <summary>
    /// Removes a custom claim from the current user.
    /// </summary>
    [HttpDelete("me/claims/{claimType}")]
    public async Task<IActionResult> RemoveClaim(string claimType, [FromQuery] string? claimValue = null)
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        var success = await _profileService.RemoveClaimAsync(userId, claimType, claimValue);
        return Ok(new { success });
    }

    /// <summary>
    /// Gets the audit trail for the current user.
    /// </summary>
    [HttpGet("me/audit")]
    public async Task<IActionResult> GetAuditTrail([FromQuery] int limit = 50)
    {
        var userId = await GetUserIdFromSession();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (limit < 1 || limit > 100)
        {
            limit = 50;
        }

        var entries = await _profileService.GetAuditTrailAsync(userId, limit);
        return Ok(entries);
    }

    /// <summary>
    /// Validates a user profile.
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidateProfile([FromBody] UserProfile profile)
    {
        var result = _profileService.ValidateProfile(profile);
        if (result.IsValid)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }

    private async Task<string?> GetUserIdFromSession()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var session = await _sessionService.ValidateSessionAsync(token);
        return session?.UserId;
    }
}

/// <summary>
/// Request to update avatar.
/// </summary>
public class UpdateAvatarRequest
{
    /// <summary>
    /// Gets or sets the avatar URL.
    /// </summary>
    public required string AvatarUrl { get; set; }
}

/// <summary>
/// Request to add a claim.
/// </summary>
public class AddClaimRequest
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public required string Value { get; set; }
}