using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace NexusAI.SharedKernel.Auth;

public static class NexusKeycloakAuthExtensions
{
    public static IServiceCollection AddNexusKeycloakAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");
        var issuer = configuration["Keycloak:Issuer"] ?? authority;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = configuration["Keycloak:Audience"];
                options.RequireHttpsMetadata = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = configuration.GetValue("Keycloak:ValidateAudience", false),
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is not ClaimsIdentity identity)
                        {
                            return Task.CompletedTask;
                        }

                        AddRoleClaims(identity, context.Principal.Claims.Where(c => c.Type == "roles").ToList());
                        AddRealmAccessRoles(identity, context.Principal.FindFirst("realm_access")?.Value);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    private static void AddRoleClaims(ClaimsIdentity identity, IReadOnlyList<Claim> roleClaims)
    {
        foreach (var claim in roleClaims)
        {
            AddRoleValue(identity, claim.Value);
        }
    }

    private static void AddRealmAccessRoles(ClaimsIdentity identity, string? realmAccessJson)
    {
        if (string.IsNullOrWhiteSpace(realmAccessJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccessJson);
            if (!document.RootElement.TryGetProperty("roles", out var rolesElement) ||
                rolesElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var role in rolesElement.EnumerateArray())
            {
                AddRoleValue(identity, role.GetString());
            }
        }
        catch (JsonException)
        {
            // Ignore malformed realm_access payloads.
        }
    }

    private static void AddRoleValue(ClaimsIdentity identity, string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        if (!identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
}
