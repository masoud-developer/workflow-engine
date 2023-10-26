using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models.Enums;
// using MicroEthos.Common.Providers.Queue.Kafka;
using MicroEthos.Common.Providers.Queue.RabbitMQ;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.DataAccess;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using WorkflowCore.Interface;
using WorkflowCore.Services.DefinitionStorage;

namespace MicroEthos.Workflow.BackgroundService.ServiceWorkers;

internal class WorkflowRunnerServiceWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IServiceProvider _serviceProvider;
    private readonly IJsonService _jsonService;
    private readonly IMicroEthosLogger _logger;

    private readonly IDefinitionLoader _workflowLoader;

    // private readonly IKafkaConnectionFactory _kafkaConnectionFactory;
    private readonly IRabbitConnectionFactory _rabbitConnectionFactory;
    private readonly string _nodeId;

    public WorkflowRunnerServiceWorker(
        IWorkflowHost workflowHost,
        IServiceProvider serviceProvider,
        IJsonService jsonService,
        IMicroEthosLogger logger,
        // IKafkaConnectionFactory kafkaConnectionFactory,
        IDefinitionLoader workflowLoader,
        IRabbitConnectionFactory rabbitConnectionFactory)
    {
        _workflowHost = workflowHost;
        _serviceProvider = serviceProvider;
        _jsonService = jsonService;
        _logger = logger;
        // _kafkaConnectionFactory = kafkaConnectionFactory;
        _workflowLoader = workflowLoader;
        _rabbitConnectionFactory = rabbitConnectionFactory;
        _nodeId = $"workflow-{(EnvironmentHelper.Get("CLUSTER_ID") ?? "node1")}";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(3000, stoppingToken);
            // WorkflowModuleServiceWorker.LockObj.WaitOne();
            // await WorkflowStarted(stoppingToken);
            // WorkflowModuleServiceWorker.LockObj.Release();

            await _logger.Info(
                $"listening on new module created.");
            await _logger.Info(
                $"listening on new workflow definition created.");
            // using var queueConnection = _kafkaConnectionFactory.Create(_nodeId);
            using var queueConnection = _rabbitConnectionFactory.Create("workflow");
            while (!stoppingToken.IsCancellationRequested)
            {
                var createdModuleMsg = await queueConnection.Dequeue(TopicNames.WorkflowModuleCreated, 50);
                if (!string.IsNullOrEmpty(createdModuleMsg))
                {
                    var createModuleRes = await ModuleCreated(createdModuleMsg);
                    if (createModuleRes)
                        await queueConnection.Ack(TopicNames.WorkflowModuleCreated);
                }

                var createdWorkflowDefMsg = await queueConnection.Dequeue(TopicNames.WorkflowDefinitionCreated, 50);
                if (!string.IsNullOrEmpty(createdWorkflowDefMsg))
                {
                    var createWorkflowRes = await WorkflowDefinitionCreated(createdWorkflowDefMsg);
                    if (createWorkflowRes)
                        await queueConnection.Ack(TopicNames.WorkflowDefinitionCreated);
                }

                var workflowEventMsg =
                    await queueConnection.Dequeue(QueueHelper.GetNames(ModuleConstants.ModuleName).RequestQueueName,
                        50);
                if (!string.IsNullOrEmpty(workflowEventMsg))
                {
                    var res = await WorkflowServeRequest(workflowEventMsg);
                    if (res)
                        await queueConnection.Ack(QueueHelper.GetNames(ModuleConstants.ModuleName).RequestQueueName);
                }
            }
        }
        catch (Exception e)
        {
            await _logger.Error("exception occured in workflow runner background service ...", e);
        }
    }

    private async Task<bool> ModuleCreated(string moduleMsg)
    {
        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();
            var module = _jsonService.Deserialize<Modules>(moduleMsg)!;
            await moduleService.Load(module.Name, module.Version, module.AssemblyFile);
            return true;
        }
        catch (Exception e)
        {
            await _logger.Error("error on module created event.", e);
            return false;
        }
    }

    private async Task<bool> WorkflowDefinitionCreated(string workflowDefMsg)
    {
        try
        {
            var workflowDef = _jsonService.Deserialize<WorkflowDefinitions>(workflowDefMsg);
            if (!_workflowHost.Registry.IsRegistered(workflowDef.Model.Id.ToString(), workflowDef.Model.Version))
            {
                _workflowLoader.LoadDefinition(workflowDef.Raw, Deserializers.Json);
            }

            return true;
        }
        catch (Exception e)
        {
            await _logger.Error("error on workflow definition created event.", e);
            return false;
        }
    }

    private async Task<bool> WorkflowServeRequest(string req)
    {
        try
        {
            var request = _jsonService.Deserialize<WorkflowEventDto>(req);
            if (request == null)
                return false;
            if (request.Command == ModuleRequests.ModuleRegistration)
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var moduleService = scope.ServiceProvider.GetRequiredService<IModuleService>();

                var payload = _jsonService.Deserialize<WorkflowAddModuleModel>(_jsonService.Serialize(request.Payload));
                var result = await moduleService.CreateAndLoadDynamically(payload);
                if (result.Status != OperationResultType.Success)
                {
                    return false;
                }

                await _logger.Info($"Module {payload.Name} successfully installed √√√√√");
                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            await _logger.Error("error on workflow request process ....", e);
            return false;
        }
    }
}