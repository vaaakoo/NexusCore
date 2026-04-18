using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexus.Core.Infrastructure.Security;
using Xunit;

namespace Nexus.Core.Tests.Security;

public sealed class JwtExtensionsTests
{
    [Fact]
    public void AddNexusSecurity_ShouldRegisterAuthenticationAndAuthorization()
    {
        // Arrange
        var services = new ServiceCollection();
        var config   = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"]        = "SuperSecretSigningKey_Minimum32CharsLength!!",
                ["Jwt:Issuer"]           = "test-issuer",
                ["Jwt:Audience"]         = "test-audience",
                ["Jwt:ExpiryMinutes"]    = "60",
                ["Jwt:ClockSkewSeconds"] = "30"
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddNexusSecurity(config);
        var provider = services.BuildServiceProvider();

        // Assert
        // 1. Check JwtSettings registration
        var settings = provider.GetService<JwtSettings>();
        settings.Should().NotBeNull();
        settings!.Issuer.Should().Be("test-issuer");

        // 2. Check Authentication services
        var authService = provider.GetService<IAuthenticationService>();
        authService.Should().NotBeNull();

        // 3. Check Authorization services
        var authzService = provider.GetService<IAuthorizationService>();
        authzService.Should().NotBeNull();
        
        // 4. Check JwtBearerOptions (indirectly via IOptions)
        // Note: Authentication scheme options are usually resolved via IOptionsMonitor<JwtBearerOptions>
    }

    [Fact]
    public void AddNexusSecurity_ShouldThrow_WhenConfigIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var config   = new ConfigurationBuilder().Build(); // Empty config

        // Act
        var act = () => services.AddNexusSecurity(config);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Missing required configuration section*");
    }
}
