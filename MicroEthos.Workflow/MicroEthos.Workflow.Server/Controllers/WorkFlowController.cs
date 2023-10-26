using MicroEthos.Common.Contracts;
using MicroEthos.Common.Endpoints.Base;
using MicroEthos.Common.Models;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace MicroEthos.Workflow.Server.Controllers;

[ApiController]
[Route(ModuleRoutes.WorkflowApiPrefix)]
public class WorkFlowController : MicroEthosBaseController
{
    private readonly IWorkflowService _workflowService;
    private readonly IJsonService _jsonService;

    public WorkFlowController(IWorkflowService workflowService, IJsonService jsonService)
    {
        _workflowService = workflowService;
        _jsonService = jsonService;
    }

    [HttpPost(ModuleRoutes.WorkflowCreateOrUpdate)]
    public async Task<OperationResult<WorkflowDefinitionModel>> Create([FromBody] WorkflowDefinitionModel model)
    {
        return await _workflowService.CreateOrUpdate(model);
    }

    [HttpPost(ModuleRoutes.WorkflowMetaData)]
    public async Task<OperationResult<Guid>> Create([FromBody] WorkflowMetaDataRequestModel model)
    {
        return await _workflowService.UpdateMetaData(model);
    }

    [HttpPost(ModuleRoutes.WorkflowRun)]
    public async Task<OperationResult<string>> Run(Guid id, int version,
        [FromBody] Dictionary<string, object> initializeData)
    {
        return await _workflowService.Run(id, version, initializeData);
    }

    [HttpPost(ModuleRoutes.WorkflowRunList)]
    public async Task<OperationResult<PaginationModel<WorkflowRunInstanceResponseModel>>> RunList()
    {
        return await _workflowService.RunList();
    }
    
    [HttpPost(ModuleRoutes.WorkflowRunCount)]
    public async Task<OperationResult<long>> RunCount(RunInstanceCountRequestModel model)
    {
        return await _workflowService.RunInstanceCount(model);
    }

    [HttpGet(ModuleRoutes.WorkflowRunDetail)]
    public async Task<OperationResult<WorkflowRunInstanceResponseModel>> GetRunDetail(string instanceId)
    {
        return await _workflowService.RunInstanceDetail(instanceId);
    }

    [HttpPost(ModuleRoutes.WorkflowResume)]
    public async Task<OperationResult<Guid>> Resume(Guid id, int version)
    {
        return await _workflowService.Resume(id, version);
    }

    [HttpPost(ModuleRoutes.WorkflowStop)]
    public async Task<OperationResult<Guid>> Stop(Guid id, int version)
    {
        return await _workflowService.Stop(id, version);
    }

    [HttpPost(ModuleRoutes.WorkflowDelete)]
    public async Task<OperationResult<Guid>> Delete(Guid id, int version)
    {
        return OperationResult<Guid>.Success(id);
    }

    [HttpPost(ModuleRoutes.WorkflowPause)]
    public async Task<OperationResult<Guid>> Pause(Guid id, int version)
    {
        return await _workflowService.Pause(id, version);
    }

    [HttpPost(ModuleRoutes.WorkflowPublishEvent)]
    public async Task<OperationResult<bool>> PublishEvent(WorkflowEventDto model)
    {
        return await _workflowService.PublishEvent(model);
    }

    [HttpGet(ModuleRoutes.WorkflowList)]
    public async Task<OperationResult<PaginationModel<WorkflowDefinitionResponseModel>>> List()
    {
        return await _workflowService.List();
    }

    [HttpGet(ModuleRoutes.WorkflowDetails)]
    public async Task<OperationResult<WorkflowDetailResponseModel>> Details(Guid id, int version)
    {
        return await _workflowService.Get(id, version);
    }

    [HttpGet(ModuleRoutes.WorkflowVersions)]
    public async Task<OperationResult<List<WorkflowVersionResponseModel>>> Versions(Guid id)
    {
        return await _workflowService.GetVersions(id);
    }

    [HttpGet(ModuleRoutes.WorkflowComponents)]
    public async Task<IActionResult> GetAllModules()
    {
        var data = _jsonService.Serialize(await _workflowService.GetInMemoryComponents());
        return Content(data, new MediaTypeHeaderValue("application/json"));
    }
}