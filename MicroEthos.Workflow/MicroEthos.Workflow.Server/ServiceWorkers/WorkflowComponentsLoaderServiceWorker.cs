using MicroEthos.Common.Contracts;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Business.Services;
using MicroEthos.Workflow.Business.Statics;
using Microsoft.Extensions.DependencyInjection;

namespace MicroEthos.Workflow.Server.ServiceWorkers;

internal class WorkflowComponentsLoaderServiceWorker : BackgroundService
{
    private readonly IMicroEthosLogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public WorkflowComponentsLoaderServiceWorker(IMicroEthosLogger logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await LoadAppDomainModules();
                await Task.Delay(250000);
            }
        }
        catch (Exception e)
        {
            await _logger.Error("can not load workflow components in memory.", e);
        }
    }

    private async Task LoadAppDomainModules()
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var workflowService = scope.ServiceProvider.GetService<IWorkflowService>()!;
        var res= await workflowService.GetComponents();
        lock (WorkflowComponents.Items)
        {
            WorkflowComponents.Items = res.Data;
        }
    }
}