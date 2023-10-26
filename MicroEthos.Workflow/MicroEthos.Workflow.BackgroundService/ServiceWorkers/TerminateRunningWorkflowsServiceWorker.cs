using System.Linq.Expressions;
using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models.Enums;
// using MicroEthos.Common.Providers.Queue.Kafka;
using MicroEthos.Common.Providers.Queue.RabbitMQ;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.DataAccess;
using MicroEthos.Workflow.DataAccess.Repository;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Models.LifeCycleEvents;
using WorkflowCore.Services.DefinitionStorage;
using WorkflowInstance = MicroEthos.Workflow.Abstraction.Models.Database.WorkflowInstance;

namespace MicroEthos.Workflow.BackgroundService.ServiceWorkers;

internal class TerminateRunningWorkflowsServiceWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IQueueProvider _queueProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedLockProvider _lockProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILifeCycleEventHub _eventHub;
    private readonly GeneralRepository<WorkflowInstance> _workflowInstanceRepo;
    private readonly IMicroEthosLogger _logger;

    public TerminateRunningWorkflowsServiceWorker(
        IWorkflowHost workflowHost,
        IServiceProvider serviceProvider,
        GeneralRepository<WorkflowInstance> workflowInstanceRepo,
        IQueueProvider queueProvider,
        IMicroEthosLogger logger, ILifeCycleEventHub eventHub,
        IDateTimeProvider dateTimeProvider, IDistributedLockProvider lockProvider)
    {
        _workflowHost = workflowHost;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _eventHub = eventHub;
        _dateTimeProvider = dateTimeProvider;
        _lockProvider = lockProvider;
        _workflowInstanceRepo = workflowInstanceRepo;
        _queueProvider = queueProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            //await TerminateExpireWorkflows();
            await Task.Delay(TimeSpan.FromMinutes(5));
        }
    }

    private async Task TerminateExpireWorkflows()
    {
        try
        {
            var before20Min = DateTime.UtcNow.AddMinutes(-60);
            var instances = await _workflowInstanceRepo.ListAsync(w => w.CreateTime < before20Min &&
                                                                       w.CompleteTime == null);
            // var canBeLockedInstances =
            //     instances; //.Where(w => _lockProvider.AcquireLock(w.Id, new CancellationToken()).GetAwaiter().GetResult()).ToList();
            // instancesIds = canBeLockedInstances.Select(s => s.Id).ToList();
            (Expression<Func<WorkflowInstance, object>>, object) statusUpdate = (w => w.Status,
                WorkflowInstanceStatus.Terminated);
            (Expression<Func<WorkflowInstance, object>>, object) completeTimeUpdate = (w => w.CompleteTime,
                _dateTimeProvider.UtcNow);
            var result = await _workflowInstanceRepo.UpdateManyAsync(w => w.CreateTime < before20Min &&
                                                                          w.CompleteTime == null,
                statusUpdate, completeTimeUpdate);
            if(result < 1)
                return;

            foreach (var wf in instances)
            {
                await _queueProvider.QueueWork(wf.Id, QueueType.Index);
                await _eventHub.PublishNotification(new WorkflowTerminated
                {
                    EventTimeUtc = _dateTimeProvider.UtcNow,
                    Reference = wf.Reference,
                    WorkflowInstanceId = wf.Id,
                    WorkflowDefinitionId = wf.WorkflowDefinitionId,
                    Version = wf.Version
                });
            }
        }
        catch (Exception ex)
        {
            await _logger.Error("Exception Occured.", ex);
        }
    }
}