using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Tripper.Infra.Data;
using Tripper.Core.Entities;
using Tripper.Infra.Auth;
using Tripper.Core.DTOs;
using System.Net.Http.Headers;

namespace Tripper.Tests;

public class CoreRulesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CoreRulesTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TripperDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<TripperDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting");
                });
            });
        });

        _client = _factory.CreateClient();
    }

    private string GenerateToken(User user)
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        return jwtService.GenerateToken(user);
    }

    private async Task<(User User, string Token)> CreateUserAndTokenAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TripperDbContext>();
        
        var user = new User 
        { 
            Id = Guid.NewGuid(), 
            Username = username, 
            Email = $"{username}@test.com", 
            PasswordHash = "hash" 
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var token = GenerateToken(user);
        return (user, token);
    }

    [Fact]
    public async Task LastAdminCannotBeRemoved()
    {
        // Arrange
        var (admin, token) = await CreateUserAndTokenAsync("admin1");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create Group
        var createResponse = await _client.PostAsJsonAsync("/groups", new CreateGroupRequest("Test Group", "Desc", "City", "Country"));
        createResponse.EnsureSuccessStatusCode();
        var group = await createResponse.Content.ReadFromJsonAsync<GroupResponse>();

        // Act - Try to remove self (last admin)
        var response = await _client.DeleteAsync($"/groups/{group!.Id}/members/{admin.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
