using System.Collections.Generic;
using System.Text;
using ApplicationCore;
using ApplicationCore.APNS;
using ApplicationCore.Interfaces;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Model;
using Model.Utils;

namespace Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<IRepository>(provider =>
                new DatabaseRepository(Configuration.GetConnectionString("CloudResConnection"),
                    provider.GetService<ILogger<DatabaseRepository>>()));

            services.AddSingleton(new SCEnvironment(Configuration.GetValue("CertPath", "certs"),
                Configuration.GetValue("CertPass", "")));
            services.AddSingleton<IApnHelper, ApnHelper>();
            services.AddSingleton<ICloudMessageService, ApnService>(provider =>
                {
                    var repository = provider.GetService<IRepository>();
                    var logger = provider.GetService<ILogger<ApnService>>();
                    var env = new SCEnvironment(Configuration.GetValue("CertPath", "certs"),
                        Configuration.GetValue("CertPass", ""));
                    var apnHelper = provider.GetService<IApnHelper>();
                    var workers = new List<IMessageWorker>();
                    var loggerWorker = provider.GetService<ILogger<IMessageWorker>>();

                    for (var i = 1; i <= SCEnvironment.MaxWorkers; i++)
                    {
                        workers.Add(new ApnMessageWorker(repository, env, apnHelper, loggerWorker,
                            CryptoUtil.GetRandomAlphanumericString(8)));
                    }

                    var maintenanceLogger = provider.GetService<ILogger<IMaintenanceWorker>>();
                    var maintenanceWorker = new MaintenanceWorker(CryptoUtil.GetRandomAlphanumericString(8),
                        maintenanceLogger, SCEnvironment.MaintenancePeriod);
                    return new ApnService(repository, logger, workers, maintenanceWorker);
                }
            ); 
            
            services.AddScoped<ITokenService, TokenService>();
            
            services.AddSingleton<ICloudMessageManager, CloudMessageManager>();
            services.AddDistributedRedisCache(options =>
                {
                    if (!Configuration.GetValue<bool>("Redis:IsRedisEnabled", false))
                    {
                        return;
                    }

                    options.Configuration = Configuration.GetValue("Redis:Configuration", "127.0.0.1:6379");
                    options.InstanceName = Configuration.GetValue("Redis:master", "master");
                }
            );
            services.AddControllers().AddNewtonsoftJson();

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "Api", Version = "v1"}); });

            // configure jwt authentication 
            var key = Encoding.ASCII.GetBytes(Configuration["Secret"]);

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
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });
        }


        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api v1"));
            }

            // app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}