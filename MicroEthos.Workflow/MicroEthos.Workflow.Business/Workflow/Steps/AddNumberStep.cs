using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using Newtonsoft.Json;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace MicroEthos.Workflow.Business.Workflow.Steps;

[StepModule("Core")]
public class AddNumberStep : StepBodyAsync
{
    [StepInput] public int Input1 { get; set; }

    [StepInput] public int Input2 { get; set; }

    [StepInput] public Response Input3 { get; set; }

    [StepOutput] public int Output { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        Console.WriteLine("=====================> Started");
        //await Task.Delay(50000);
        // Thread.Sleep(50000);
        // Console.WriteLine($"==================> email with Title ({Input3.Title}) received ....");
        Output = Input1 + Input2;
        return ExecutionResult.Next();
    }
}

[StepModule("Core")]
public class ShowMessageStep : StepBodyAsync
{
    [StepInput] public int Data { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        Console.WriteLine($"Result is {JsonConvert.SerializeObject(Data)}");
        return ExecutionResult.Next();
    }
}

public class Response
{
    public bool Success { get; set; }
}