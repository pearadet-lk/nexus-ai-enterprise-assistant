namespace NexusAI.Contracts.Auth;

public sealed record UserProfileDto(
    string UserId,
    string? Email,
    string? DisplayName,
    IReadOnlyList<string> Roles);
