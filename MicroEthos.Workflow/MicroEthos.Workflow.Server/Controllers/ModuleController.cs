using MicroEthos.Common.Endpoints.Base;
using MicroEthos.Common.Models;
using MicroEthos.Workflow.Abstraction.Constants;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace MicroEthos.Workflow.Server.Controllers;

[ApiController]
[Route(ModuleRoutes.ModuleApiPrefix)]
public class ModuleController : MicroEthosBaseController
{
    private readonly IModuleService _moduleService;

    public ModuleController(IModuleService moduleService)
    {
        _moduleService = moduleService;
    }

    [HttpPost(ModuleRoutes.ModuleCreate)]
    public async Task<OperationResult<Guid>> Create(WorkflowAddModuleModel model)
    {
        return await _moduleService.CreateAndLoadDynamically(model);
    }

    [HttpPost(ModuleRoutes.ModuleUnload)]
    public async Task<OperationResult<bool>> CreateModule(UnloadModuleRequest model)
    {
        return await _moduleService.Unload(model.Name, model.Version);
    }
    
    [HttpPost(ModuleRoutes.ModuleList)]
    public async Task<OperationResult<List<WorkflowModuleResponse>>> List()
    {
        return await _moduleService.List();
    }
}