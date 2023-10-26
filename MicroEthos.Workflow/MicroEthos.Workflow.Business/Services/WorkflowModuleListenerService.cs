using System.Reflection;
using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models.Enums;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using MicroEthos.Workflow.Business.Workflow.Steps.Utils;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using WorkflowCore.Interface;

namespace MicroEthos.Workflow.Business.Services;

public class WorkflowModuleListenerService
{
    private readonly IJsonService _jsonService;
    private readonly IMicroEthosLogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IWorkflowHost _workflowHost;

    public WorkflowModuleListenerService(IMicroEthosLogger logger,
        IWorkflowHost workflowHost,
        IServiceProvider serviceProvider,
        IJsonService jsonService)
    {
        _logger = logger;
        _workflowHost = workflowHost;
        _serviceProvider = serviceProvider;
        _jsonService = jsonService;
    }

    public async Task<bool> ModuleResponseReceived(string moduleResp, Modules module)
    {
        var moduleRespObj = _jsonService.Deserialize<WorkflowEventDto>(moduleResp);
        if (moduleRespObj == null ||
            string.IsNullOrWhiteSpace(moduleRespObj.Command))
        {
            await _logger.Error("response of module received with invalid data");
            return true;
        }

        await _logger.Info(
            $"Response of module {moduleRespObj.ModuleId} and action {moduleRespObj.Command} received with payload {moduleRespObj.Payload}\n" +
            $"Job id is {moduleRespObj.JobId}\n" +
            $"Trace Id is {moduleRespObj.TraceId}");
        var payload = _jsonService.Serialize(moduleRespObj.Payload, SerializationType.CamelCase);
        await _workflowHost.PublishEvent(moduleRespObj.JobId, moduleRespObj.JobId, payload,
            DateTime.Now);
        return true;
    }

    public async Task<bool> ModuleEventRaised(string eventMsg, Modules module)
    {
        var eventMsgObj = _jsonService.Deserialize<WorkflowEventDto>(eventMsg);
        if (eventMsgObj == null ||
            string.IsNullOrWhiteSpace(eventMsgObj.Command))
        {
            await _logger.Error("event received with invalid data");
            return true;
        }

        await _logger.Info(
            $"{eventMsgObj.Command} event raised from module {eventMsgObj.ModuleId} with payload {eventMsgObj.Payload}\n" +
            $"Job id is {eventMsgObj.JobId}");

        await using var scope = _serviceProvider.CreateAsyncScope();
        var runningWorkflows = await scope.ServiceProvider.GetRequiredService<IRepository<WorkflowDefinitions>>()
            .ListAsync(w => w.Status == WorkflowDefinitionStatus.Running); //&& w.Model.InstitutionId == eventMsgObj.InstitutionId && w.Model.ServiceId == eventMsgObj.ServiceId);

        var mustBeInitializeWorkflows = runningWorkflows.Where(w => w.Model.Steps.Count > 0
                                                                    && w.Model.Steps[0].StepType
                                                                        .Contains(module.AssemblyName) &&
                                                                    w.Model.Steps[0].StepType.ToLower()
                                                                        .Contains(eventMsgObj.Command.ToLower()))
            .ToList();

        var eventOutPropName = string.Empty;
        if (mustBeInitializeWorkflows.Count > 0)
        {
            var outProp = Utils.GetStepType(mustBeInitializeWorkflows.First().Model!.Steps.First().StepType)
                .GetProperties().FirstOrDefault(p => p.GetCustomAttributes(typeof(StepOutputAttribute)).Any());
            if (outProp == null)
                return true;
            eventOutPropName = outProp.Name;
        }

        foreach (var mustBeInitializeWorkflow in mustBeInitializeWorkflows)
        {
            mustBeInitializeWorkflow.RunParameters.Items[
                    $"$${mustBeInitializeWorkflow.Model!.Steps[0].Id}_{eventOutPropName}_Out"] =
                BsonValue.Create(_jsonService.Serialize(eventMsgObj.Payload));

            await _workflowHost.StartWorkflow(mustBeInitializeWorkflow.Model!.Id.ToString(),
                mustBeInitializeWorkflow.Model.Version, new WorkflowStateModel
                {
                    State = mustBeInitializeWorkflow.RunParameters,
                    TraceId = string.IsNullOrEmpty(eventMsgObj.TraceId) || 
                              eventMsgObj.TraceId == Guid.Empty.ToString() ? Guid.NewGuid() : new Guid(eventMsgObj.TraceId),
                    InstitutionId = new Guid(eventMsgObj.InstitutionId),
                    ServiceId = new Guid(eventMsgObj.ServiceId),
                    UserId = string.IsNullOrEmpty(eventMsgObj.UserId) ? null : new Guid(eventMsgObj.ServiceId)
                });
        }
        
        var payload = _jsonService.Serialize(eventMsgObj.Payload, SerializationType.CamelCase);
        await _workflowHost.PublishEvent(eventMsgObj.Command,
            module.Name, payload, DateTime.Now.AddSeconds(2));

        return true;
    }
}