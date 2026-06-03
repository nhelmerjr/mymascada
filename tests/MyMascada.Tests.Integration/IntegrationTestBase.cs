using MyMascada.Domain.Enums;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MyMascada.Domain.Entities;
using MyMascada.Infrastructure.Data;
using MyMascada.WebAPI;
using Xunit;

namespace MyMascada.Tests.Integration;

public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    protected readonly WebApplicationFactory<Program> Factory;
    protected readonly HttpClient Client;
    protected readonly ApplicationDbContext DbContext;
    protected readonly Guid TestUserId = Guid.NewGuid();

    // JWT settings shared between the host (token validation) and GenerateJwtToken (signing),
    // so they must match for authenticated requests to succeed.
    private const string TestJwtKey = "super-secret-key-for-testing-that-is-32-chars-long!!";
    private const string TestJwtIssuer = "MyMascadaTest";
    private const string TestJwtAudience = "MyMascadaTest";

    // Program.cs reads several settings from configuration BEFORE building the host (e.g.
    // Jwt:Key throws if missing). ConfigureAppConfiguration runs too late for those, so set
    // them as environment variables — a source WebApplication.CreateBuilder reads up front.
    static IntegrationTestBase()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", TestJwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", TestJwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", TestJwtAudience);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "Host=localhost;Database=mymascada_test;Username=test;Password=test");
    }

    protected IntegrationTestBase(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Remove all existing DbContext registrations
                var dbContextDescriptors = services.Where(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                         d.ServiceType == typeof(ApplicationDbContext) ||
                         d.ServiceType.Name.Contains("DbContext")).ToList();
                         
                foreach (var descriptor in dbContextDescriptors)
                {
                    services.Remove(descriptor);
                }

                // Add in-memory database for testing. The database name must be captured ONCE
                // (not inside the options lambda) so every DbContext instance in this test shares
                // the same store — otherwise seeded data is invisible to the request's context.
                var dbName = $"TestDb_{Guid.NewGuid()}";
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName);
                    options.EnableSensitiveDataLogging();
                }, ServiceLifetime.Scoped);

                // Reduce logging noise
                services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            });
        });

        Client = Factory.CreateClient();
        
        // Get the database context
        var scope = Factory.Services.CreateScope();
        DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database is created
        DbContext.Database.EnsureCreated();
        
        // Set up authentication
        SetupAuthentication();
    }

    private void SetupAuthentication()
    {
        // Create a JWT token for testing
        var token = GenerateJwtToken(TestUserId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private string GenerateJwtToken(Guid userId)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(TestJwtKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, "Test User"),
                new Claim(ClaimTypes.Email, "test@example.com")
            }),
            Issuer = TestJwtIssuer,
            Audience = TestJwtAudience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    protected async Task<User> CreateTestUserAsync()
    {
        var user = new User
        {
            Id = TestUserId,
            Email = "test@example.com",
            UserName = "testuser",
            FirstName = "Test",
            LastName = "User",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();
        return user;
    }

    protected async Task<Account> CreateTestAccountAsync(string name = "Test Account", decimal initialBalance = 1000m)
    {
        var account = new Account
        {
            Name = name,
            Type = Domain.Enums.AccountType.Checking,
            CurrentBalance = initialBalance,
            Currency = "USD",
            UserId = TestUserId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Accounts.Add(account);
        await DbContext.SaveChangesAsync();
        return account;
    }

    protected async Task<Transaction> CreateTestTransactionAsync(int accountId, decimal amount = 100m, string description = "Test Transaction")
    {
        var transaction = new Transaction
        {
            Amount = amount,
            Description = description,
            TransactionDate = DateTime.UtcNow,
            AccountId = accountId,
            Status = Domain.Enums.TransactionStatus.Cleared,
            Source = Domain.Enums.TransactionSource.Manual,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Transactions.Add(transaction);
        await DbContext.SaveChangesAsync();
        return transaction;
    }

    protected async Task<Category> CreateTestCategoryAsync(string name = "Test Category")
    {
        var category = new Category
        {
            Name = name,
            Color = "#FF0000",
            IsSystemCategory = false,
            IsActive = true,
            UserId = TestUserId,
            CreatedAt = DateTime.UtcNow
        };

        DbContext.Categories.Add(category);
        await DbContext.SaveChangesAsync();
        return category;
    }

    public void Dispose()
    {
        DbContext?.Dispose();
        Client?.Dispose();
    }
}