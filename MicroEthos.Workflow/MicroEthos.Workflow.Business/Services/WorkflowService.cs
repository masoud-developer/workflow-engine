using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models;
using MicroEthos.Workflow.Abstraction.Contracts.Providers;
using MicroEthos.Workflow.Abstraction.Contracts.Services;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Database;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Response;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using MicroEthos.Workflow.Business.Statics;
using MicroEthos.Workflow.Business.Workflow.Steps.Utils;
using MicroEthos.Workflow.DataAccess.Repository;
using MongoDB.Bson;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using NJsonSchema.Generation;
using WorkflowCore.Exceptions;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;
using WorkflowCore.Services.DefinitionStorage;
using WorkflowInstance = MicroEthos.Workflow.Abstraction.Models.Database.WorkflowInstance;

namespace MicroEthos.Workflow.Business.Services;

public class WorkflowService : IWorkflowService
{
    private readonly IDefinitionLoader _loader;
    private readonly IMicroEthosLogger _logger;
    private readonly IJsonService _jsonService;
    private readonly IRepository<Modules> _moduleRepo;
    private readonly IPool<IQueueClient> _queueConnectionPool;
    private readonly IWorkflowHost _workflowHost;
    private readonly GeneralRepository<WorkflowInstanceError> _workflowInstanceErrorRepo;
    private readonly GeneralRepository<WorkflowInstance> _workflowInstanceRepo;
    private readonly IRepository<WorkflowDefinitions> _workflowRepo;

    public WorkflowService(
        IDefinitionLoader loader,
        IMicroEthosLogger logger,
        IJsonService jsonService,
        IRepository<WorkflowDefinitions> workflowRepo,
        IRepository<Modules> moduleRepo,
        IWorkflowHost workflowHost,
        IPool<IQueueClient> queueConnectionPool,
        GeneralRepository<WorkflowInstanceError> workflowInstanceErrorRepo,
        GeneralRepository<WorkflowInstance> workflowInstanceRepo)
    {
        _loader = loader;
        _logger = logger;
        _jsonService = jsonService;
        _workflowHost = workflowHost;
        _queueConnectionPool = queueConnectionPool;
        _workflowRepo = workflowRepo;
        _moduleRepo = moduleRepo;
        _workflowInstanceRepo = workflowInstanceRepo;
        _workflowInstanceErrorRepo = workflowInstanceErrorRepo;
    }

    public async Task<OperationResult<bool>> PublishEvent(WorkflowEventDto model)
    {
        try
        {
            using (var connection = _queueConnectionPool.Acquire())
            {
                await connection.Enqueue("module-email-imap-response",
                    Encoding.UTF8.GetBytes(_jsonService.Serialize(new
                    {
                        model.ModuleId,
                        model.Command,
                        model.CreatedDate,
                        model.ServiceId,
                        model.InstitutionId,
                        model.JobId,
                        Payload = model.Payload.ToString()
                    })));
                return OperationResult<bool>.Success(true);
            }
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in publishing event", e);
            return OperationResult<bool>.Fail("Error occured in publishing event");
        }
    }

    public async Task<OperationResult<Guid>> Stop(Guid id, int version)
    {
        try
        {
            var workflowDef = await _workflowRepo.GetAsync(w => w.Model.Id == id && w.Model.Version == version);
            if (workflowDef == null || (!_workflowHost.Registry.IsRegistered(id.ToString(), version)
                                        && workflowDef.Status == WorkflowDefinitionStatus.Running))
                return OperationResult<Guid>.Fail("WORKFLOW_NOT_FOUND");

            var instances =
                await _workflowInstanceRepo.ListAsync(w => w.WorkflowDefinitionId == id.ToString() && w.Status != WorkflowInstanceStatus.Terminated, w => w.Data);
            foreach (var instance in instances)
                await _workflowHost.TerminateWorkflow(instance.Id);

            workflowDef.Status = WorkflowDefinitionStatus.Stop;
            await _workflowRepo.UpdateAsync(workflowDef.Id, workflowDef);
            return OperationResult<Guid>.Success(id);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in stop", e);
            return OperationResult<Guid>.Fail("Error occured in publishing event");
        }
    }

