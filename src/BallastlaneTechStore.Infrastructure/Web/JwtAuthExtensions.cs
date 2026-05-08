using System.Text;
using BallastlaneTechStore.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BallastlaneTechStore.Infrastructure.Web;

public static class JwtAuthExtensions
{
    public static IServiceCollection AddTechStoreJwtAuth(this IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt settings missing.");

        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = section.Issuer,
                    ValidAudience = section.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(section.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        return services;
    }
}
