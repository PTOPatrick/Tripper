using Tripper.API;
using Tripper.API.Endpoints;
using Tripper.Application;
using Tripper.Core.Interfaces;
using Tripper.Infra;
using Tripper.Infra.Data;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Services
    .AddApi(configuration)
    .AddApplication()
    .AddInfra(configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TripperDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    await TripperDbSeeder.SeedAsync(db, hasher);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => {
        options.SwaggerEndpoint("/openapi/v1.json", "Tripper API v1");
    });
}

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapGroupEndpoints();
app.MapItemEndpoints();
app.MapVotingEndpoints();
app.MapCurrencyEndpoints();

app.Run();