    public async Task<OperationResult<Guid>> Pause(Guid id, int version)
    {
        try
        {
            var workflowDef = await _workflowRepo.GetAsync(w => w.Model.Id == id && w.Model.Version == version);
            if (workflowDef == null || (!_workflowHost.Registry.IsRegistered(id.ToString(), version)
                                        && workflowDef.Status == WorkflowDefinitionStatus.Running))
                return OperationResult<Guid>.Fail("WORKFLOW_NOT_FOUND");

            var instances =
                await _workflowInstanceRepo.ListAsync(w => w.WorkflowDefinitionId == id.ToString(), w => w.Data);
            foreach (var instance in instances)
                await _workflowHost.SuspendWorkflow(instance.Id);

            workflowDef.Status = WorkflowDefinitionStatus.Pause;
            await _workflowRepo.UpdateAsync(workflowDef.Id, workflowDef);
            return OperationResult<Guid>.Success(id);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in stop", e);
            return OperationResult<Guid>.Fail("Error occured in publishing event");
        }
    }

    public async Task<OperationResult<Guid>> Resume(Guid id, int version)
    {
        try
        {
            var workflowDef = await _workflowRepo.GetAsync(w => w.Model.Id == id && w.Model.Version == version);
            if (workflowDef == null || (!_workflowHost.Registry.IsRegistered(id.ToString(), version)
                                        && workflowDef.Status == WorkflowDefinitionStatus.Pause))
                return OperationResult<Guid>.Fail("WORKFLOW_NOT_FOUND");

            await _workflowHost.ResumeWorkflow(id.ToString());
            workflowDef.Status = WorkflowDefinitionStatus.Running;
            await _workflowRepo.UpdateAsync(workflowDef.Id, workflowDef);
            return OperationResult<Guid>.Success(id);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in stop", e);
            return OperationResult<Guid>.Fail("Error occured in publishing event");
        }
    }

    public async Task<OperationResult<string>> Run(Guid id, int version, Dictionary<string, object>? initialData)
    {
        try
        {
            var workflowDef = await _workflowRepo.GetAsync(w => w.Model.Id == id && w.Model.Version == version);
            if (workflowDef == null || !_workflowHost.Registry.IsRegistered(id.ToString(), version))
                return OperationResult<string>.Fail("WORKFLOW_NOT_FOUND");

            var state = new StateDict
            {
                Items = new BsonDocument()
            };
            if (initialData != null && initialData.Count > 0)
                initialData.ToList().ForEach(data =>
                {
                    // var element = (JsonElement)data.Value;
                    state.Items[data.Key] = BsonValue.Create(data.Value.ToString()!);
                    // state.Items[data.Key] = BsonValue.Create(element.ValueKind == JsonValueKind.String ? $"\"{data.Value.ToString()!}\"" : data.Value.ToString()!);
                });

            var stateModel = new WorkflowStateModel
            {
                State = state,
                TraceId = Guid.NewGuid()
            };

            if (!IsEventStep(workflowDef.Model.Steps.First().StepType))
                await _workflowHost.StartWorkflow(id.ToString(), version, stateModel);

            workflowDef.Status = WorkflowDefinitionStatus.Running;
            workflowDef.RunParameters = stateModel.State;
            await _workflowRepo.UpdateAsync(workflowDef.Id, workflowDef);
            return OperationResult<string>.Success(id.ToString(), "workflow successfully running ...");
        }
        catch (Exception e)
        {
            await _logger.Error($"Error occured in running workflow with id {id}", e);
            return OperationResult<string>.Fail("Error occured in publishing event");
        }
    }

