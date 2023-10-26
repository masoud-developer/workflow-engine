using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace MicroEthos.Workflow.Business.Workflow.Steps;

[StepModule("Core")]
public class GetUsers : StepBodyAsync
{
    [StepInput] public FilterUser Filter { get; set; }
    [StepOutput] public List<UserModel> Users { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        Users = new List<UserModel>
        {
            new()
            {
                Name = "Masoud",
                LastName = "Shafaghi",
                Age = 30
            },
            new()
            {
                Name = "Sharif",
                LastName = "Eari",
                Age = 29
            }
        };
        return ExecutionResult.Next();
    }
}

public class FilterUser
{
    public string NameContains { get; set; }
}

public class UserModel
{
    public string Name { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}