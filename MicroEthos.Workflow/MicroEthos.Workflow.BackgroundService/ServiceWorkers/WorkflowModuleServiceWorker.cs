using MicroEthos.Common.Contracts;
// using MicroEthos.Common.Providers.Queue.Kafka;
using MicroEthos.Common.Providers.Queue.RabbitMQ;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.Business.Services;
using MicroEthos.Workflow.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using WorkflowCore.Interface;

namespace MicroEthos.Workflow.BackgroundService.ServiceWorkers;

internal class WorkflowModuleServiceWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IMicroEthosLogger _logger;
    private readonly IWorkflowHost _workflowHost;
    private readonly WorkflowModuleListenerService _workflowListenerService;
    private readonly IServiceProvider _serviceProvider;
    // private readonly IKafkaConnectionFactory _kafkaConnectionFactory;
    private readonly IRabbitConnectionFactory _rabbitConnectionFactory;
    public static Semaphore LockObj = new(1, 1);

    public WorkflowModuleServiceWorker(WorkflowModuleListenerService workflowListenerService,
        IMicroEthosLogger logger,
        IServiceProvider serviceProvider,
        IWorkflowHost workflowHost,
        // IKafkaConnectionFactory kafkaConnectionFactory,
        IRabbitConnectionFactory rabbitConnectionFactory)
    {
        _workflowListenerService = workflowListenerService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _workflowHost = workflowHost;
        // _kafkaConnectionFactory = kafkaConnectionFactory;
        _rabbitConnectionFactory = rabbitConnectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            LockObj.WaitOne();
            var modules = await LoadModules();
            await Initialize(stoppingToken);
            LockObj.Release();
            await Task.Delay(1000);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                var parallelCancelSource = new CancellationTokenSource();
                var parallelCancellationToken = parallelCancelSource.Token;
                Parallel.ForEachAsync(modules, async (module, cts) =>
                {
                    // using var queueConnection = _kafkaConnectionFactory.Create("workflow");
                    using var queueConnection = _rabbitConnectionFactory.Create(string.Empty);
                    await _logger.Info(
                        $"Now listening on response of module {module.Name}({module.Version}) on queue {module.Queues.ResponseQueueName}.");
                    await _logger.Info(
                        $"Now listening on event of module {module.Name}({module.Version}) on queue {module.Queues.EventQueueName}.");
                    while (!parallelCancellationToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
                        try
                        {
                            var moduleResp = await queueConnection.Dequeue(module.Queues.ResponseQueueName, 50);
                            if (!string.IsNullOrEmpty(moduleResp))
                            {
                                var result = await _workflowListenerService.ModuleResponseReceived(moduleResp, module);
                                if (result)
                                    await queueConnection.Ack(module.Queues.ResponseQueueName);
                            }

                            var moduleEvent = await queueConnection.Dequeue(module.Queues.EventQueueName, 50);
                            if (!string.IsNullOrEmpty(moduleEvent))
                            {
                                var result = await _workflowListenerService.ModuleEventRaised(moduleEvent, module);
                                if (result)
                                    await queueConnection.Ack(module.Queues.EventQueueName);
                            }
                        }
                        catch (Exception e)
                        {
                            await _logger.Error("Error occured :", e);
                        }

                    await queueConnection.Close();
                });

                await Task.Delay(150000, stoppingToken);
                parallelCancelSource.Cancel();
                await Task.Delay(500, stoppingToken);
            }
        }
        catch (Exception e)
        {
            await _logger.Error("exception occured in workflow module listener background service ...", e);
        }
    }

    private async Task<List<Modules>> LoadModules()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var modules = await scope.ServiceProvider.GetRequiredService<IModuleService>().LoadAll();

        //create queues if not exists
        // using var queueConnection = _kafkaConnectionFactory.Create("workflow");
        using var queueConnection = _rabbitConnectionFactory.Create(string.Empty);
        foreach (var m in modules)
        {
            await queueConnection.Declare(m.Queues.EventQueueName);
            await queueConnection.Declare(m.Queues.ResponseQueueName);
            await queueConnection.Declare(m.Queues.RequestQueueName);
        }

        return modules;
    }
    
    private async Task Initialize(CancellationToken stoppingToken)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(WorkflowStateModel)))
        {
            BsonClassMap.RegisterClassMap<WorkflowStateModel>(cm => { cm.AutoMap(); });
        }

        await RunDbCommands();
        await InitializeLoading();
        await Task.Delay(3000, stoppingToken);
        ThreadPool.QueueUserWorkItem(async (host) =>
        {
            await ((IWorkflowHost)host).StartAsync(stoppingToken);
        }, _workflowHost);
    }

    private async Task InitializeLoading()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IWorkflowService>().LoadAll();
    }
    
    private async Task RunDbCommands()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        try
        {
            var moduleRepo = scope.ServiceProvider.GetService<IRepository<Modules>>()!;
            await moduleRepo.RunCommand(DbCommands.CreateModulesCollectionUniqueIndex);
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
        }
    }
}