    public async Task<OperationResult<WorkflowDefinitionModel>> CreateOrUpdate(WorkflowDefinitionModel workflow)
    {
        try
        {
            // var valid = await ValidateWorkflow(workflow);
            // if (!valid)
            //     return OperationResult<string>.Fail();
            var oldVersionDefinitions = (await _workflowRepo.ListAsync(w => w.Model.Id == workflow.Id))
                .OrderByDescending(w => w.Model.Version).ToList();
            if (oldVersionDefinitions.Count > 0)
            {
                var lastVersion = oldVersionDefinitions.First();
                var lastVersionInstances = await _workflowInstanceRepo.ListAsync(ins =>
                    ins.WorkflowDefinitionId == lastVersion.Model.Id.ToString() &&
                    ins.Version == lastVersion.Model.Version);
                if (lastVersionInstances.Count > 0 && workflow.Action == null)
                    return OperationResult<WorkflowDefinitionModel>.Fail("ACTION_REQUIRED_WHEN_WORKFLOW_HAS_INSTANCE");

                workflow.Version = 1;
                if (lastVersionInstances.Count > 0 && workflow.Action == WorkflowAction.TerminateNow)
                {
                    //must be create new version
                    workflow.Version = lastVersion.Model.Version + 1;
                    foreach (var instance in lastVersionInstances)
                        await _workflowHost.TerminateWorkflow(instance.Id);
                    _workflowHost.Registry.DeregisterWorkflow(lastVersion.Model.Id.ToString(),
                        lastVersion.Model.Version);
                }

                else if (lastVersionInstances.Count > 0 &&
                         workflow.Action == WorkflowAction.TerminateAfterInProgressInstances)
                {
                    //must be create new version
                    workflow.Version = lastVersion.Model.Version + 1;
                    foreach (var instance in lastVersionInstances)
                        await _workflowHost.TerminateWorkflow(instance.Id);
                    _workflowHost.Registry.DeregisterWorkflow(lastVersion.Model.Id.ToString(),
                        lastVersion.Model.Version);
                }
                else
                {
                    //update last version
                    if (_workflowHost.Registry.IsRegistered(workflow.Id.ToString(), workflow.Version))
                        _workflowHost.Registry.DeregisterWorkflow(workflow.Id.ToString(), workflow.Version);

                    var updatedLastVersion = await CreateWorkflowDefinitionObject(lastVersion.Id, lastVersion.Model);
                    await _workflowRepo.UpdateAsync(updatedLastVersion.Id, updatedLastVersion);
                    return OperationResult<WorkflowDefinitionModel>.Success(workflow,
                        "workflow successfully updated ...");
                }
            }
            else
            {
                workflow.Version = 1;
            }

            workflow.Id = Guid.NewGuid();
            var definitionModel = await CreateWorkflowDefinitionObject(Guid.NewGuid(), workflow);
            await _workflowRepo.AddAsync(definitionModel);

            using var connection = _queueConnectionPool.Acquire();
            await connection.Enqueue(TopicNames.WorkflowDefinitionCreated, definitionModel);

            return OperationResult<WorkflowDefinitionModel>.Success(workflow, "workflow successfully created ...");
        }
        catch (WorkflowDefinitionLoadException e)
        {
            await _logger.Error("WorkflowDefinitionLoadException occured ...", e);
            return OperationResult<WorkflowDefinitionModel>.Fail(e.Message);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in creating workflow", e);
            return OperationResult<WorkflowDefinitionModel>.Fail("Error occured in creating workflow");
        }
    }

    public async Task<OperationResult<Guid>> UpdateMetaData(WorkflowMetaDataRequestModel model)
    {
        try
        {
            var workflow =
                await _workflowRepo.GetAsync(m => m.Model.Id == model.Id && m.Model.Version == model.Version);
            if (workflow == null)
                OperationResult<Guid>.Fail("WORKFLOW_NOT_FOUND");

            workflow.MetaData = model.MetaData;
            await _workflowRepo.UpdateAsync(workflow.Id, workflow);
            return OperationResult<Guid>.Success(workflow.Id, "workflow metadata successfully updated ...");
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in creating workflow", e);
            return OperationResult<Guid>.Fail("Error occured in creating workflow");
        }
    }

    public async Task<OperationResult<List<WorkflowComponentGroupResponseModel>>> GetComponents()
    {
        try
        {
            var allModules = await _moduleRepo.ListAsync(m => !m.Deprecated);
            var oldVersionModules = allModules
                .GroupBy(m => m.Name)
                .SelectMany(g => g.OrderByDescending(m => m.CreatedAt).Skip(1))
                .ToList();

            //get steps from appDomain assemblies expect modules with old versions
            var steps = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract
                                      && t.IsAssignableTo(typeof(StepBodyAsync))
                                      && t.GetCustomAttributes(typeof(StepModuleAttribute)).Any()
                                      && oldVersionModules.All(m =>
                                          !t.Assembly.FullName!.Contains($"{m.Name}_{m.Version}")))
                .ToList();

            var result = steps
                .GroupBy(g =>
                {
                    var dynamicModuleVersion =
                        allModules.FirstOrDefault(m => m.AssemblyName == g.Assembly.GetName().Name)?.Version
                            .Replace(".", "");
                    return ((StepModuleAttribute)g.GetCustomAttributes(typeof(StepModuleAttribute)).First()).Module +
                           (dynamicModuleVersion ?? string.Empty);
                })
                .Select(s => new WorkflowComponentGroupResponseModel
                {
                    Name = $"{s.Key}".TrimStart('I'),
                    Components = s.Select(c => new WorkflowComponentResponseModel
                    {
                        Id = c.Name,
                        DataType = $"{c.FullName}, {c.Assembly.GetName().Name}",
                        Inputs = GenerateInputOutputSchema(c, true),
                        Outputs = GenerateInputOutputSchema(c, false),
                        Type = ((StepModuleAttribute)c.GetCustomAttributes(typeof(StepModuleAttribute)).First()).Type
                    }).ToList()
                }).ToList();

            result.First(s => s.Name == "Primitives").Components.AddRange(AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract
                                      && t.IsAssignableTo(typeof(ContainerStepBody))
                                      && !string.IsNullOrWhiteSpace(t.Namespace)
                                      && t.Namespace.Contains("WorkflowCore.Primitives"))
                .Select(item =>
                {
                    var primitiveInput = GetPrimitivesInputs(item);
                    var primitiveOutput = GetPrimitivesOutputs(item);
                    if (primitiveInput == null)
                        return null;
                    return new WorkflowComponentResponseModel
                    {
                        Id = item.Name,
                        DataType = $"{item.FullName}, {item.Assembly.GetName().Name}",
                        Inputs = primitiveInput,
                        Outputs = primitiveOutput ?? new JObject(),
                        Type = StepType.Function
                    };
                }).Where(item => item != null).ToList()!);

