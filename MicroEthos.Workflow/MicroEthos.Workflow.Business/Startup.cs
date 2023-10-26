// using MicroEthos.Common.Providers.Queue.Kafka;

using MicroEthos.Common.Providers.Queue.RabbitMQ;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Business.Extensions;
using MicroEthos.Workflow.Business.Providers;
using MicroEthos.Workflow.Business.Services;
using MicroEthos.Workflow.Business.Workflow.Modules;
using MicroEthos.Workflow.Business.Workflow.Providers;
using MicroEthos.Workflow.Business.Workflow.Steps.Middlewares;
using MicroEthos.Workflow.DataAccess;
using MicroEthos.Workflow.DataAccess.Repository;
using Microsoft.Extensions.DependencyInjection;
using WorkflowCore.Interface;

namespace MicroEthos.Workflow.Business;

public static class Startup
{
    public static void AddBusiness(this IServiceCollection services)
    {
        // var kafkaHost = EnvironmentHelper.Get("KAFKA_HOST")!;
        // var kafkaUsername = EnvironmentHelper.Get("KAFKA_USER_NAME")!;
        // var kafkaPassword = EnvironmentHelper.Get("KAFKA_USER_PASS")!;

        // services.AddKafkaConnectionPool(500);
        // services.AddKafkaConnectionFactory(kafkaUsername, kafkaPassword, kafkaHost);
        // services.AddKafka();

        var rabbitHost = EnvironmentHelper.Get("RABBIT_SERVER")!;
        var rabbitUsername = EnvironmentHelper.Get("RABBIT_USERNAME")!;
        var rabbitPassword = EnvironmentHelper.Get("RABBIT_PASSWORD")!;
        services.AddRabbitConnectionFactory(true, rabbitUsername, rabbitPassword, rabbitHost);
        services.AddRabbit();
        services.AddRabbitConnectionPool(500, false, rabbitUsername, rabbitPassword, rabbitHost);

        services.AddDataAccess();

        services.AddWorkflow(config =>
        {
            config.UseIdleTime(TimeSpan.FromMilliseconds(100));
            config.UsePollInterval(TimeSpan.FromMilliseconds(1000));
            config.EnableIndexes = false;
            config.UseRedisQueues(EnvironmentHelper.Get("REDIS_DB_CONNECTION_STRING"), "workflow");
            config.UseRedisLocking(EnvironmentHelper.Get("REDIS_DB_CONNECTION_STRING"), "workflow");
            config.UseRedisEventHub(EnvironmentHelper.Get("REDIS_DB_CONNECTION_STRING"), "workflow-events");
            config.UseMongoDBPersistance(EnvironmentHelper.Get("MONGO_DB_URL")!,
                EnvironmentHelper.Get("MONGO_DB_NAME")!);
            // config.UseElasticsearch(new ConnectionSettings(new Uri(builder.Configuration["ElasticConfiguration:Uri"])),
            //     "dr_ethos_workflow_index");
            config.UseMaxConcurrentWorkflows(100000);
        });
        services.AddWorkflowDSL();
        services.AddWorkflowStepMiddleware<WorkflowStepMiddleware>();

        //transient services
        services.AddTransient<IDefinitionLoader, WorkflowDefinitionLoader>();

        //scoped services
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IModuleService, ModuleService>();

        //singleton services
        services.AddSingleton<WorkflowModuleListenerService>();
        services.AddSingleton<WorkflowModuleLoader>();
        services.AddSingleton<IModulesClient, ModulesClient>();
        services
            .AddSingleton<IRepository<WorkflowDefinitions>,
                MongoDbRepository<WorkflowDefinitions>>();
        services
            .AddSingleton<IRepository<Modules>,
                MongoDbRepository<Modules>>();
        services
            .AddSingleton<GeneralRepository<WorkflowInstance>>();
        services
            .AddSingleton<GeneralRepository<WorkflowInstanceError>>();
    }
}