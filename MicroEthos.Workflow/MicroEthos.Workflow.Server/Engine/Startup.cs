using System.IO.Compression;
using MicroEthos.Common.Extensions;
using MicroEthos.Common.Logging.Extensions;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.Business;
using MicroEthos.Workflow.DataAccess;
using MicroEthos.Workflow.Server.ServiceWorkers;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using MongoDB.Bson.Serialization;
using WorkflowCore.Interface;

namespace MicroEthos.Workflow.Server.Engine;

public static class Startup
{
    public static void AddServices(this IServiceCollection services, WebApplicationBuilder builder)
    {
        services.AddMicroEthosCommon();
        services.AddMicroEthosLogging();
        services.AddCors();
        services.AddRequestDecompression();
        services.Configure<GzipCompressionProviderOptions>(options => { options.Level = CompressionLevel.Fastest; });
        services.AddResponseCompression(options => { options.Providers.Add<GzipCompressionProvider>(); });
        services.AddControllers().AddNewtonsoftJson();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "API",
                Description = $"MicroEthos {ModuleConstants.ModuleName} API"
            });
        });

        services.AddBusiness();
        services.AddHostedService<WorkflowComponentsLoaderServiceWorker>();
    }

    public static void UseServices(this WebApplication app)
    {
        if (EnvironmentHelper.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        app.UseCors(c => c
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
        app.UseRequestDecompression();
        app.UseResponseCompression();
        app.UseRouting();
        app.MapControllers();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", ModuleConstants.ModuleName);
            c.RoutePrefix = string.Empty; // Set Swagger UI at apps root
        });

        ApplicationStarted(app.Services);
    }

    private static void ApplicationStarted(IServiceProvider serviceProvider)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(WorkflowStateModel)))
        {
            BsonClassMap.RegisterClassMap<WorkflowStateModel>(cm => { cm.AutoMap(); });
        }

        LoadAll(serviceProvider);
        var queue = serviceProvider.GetRequiredService<IQueueProvider>();
        var eventHub = serviceProvider.GetRequiredService<ILifeCycleEventHub>();
        var lockProvider = serviceProvider.GetRequiredService<IDistributedLockProvider>();
        queue.Start();
        eventHub.Start();
        lockProvider.Start();

        //run db scripts
        using var scope = serviceProvider.CreateAsyncScope();
        try
        {
            var moduleRepo = scope.ServiceProvider.GetService<IRepository<Modules>>()!;
            var res = moduleRepo.RunCommand(DbCommands.CreateModulesCollectionUniqueIndex).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static void LoadAll(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IModuleService>().LoadAll().GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<IWorkflowService>().LoadAll().GetAwaiter().GetResult();
    }
}