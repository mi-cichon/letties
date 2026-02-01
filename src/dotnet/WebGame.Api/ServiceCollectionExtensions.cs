using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using WebGame.Application.Games;
using WebGame.Application.Languages;
using WebGame.Application.Lobbies;
using WebGame.Application.Services;
using WebGame.BackgroundServices;
using WebGame.Domain.Interfaces;
using WebGame.Domain.Interfaces.Games;
using WebGame.Domain.Interfaces.Languages;
using WebGame.Domain.Interfaces.Lobbies;
using WebGame.Hubs;

namespace WebGame;

public static class ServiceCollectionExtensions
{
    public static WebApplication MapHubs(this WebApplication app)
    {
        app.MapHub<GameHub>("/gameHub");
        app.MapHub<LoginHub>("/loginHub");

        return app;
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection AddServices()
        {
            services
                .AddSingleton<ILobbyManager, LobbyManager>()
                .AddTransient<IGameContextService, GameContextService>()
                .AddTransient<IAuthorizationService, AuthorizationService>();

            services
                .AddSingleton<IGameLanguageProviderFactory, GameLanguageProviderFactory>()
                .AddSingleton<IGameLanguageProvider, EnglishLanguageProvider>()
                .AddSingleton<IGameLanguageProvider, PolishLanguageProvider>();

            services
                .AddSingleton<IGameEngineFactory, GameEngineFactory>()
                .AddSingleton<IBoardGenerator, BoardGenerator>();

            services.AddHostedService<GameRulesTickService>();
            
            return services;
        }

        public IServiceCollection AddGameLobbies(IConfiguration configuration)
        {
            var lobbyCount = configuration.GetValue<int>("LobbyCount");

            for (var i = 0; i < lobbyCount; i++)
            {
                services.AddSingleton<IGameLobby, GameLobby>();
            }

            return services;
        }
        
        public IServiceCollection AddSwaggerGen()
        {
            return services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Scrabble Game API", Version = "v1" });

                c.AddSignalRSwaggerGen();
    
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });
        }

        public IServiceCollection AddJwtAuthentication(IConfiguration configuration)
        {
            var jwtSecret = configuration.GetSection("JwtSecret").Value!;
            var key = Encoding.ASCII.GetBytes(jwtSecret);

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });
        
            return services;
        }
    }
}