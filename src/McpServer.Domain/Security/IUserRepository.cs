namespace McpServer.Domain.Security;

/// <summary>
/// Repository interface for user management.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by email.
    /// </summary>
    /// <param name="email">The email address.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by external login.
    /// </summary>
    /// <param name="provider">The provider name.</param>
    /// <param name="providerUserId">The provider user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user if found, null otherwise.</returns>
    Task<User?> GetByExternalLoginAsync(string provider, string providerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created user.</returns>
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated user.</returns>
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if deleted, false otherwise.</returns>
    Task<bool> DeleteAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an external login to a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="externalLogin">The external login to link.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if linked, false otherwise.</returns>
    Task<bool> LinkExternalLoginAsync(string userId, ExternalLogin externalLogin, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks an external login from a user.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="provider">The provider to unlink.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if unlinked, false otherwise.</returns>
    Task<bool> UnlinkExternalLoginAsync(string userId, string provider, CancellationToken cancellationToken = default);
}