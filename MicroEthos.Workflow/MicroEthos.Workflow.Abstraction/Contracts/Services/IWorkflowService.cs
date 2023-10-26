using MicroEthos.Common.Models;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;

namespace MicroEthos.Workflow.Abstraction.Contracts.Services;

public interface IWorkflowService
{
    Task<OperationResult<bool>> PublishEvent(WorkflowEventDto model);
    Task<OperationResult<Guid>> Stop(Guid id, int version);
    Task<OperationResult<Guid>> Pause(Guid id, int version);
    Task<OperationResult<Guid>> Resume(Guid id, int version);
    Task<OperationResult<List<WorkflowComponentGroupResponseModel>>> GetComponents();
    Task<OperationResult<List<WorkflowComponentGroupResponseModel>>> GetInMemoryComponents();
    Task<OperationResult<WorkflowDefinitionModel>> CreateOrUpdate(WorkflowDefinitionModel workflow);
    Task<OperationResult<string>> Run(Guid id, int version, Dictionary<string, object>? initialData);
    Task<OperationResult<PaginationModel<WorkflowRunInstanceResponseModel>>> RunList();
    Task<OperationResult<WorkflowRunInstanceResponseModel>> RunInstanceDetail(string runInstanceId);
    Task<OperationResult<PaginationModel<WorkflowDefinitionResponseModel>>> List();
    Task<OperationResult<WorkflowDetailResponseModel>> Get(Guid id, int version);
    Task<OperationResult<List<WorkflowVersionResponseModel>>> GetVersions(Guid id);
    Task<OperationResult<long>> RunInstanceCount(RunInstanceCountRequestModel model);
    Task LoadAll();
    Task<OperationResult<Guid>> UpdateMetaData(WorkflowMetaDataRequestModel model);
}