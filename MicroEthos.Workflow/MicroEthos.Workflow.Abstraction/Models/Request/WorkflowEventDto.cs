namespace MicroEthos.Workflow.Abstraction.Models.Request;

public class WorkflowEventDto
{
    public string TraceId { get; set; }
    public string JobId { get; set; }
    public string ModuleId { get; set; }
    public string UserId { get; set; }
    public string ServiceId { get; set; }
    public string InstitutionId { get; set; }
    public string Command { get; set; }
    public object Payload { get; set; }
    public DateTime CreatedDate { get; set; }
}