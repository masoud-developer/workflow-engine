namespace MicroEthos.Workflow.Abstraction.Constants;

public static class ModuleRoutes
{
    public const string WorkflowApiPrefix = "api/v1/workflow";
    public const string ModuleApiPrefix = "api/v1/module";
    public const string ModuleCreate = "create";
    public const string ModuleUnload = "unload";
    public const string ModuleList = "list";
    
    public const string WorkflowCreateOrUpdate = "create-or-update";
    public const string WorkflowMetaData = "metadata";
    public const string WorkflowRun = "run/{id:guid}/{version:int}";
    public const string WorkflowRunList = "run/list";
    public const string WorkflowRunCount = "run/count";
    public const string WorkflowRunDetail = "run/detail/{instanceId}";
    public const string WorkflowResume = "resume/{id:guid}/{version:int}";
    public const string WorkflowStop = "stop/{id:guid}/{version:int}";
    public const string WorkflowDelete = "delete/{id:guid}/{version:int}";
    public const string WorkflowPause = "pause/{id:guid}/{version:int}";
    public const string WorkflowPublishEvent = "publish-event";
    public const string WorkflowList = "list";
    public const string WorkflowDetails = "details/{id:guid}/{version:int}";
    public const string WorkflowVersions = "versions/{id:guid}";
    public const string WorkflowComponents = "components";
}