            return OperationResult<List<WorkflowComponentGroupResponseModel>>.Success(result);
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured in get components", e);
            return OperationResult<List<WorkflowComponentGroupResponseModel>>.Fail("Error occured in get components");
        }
    }

    public async Task<OperationResult<List<WorkflowComponentGroupResponseModel>>> GetInMemoryComponents()
    {
        try
        {
            lock (WorkflowComponents.Items)
            {
                return OperationResult<List<WorkflowComponentGroupResponseModel>>.Success(WorkflowComponents.Items);
            }
        }
        catch (Exception e)
        {
            await _logger.Error("Error occured", e);
            return OperationResult<List<WorkflowComponentGroupResponseModel>>.Fail();
        }
    }

    public async Task<OperationResult<PaginationModel<WorkflowDefinitionResponseModel>>> List()
    {
        try
        {
            var workflows = await _workflowRepo.ListAsync(null, excludes: w => w.MetaData);
            return OperationResult<PaginationModel<WorkflowDefinitionResponseModel>>.Success(
                new PaginationModel<WorkflowDefinitionResponseModel>
                {
                    Items = workflows.Select(async s =>
                    {
                        var instanceCount = await _workflowInstanceRepo.CountAsync(ins =>
                            ins.WorkflowDefinitionId == s.Model.Id.ToString() &&
                            ins.Version == s.Model.Version);
                        return new WorkflowDefinitionResponseModel
                        {
                            Id = s.Model.Id,
                            Version = s.Model.Version,
                            Author = "masoud",
                            Name = s.Model.Name,
                            StepCount = s.Model.Steps.Count,
                            Created = s.CreatedAt,
                            Updated = s.UpdatedAt,
                            Status = s.Status,
                            RunInstanceCount = instanceCount
                        };
                    }).Select(s => s.Result).ToList(),
                    PageNumber = 1,
                    PageSize = 1000,
                    TotalCount = workflows.Count
                });
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<PaginationModel<WorkflowDefinitionResponseModel>>.Fail(
                "Error occured when get workflows.");
        }
    }

    public async Task<OperationResult<PaginationModel<WorkflowRunInstanceResponseModel>>> RunList()
    {
        try
        {
            var instances = await _workflowInstanceRepo.ListAsync();
            instances = instances.OrderByDescending(i => i.CreateTime).ToList();
            var definitions = await _workflowRepo.ListAsync();
            var result = instances.Select(s => new WorkflowRunInstanceResponseModel
            {
                Id = s.Id,
                DefinitionName = definitions.FirstOrDefault(d =>
                        d.Model.Id == new Guid(s.WorkflowDefinitionId) && s.Version == d.Model.Version)?
                    .Model.Name,
                DefinitionId = s.WorkflowDefinitionId,
                Version = s.Version,
                CreateTime = s.CreateTime,
                CompleteTime = s.CompleteTime,
                NextExecution = s.NextExecution,
                Description = s.Description,
                Status = s.Status,
                TraceId = s.Data["TraceId"]?.ToString() ?? string.Empty
            }).ToList();
            var paginated = new PaginationModel<WorkflowRunInstanceResponseModel>
            {
                Items = result,
                PageNumber = 1,
                PageSize = 10000,
                TotalCount = result.Count
            };
            return OperationResult<PaginationModel<WorkflowRunInstanceResponseModel>>.Success(paginated);
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<PaginationModel<WorkflowRunInstanceResponseModel>>.Fail();
        }
    }

    public async Task<OperationResult<long>> RunInstanceCount(RunInstanceCountRequestModel model)
    {
        try
        {
            var from = DateTime.UtcNow.Subtract(model.From);
            var count = await _workflowInstanceRepo.CountAsync(w => w.CreateTime >= from &&
                                                                    (model.Type == null || w.Status == model.Type));
            return OperationResult<long>.Success(count);
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<long>.Fail();
        }
    }

    public async Task<OperationResult<WorkflowRunInstanceResponseModel>> RunInstanceDetail(string runInstanceId)
    {
        try
        {
            var instance = await _workflowHost.PersistenceStore.GetWorkflowInstance(runInstanceId);
            if (instance == null)
                return OperationResult<WorkflowRunInstanceResponseModel>.Fail("NOT_FOUND");

            var instanceStateData = (WorkflowStateModel)instance.Data;
            var definition = await _workflowRepo.GetAsync(w => w.Model.Id == new Guid(instance.WorkflowDefinitionId)
                                                               && w.Model.Version == instance.Version);
            if (definition == null)
                return OperationResult<WorkflowRunInstanceResponseModel>.Fail("NOT_FOUND");

            var instanceErrors = await _workflowInstanceErrorRepo.ListAsync(w => w.WorkflowId == runInstanceId);
            instanceErrors = instanceErrors.OrderByDescending(i => i.ErrorTime).ToList();
            var result = OperationResult<WorkflowRunInstanceResponseModel>.Success(new WorkflowRunInstanceResponseModel
            {
                Id = instance.Id,
                TraceId = instanceStateData.TraceId.ToString(),
                DefinitionId = instance.WorkflowDefinitionId,
                DefinitionName = definition.Model.Name,
                Version = instance.Version,
                CreateTime = instance.CreateTime,
                CompleteTime = instance.CompleteTime,
                NextExecution = instance.NextExecution,
                Description = instance.Description,
                Status = (WorkflowInstanceStatus)instance.Status,
                MetaData = definition?.MetaData,
                Steps = instance.ExecutionPointers.Select(s => new WorkflowInstanceStepDetailResponseModel
                {
                    Active = s.Active,
                    Children = s.Children,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Id = s.Id,
                    SleepUntil = s.SleepUntil,
                    RetryCount = s.RetryCount,
                    Status = (WorkflowInstanceStepPointerStatus)s.Status,
                    ContextItem = s.ContextItem,
                    EventData = s.EventData,
                    StepName = s.StepName,
                    StepId = s.StepId,
                    Data = s.ExtensionAttributes.Where(ex => ex.Key.Contains("$$"))
                        .ToDictionary(d => d.Key, d => d.Value),
                    Logs = instanceErrors.Where(l => l.ExecutionPointerId == s.Id).Select(e =>
                        new WorkflowInstanceLogModel
                        {
                            Type = WorkflowInstanceLogType.Error,
                            Time = e.ErrorTime,
                            Message = e.Message
                        }).ToList()
                }).ToList()
            });
            return result;
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<WorkflowRunInstanceResponseModel>.Fail();
        }
    }

    public async Task<OperationResult<WorkflowDetailResponseModel>> Get(Guid id, int version)
    {
        try
        {
            var workflowDefinition = await _workflowRepo.GetAsync(w => w.Model.Id == id && w.Model.Version == version);
            if (workflowDefinition == null)
                return OperationResult<WorkflowDetailResponseModel>.Fail("WORKFLOW_NOT_FOUND");

            var workflowInstanceCount = await _workflowInstanceRepo.CountAsync(ins =>
                ins.WorkflowDefinitionId == id.ToString() &&
                ins.Version == version);

            return OperationResult<WorkflowDetailResponseModel>.Success(new WorkflowDetailResponseModel
            {
                Id = workflowDefinition.Model.Id,
                Author = "masoud",
                CreatedAt = workflowDefinition.CreatedAt,
                MetaData = workflowDefinition.MetaData,
                Name = workflowDefinition.Model.Name,
                Version = workflowDefinition.Model.Version,
                Steps = workflowDefinition.Model.Steps,
                RunInstanceCount = workflowInstanceCount,
                Status = workflowDefinition.Status
            });
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<WorkflowDetailResponseModel>.Fail(
                "Error occured when get workflows.");
        }
    }

    public async Task<OperationResult<List<WorkflowVersionResponseModel>>> GetVersions(Guid id)
    {
        try
        {
            var workflowDefinitions = await _workflowRepo.ListAsync(w => w.Model.Id == id);
            var versions = workflowDefinitions.Select(s => new WorkflowVersionResponseModel
            {
                Id = s.Model.Id,
                Version = s.Model.Version,
                Name = s.Model.Name,
                VersionId = s.Id
            }).ToList();
            return OperationResult<List<WorkflowVersionResponseModel>>.Success(versions);
        }
        catch (Exception e)
        {
            await _logger.Error("Error Occured ...", e);
            return OperationResult<List<WorkflowVersionResponseModel>>.Fail();
        }
    }

    public async Task LoadAll()
    {
        var workflows = await _workflowRepo.ListAsync(null, excludes: w => w.MetaData);
        workflows.ForEach(w =>
        {
            try
            {
                if (!_workflowHost.Registry.IsRegistered(w.Model.Id.ToString(), w.Model.Version))
                    _loader.LoadDefinition(w.Raw, Deserializers.Json);
            }
            catch (Exception e)
            {
                _logger.Error($"can not load workflow {w.Model?.Name}", e);
            }
        });
    }

    private bool ValidateSteps(List<WorkflowStepDefinitionModel> steps)
    {
        //check duplicate id in steps
        if (steps.GroupBy(s => s.Id).Count() != steps.Count)
            return false;

        foreach (var step in steps)
        {
            if (!ValidateOutputAndInputType(step, steps))
                return false;
            if (step.Do != null && step.Do.Count > 0)
                foreach (var doSteps in step.Do)
                    if (!ValidateSteps(doSteps))
                        return false;
        }

        return true;
    }

    #region Private Methods

    private bool IsEventStep(string stepType)
    {
        var moduleAttr = Utils.GetStepType(stepType)
            .GetCustomAttributes(typeof(StepModuleAttribute)).FirstOrDefault();
        if (moduleAttr == null)
            return false;
        return ((StepModuleAttribute)moduleAttr).Type == StepType.Event;
    }

    private async Task<WorkflowDefinitions> CreateWorkflowDefinitionObject(Guid id, WorkflowDefinitionModel workflow)
    {
        var obj = JObject.FromObject(workflow, new JsonSerializer
        {
            NullValueHandling = NullValueHandling.Ignore
        });
        obj.Add("DataType",
            "MicroEthos.Workflow.Abstraction.Models.Workflow.WorkflowStateModel, MicroEthos.Workflow.Abstraction");
        await ParseAndGenerateCorrectExpressions(obj["Steps"] as JArray);

        var workflowJson = obj.ToString();
        var result = Regex.Replace(
            workflowJson,
            @"(?<!\[)(?<!\\)""[$]{2}[^""]{1,}""",
            match => $"\"State[\\\"{match.Value.Trim('\"')}\\\"]\"");
        var def = _loader.LoadDefinition(result, Deserializers.Json);
        workflow.Steps.ForEach(step => { step.Name = step.Id; });
        return new WorkflowDefinitions
        {
            Id = id,
            Model = workflow,
            Raw = result,
            Status = WorkflowDefinitionStatus.Stop,
            MetaData = workflow.MetaData
        };
    }

    private JObject GenerateInputOutputSchema(Type type, bool isInput)
    {
        var res = new JObject();
        type.GetProperties()
            .Where(inp => inp.GetCustomAttributes(isInput ? typeof(StepInputAttribute) : typeof(StepOutputAttribute))
                .Any())
            .ToList()
            .ForEach(inp =>
            {
                var schemaStr = JsonSchema.FromType(inp.PropertyType, new JsonSchemaGeneratorSettings
                {
                    // SerializerSettings = new JsonSerializerSettings
                    // {
                    //     ContractResolver = new CamelCasePropertyNamesContractResolver()
                    // }
                    FlattenInheritanceHierarchy = true,
                    DefaultReferenceTypeNullHandling = ReferenceTypeNullHandling.NotNull,
                }).ToJson();

                res.Add(inp.Name, JToken.Parse(schemaStr));
            });

        return res;
    }

    private async Task<bool> ValidateWorkflow(WorkflowDefinitionModel model)
    {
        try
        {
            if (!ValidateSteps(model.Steps))
                return false;
            return true;
        }
        catch (Exception e)
        {
            await _logger.Error("error occured ...", e);
            return false;
        }
    }

    private async Task ParseAndGenerateCorrectExpressions(JArray steps)
    {
        var clonedSteps = steps.DeepClone();
        foreach (var (clonedStep, index) in clonedSteps.Select((step, index) => (step, index)))
        {
            var step = steps[index];
            if (step["SelectNextStep"]?.HasValues ?? false)
                foreach (var prop in (step["SelectNextStep"] as JObject).Properties())
                    step["SelectNextStep"][prop.Name] =
                        await ParseConditionAndCreateCorrectCondition(prop.Value.ToString());

            if (step["StepType"].ToString() == "WorkflowCore.Primitives.If, WorkflowCore")
            {
                if (step["Inputs"]?.HasValues ?? false)
                    step["Inputs"]["Condition"] =
                        await ParseConditionAndCreateCorrectCondition(step["Inputs"]?["Condition"].ToString());

                if (step["Do"]?.HasValues ?? false)
                    foreach (var subSteps in step["Do"] as JArray)
                        if (subSteps.HasValues && subSteps is JArray array)
                            await ParseAndGenerateCorrectExpressions(array);
            }

            // if (step["StepType"].ToString().Contains(".Dynamic."))
            // {
            //     var typeSections = step["StepType"].ToString().Split(',').Select(s => s.Trim()).ToArray();
            //     var stepTypeName = typeSections[0];
            //     var stepAssemblyName = typeSections[1];
            //     var stepType = AppDomain.CurrentDomain.GetAssemblies()
            //         .FirstOrDefault(s => s.GetName().Name == stepAssemblyName)
            //         .GetTypes().FirstOrDefault(s => s.FullName == stepTypeName);
            //
            //     var input = stepType.GetProperties().Where(p =>
            //         p.GetCustomAttributes(typeof(StepInputAttribute), false).Any()).FirstOrDefault();
            //
            //     var waitStep = JObject.Parse(_jsonService.Serialize(new
            //     {
            //         Id = $"WaitFor_{step["Id"]}",
            //         StepType = "WorkflowCore.Primitives.WaitFor, WorkflowCore",
            //         NextStepId = step["NextStepId"].ToString(),
            //         Inputs = new Dictionary<string, string>
            //         {
            //             ["EventName"] = $"data.State.Items[\"$${step["Id"]}_JobId\"].ToString()",
            //             ["EventKey"] = $"data.State.Items[\"$${step["Id"]}_JobId\"].ToString()"
            //         }
            //     }));
            //     if (input != null)
            //     {
            //         var output = new JObject { { $"$${step["Id"]}_{input.Name}_In", "step.EventData" } };
            //         waitStep["Outputs"] = output;
            //     }
            //
            //     step.AddAfterSelf(waitStep);
            //     step["NextStepId"] = waitStep["Id"];
            // }

            // if (step["Inputs"]?.HasValues ?? false)
            // {
            //     if (step["Inputs"] is JObject inputs)
            //         foreach (var input in inputs.Properties().Where(p => p.Value?.ToString()?.Contains("$$") ?? false))
            //         {
            //             var stepType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes())
            //                 .First(s => s.FullName == step["StepType"].ToString().Split('.')[0]);
            //
            //             var inputType = stepType.GetProperties().FirstOrDefault(p =>
            //                 p.Name.Equals(input.Name, StringComparison.OrdinalIgnoreCase))?.PropertyType;
            //             
            //             if(inputType.IsPrimitive)
            //                input.Value = await ParseConditionAndCreateCorrectCondition(input.Value.ToString());
            //             else if (inputType.IsClass)
            //                 input.Value = await ParseInputAndCreateCorrectInput(inputType, input.Value.ToString());
            //         }
            // }
        }
    }

    private Type GetParameterType(string stepTypeName, string property, bool input)
    {
        var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(s => s.GetTypes());
        var stepType = types.FirstOrDefault(t =>
            t.FullName == stepTypeName && t.GetCustomAttributes(typeof(StepModuleAttribute)).Any());

        var propType = stepType.GetProperties()
            .Where(inp => inp.GetCustomAttributes(input ? typeof(StepInputAttribute) : typeof(StepOutputAttribute))
                .Any())
            .ToList()
            .FirstOrDefault(t => t.Name.Equals(property, StringComparison.OrdinalIgnoreCase))
            .PropertyType;

        return propType;
    }

    private async Task<string> ParseConditionAndCreateCorrectCondition(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "";
        var allSteps = (await GetComponents()).Data?.SelectMany(s => s.Components);

        var exItems = expression.Split(' ')
            .Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        var result = string.Empty;
        var operators = new[] { "+", "-", "%", "==", "*", "/", "&&", "||" };

        foreach (var exItem in exItems)
            if (operators.Contains(exItem))
            {
                result += $"{exItem} ";
            }
            else if (exItem.StartsWith("$"))
            {
                var props = exItem.Split('.');
                var keySections = props[0].TrimStart('$').Split('_');
                var stepId = keySections[0];
                var propName = keySections[1];
                var inputOrOutProp = keySections[2];

                JToken selectedProp = null;
                if (inputOrOutProp.ToLower() == "out")
                    selectedProp = allSteps.FirstOrDefault(s => s.Id == stepId).Outputs[propName];
                else if (inputOrOutProp.ToLower() == "in")
                    selectedProp = allSteps.FirstOrDefault(s => s.Id == stepId).Inputs[propName];

                var schema = await JsonSchema.FromJsonAsync(selectedProp?.ToString() ?? string.Empty);

                if (props.Length == 1)
                {
                    result += GenerateConvertLiteral(schema.Type.ToString().ToLower());
                }
                else
                {
                    for (var i = 1; i < props.Length - 1; i++)
                        schema = schema.Properties[props[i]].Reference;
                    // if(i == props.Length - 1)
                    //     selectedProp = selectedProp["properties"][props[i]];
                    // else
                    //     selectedProp = selectedProp["properties"][props[i]];


                    // result += GenerateConvertLiteral(selectedProp["type"]
                    //     .ToString());
                    result += GenerateConvertLiteral(schema.Properties[props[props.Length - 1]].Type.ToString()
                        .ToLower());
                }

                result += "(";
                result += "data.State.Items";
                if (props.Length == 1)
                    result += $"[\"{props[0]}\"]";
                else
                    result += props.Select(a => $"[\"{a}\"]").Aggregate((a, b) => $"{a}{b}");

                result += ") ";
            }
            else
            {
                result += $"{exItem} ";
            }

        return result.Trim();
    }

    private bool ValidateOutputAndInputType(WorkflowStepDefinitionModel currentStep,
        List<WorkflowStepDefinitionModel> allSteps)
    {
        foreach (var output in currentStep.Outputs)
        {
            if (!output.Key.StartsWith("$$") && !output.Key.EndsWith("In"))
                continue;

            var assignSections = output.Key.Substring(2).Split("_");
            var nextStepId = assignSections[0];
            var nextStepAssignedInputProperty = assignSections[1];
            var isInput = assignSections[2] == "In";

            var nextStep = allSteps.FirstOrDefault(s => s.Id == nextStepId);
            if (nextStep == null)
            {
                _logger.Error($"Can not find step with id {nextStepId} for assign output to it.");
                return false;
            }

            var nextStepInputType =
                GetParameterType(nextStep.StepType.Split(',')[0], nextStepAssignedInputProperty, true);
            var nextStepInputSchema = JsonSchema.FromType(nextStepInputType);

            var currentStepOutputSections = output.Value.Split('.').Skip(1).ToArray();
            var currentStepOutputType =
                GetParameterType(currentStep.StepType.Split(',')[0], currentStepOutputSections[0], false);
            var currentStepOutputSchema = JsonSchema.FromType(currentStepOutputType);
            if (currentStepOutputSections.Length > 1)
                for (var i = 1; i < currentStepOutputSections.Length - 1; i++)
                    currentStepOutputSchema =
                        currentStepOutputSchema.Properties[currentStepOutputSections[i]].Reference;

            return !nextStepInputSchema.Validate(currentStepOutputSchema.ToSampleJson()).Any();
        }

        return true;
    }

    private string GenerateConvertLiteral(string type)
    {
        switch (type.ToLower())
        {
            case "string":
                return "Convert.ToString";
            case "integer":
                return "Convert.ToInt32";
            case "long":
                return "Convert.ToInt64";
            case "short":
                return "Convert.ToInt16";
            case "double":
                return "Convert.ToDouble";
            case "number":
                return "Convert.ToDouble";
            case "datetime":
                return "Convert.ToDateTime";
            case "date":
                return "Convert.ToDateTime";
            case "time":
                return "Convert.ToDateTime";
            case "float":
                return "Convert.ToDecimal";
            case "decimal":
                return "Convert.ToDecimal";
            case "boolean":
                return "Convert.ToBoolean";
            case "bool":
                return "Convert.ToBoolean";
            default:
                return "";
        }
    }

    private JObject? GetPrimitivesInputs(Type primitiveType)
    {
        var inputObject = new JObject();
        var serializeSetting = new JsonSchemaGeneratorSettings
        {
            SerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        };
        switch (primitiveType.Name)
        {
            case nameof(If):
                inputObject.Add("Condition",
                    JToken.Parse(JsonSchema.FromType(typeof(bool), serializeSetting).ToJson()));
                break;
            case nameof(Foreach):
                inputObject.Add("Collection",
                    JToken.Parse(JsonSchema.FromType(typeof(object[]), serializeSetting).ToJson()));
                break;
            case nameof(While):
                inputObject.Add("Condition",
                    JToken.Parse(JsonSchema.FromType(typeof(bool), serializeSetting).ToJson()));
                break;
            case nameof(Schedule):
                inputObject.Add("Interval", JToken.Parse(JsonSchema.FromType(typeof(int), serializeSetting).ToJson()));
                break;
            default:
                return null;
        }

        return inputObject;
    }

    private JObject? GetPrimitivesOutputs(Type primitiveType)
    {
        var inputObject = new JObject();
        var serializeSetting = new JsonSchemaGeneratorSettings
        {
            SerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        };
        switch (primitiveType.Name)
        {
            case nameof(Foreach):
                inputObject.Add("Item",
                    JToken.Parse(JsonSchema.FromType(typeof(object), serializeSetting).ToJson()));
                break;
            default:
                return null;
        }

        return inputObject;
    }

    #endregion
}