
using MainWebAPI.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace MainWebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // builder.WebHost.UseUrls("http://10.60.250.239");
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Listen(IPAddress.Any, 7125, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
                        httpsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls13 | System.Security.Authentication.SslProtocols.Tls12;
                    });
                });
            });
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
             options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.LoginPath = "/api/account/login";
                options.LogoutPath = "/api/account/logout";
                options.SlidingExpiration = true;
            });
            // Add services to the container.
            builder.Services.AddHttpClient();
            builder.Services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.WriteIndented = true; // Pretty print JSON
                    });
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: "CORS_POLICY",
                                  builder =>
                                  {
                                      builder
                                      .AllowAnyHeader()
                                      .AllowAnyMethod()
                                      .SetIsOriginAllowed(options => true)
                                      .AllowCredentials();
                                  });
            });
            // builder.Services.AddStackExchangeRedisCache(options =>
            // {
            //     options.Configuration = "localhost:6379"; // Your Redis server configuration

            // });
            builder.Services.AddLogging();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                //app.UseSwaggerUI(c =>
                //{
                //    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                //    c.RoutePrefix = string.Empty; // This makes Swagger UI the root of the site
                //});
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseResponseCaching();
            app.UseCors("CORS_POLICY"); // Use CORS policy
            app.MapControllers();

            app.Run();
        }
    }
}
