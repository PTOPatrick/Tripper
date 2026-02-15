using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Tripper.Application.Interfaces.Services;
using Tripper.Core.Interfaces;
using Tripper.Infra.Auth;
using Tripper.Infra.Services;

namespace Tripper.API;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApi(IConfiguration configuration)
        {
            var jwtConfiguration = configuration.GetSection("JwtSettings");
            var jwtSettings = jwtConfiguration.Get<JwtSettings>();
            var key = Encoding.ASCII.GetBytes(jwtSettings!.Secret);
            
            services.Configure<JwtSettings>(jwtConfiguration);
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddOpenApi();
            services.AddSecurity(key, jwtSettings);
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular",
                    policy => policy.WithOrigins("http://localhost:4200")
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });
        
            return services;
        }

        private void AddSecurity(byte[] key, JwtSettings jwtSettings)
        {
            services.AddScoped<JwtTokenService>();
            services.AddScoped<IPasswordHasher, PasswordHasher>();

            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience
                    };
                });
        
            services.AddAuthorization();
        }
    }
}