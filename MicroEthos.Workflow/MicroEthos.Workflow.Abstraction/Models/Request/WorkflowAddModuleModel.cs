using MicroEthos.Common.Models;

namespace MicroEthos.Workflow.Abstraction.Models.Request;

public class WorkflowAddModuleModel
{
    public WorkflowAddModuleModel()
    {
        Components = new List<WorkflowAddComponentModel>();
        Events = new List<WorkflowEventModuleDto>();
    }

    public string Name { get; set; }
    public string Version { get; set; }
    public QueueNamesDto Queues { get; set; }
    public List<WorkflowAddComponentModel> Components { get; set; }
    public List<WorkflowEventModuleDto> Events { get; set; }
}

public class WorkflowAddComponentModel
{
    public string Name { get; set; }
    public string InputSchema { get; set; }
    public string OutputSchema { get; set; }
}

public class WorkflowEventModuleDto
{
    public string Name { get; set; }
    public string OutputSchema { get; set; }
}

// public class NotifInput
// {
//     public string Text { get; set; }
//     public string Title { get; set; }
// }
//
public class NotifOut
{
    public bool Success { get; set; }
}