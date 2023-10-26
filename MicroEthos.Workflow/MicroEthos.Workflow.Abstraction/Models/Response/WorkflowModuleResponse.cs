namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowModuleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public string Version { get; set; }
    public string RequestQueueName { get; set; }
    public string ResponseQueueName { get; set; }
    public string EventQueueName { get; set; }
    public bool Loaded { get; set; }
    public DateTime CreatedAt { get; set; }
}