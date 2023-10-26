using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Jint.Runtime;
using MicroEthos.Common.Contracts;
using MicroEthos.Common.Models.Enums;
using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using MicroEthos.Workflow.Business.Workflow.Providers;
using MongoDB.Bson;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using WorkflowCore.Primitives;
using ExecutionResult = WorkflowCore.Models.ExecutionResult;

namespace MicroEthos.Workflow.Business.Workflow.Steps.Middlewares;

public class WorkflowStepMiddleware : IWorkflowStepMiddleware
{
    private readonly IWorkflowHost _host;
    private readonly IJsonService _jsonService;
    private readonly IMicroEthosLogger _log;

    public WorkflowStepMiddleware(IMicroEthosLogger log, IWorkflowHost host, IJsonService jsonService)
    {
        _log = log;
        _host = host;
        _jsonService = jsonService;
    }

    public async Task<ExecutionResult> HandleAsync(
        IStepExecutionContext context,
        IStepBody body,
        WorkflowStepDelegate next)
    {
        var workflowId = context.Workflow.Id;
        var stepId = context.Step.Id;
        var contextObject = (WorkflowStateModel)context.Workflow.Data;
        var bodyProperties = body.GetType().GetProperties();
        context.ExecutionPointer.StepName = context.Step.ExternalId;
        
        //set inputs with correct type
        if (IsPrimitiveStep(body))
        {
            BindPrimitiveStepInputs(context, body, contextObject);
        }
        else
        {
            var inputs = bodyProperties.Where(p =>
                p.GetCustomAttributes(typeof(StepInputAttribute), false).Any());
            foreach (var input in inputs)
                if (contextObject.State.Items.Contains($"$${context.Step.ExternalId}_{input.Name}_In"))
                    try
                    {
                        var stepInput = contextObject.State.Items[$"$${context.Step.ExternalId}_{input.Name}_In"];
                        var value = ResolveCurrentStepInput(input.Name, input.PropertyType, stepInput,
                            contextObject, context);
                        // var value = Convert(JsonConvert.DeserializeObject(
                        //     contextObject.State.Items[$"$${context.Step.ExternalId}_{input.Name}_In"].ToString(),
                        //     input.PropertyType)!, input.PropertyType);

                        input.SetValue(body, value);
                    }
                    catch (Exception e)
                    {
                        context.Step.ErrorBehavior = WorkflowErrorHandling.Terminate;
                        await _log.Error($"Can not bind input of step {context.Step.ExternalId} in order {stepId}", e);
                        throw;
                    }
        }

        //create log scope
        using (_log.BeginScope("WorkflowId", workflowId))
        using (_log.BeginScope("StepId", stepId.ToString()))
        {
            //set current step outputs for bind it
            var outputs = bodyProperties.Where(p =>
                p.GetCustomAttributes(typeof(StepOutputAttribute), false).Any());

            // var result = await next();
            var result = await ExecuteNextStep(context, body, next);
            //bind current step outputs
            foreach (var output in outputs)
            {
                if (contextObject.State.Items.Contains($"$${context.Step.ExternalId}_{output.Name}_Out") &&
                    contextObject.State.Items[$"$${context.Step.ExternalId}_{output.Name}_Out"]?.ToString()
                        ?.ToLower() != "null")
                {
                    context.ExecutionPointer.ExtensionAttributes[$"$${output.Name}_Out"] =
                        contextObject.State.Items[$"$${context.Step.ExternalId}_{output.Name}_Out"];
                    continue;
                }

                var serializedOutValue = _jsonService.Serialize(output.GetValue(body), SerializationType.CamelCase);
                contextObject.State.Items[$"$${context.Step.ExternalId}_{output.Name}_Out"] =
                    BsonValue.Create(serializedOutValue);
                context.ExecutionPointer.ExtensionAttributes[$"$${output.Name}_Out"] = serializedOutValue;
            }
            
            return result;
        }
    }

    private bool IsPrimitiveStep(IStepBody step)
    {
        var stepType = step.GetType();
        if (stepType.Namespace?.Contains("WorkflowCore.Primitives") ?? false)
            return true;
        return false;
    }

    private dynamic Convert(dynamic source, Type dest)
    {
        return System.Convert.ChangeType(source, dest);
    }

    private object? ResolveCurrentStepInput(string inputName, Type inputStepType, BsonValue value, WorkflowStateModel stateModel,
        IStepExecutionContext context)
    {
        var state = stateModel.State;
        var replacedStatement = Regex.Replace(value.ToString()!, @"(\$\$[^\s.\$]+_[^\s.]+_Out)(\.[^\s\`\'\$""]+)?",
            match =>
            {
                var segments = match.Value.Trim('.').Split('.');
                JToken token = null;
                if (segments[0].Split('_').First().ToLower().Trim().StartsWith("$$foreach") &&
                    segments[0].Split('_')[1].ToLower().Trim() == "item" &&
                    !state.Items.Names.Contains(segments[0]))
                    token = JToken.Parse(context.Item?.ToString() ?? string.Empty);
                else if (state.Items.Names.Contains(segments[0]))
                    token = JToken.Parse(state.Items[segments[0]]?.ToString() ?? string.Empty);
                
                for (var i = 1; i < segments.Length; i++)
                    token = token?[segments[i][0].ToString().ToLower() + segments[i][1..]];

                if (token == null)
                    return "null";

                if (value.ToString()!.StartsWith("{") && token.Type == JTokenType.String)
                    return token.ToString().Replace("\"", "\\\"").Replace("\'", "\\\'");
                
                if (!value.ToString()!.StartsWith("{") && token.Type == JTokenType.String)
                    return $"\"{token.ToString().Replace("\"", "\\\"").Replace("\'", "\\\'")}\"";
                
                return token.ToString().ToLower();

                // return !value.ToString()!.StartsWith("{") && token.Type == JTokenType.String
                //     ? $"\"{token.ToString().Replace("\"", "\\\"")}\""
                //     : token.ToString().ToLower();
                // return token.BsonType == BsonType.String ? $"\"{token.ToString()}\"" : token?.ToString() ?? "null";
            });
        replacedStatement = replacedStatement.Replace("`", "'");
        replacedStatement = replacedStatement.Replace("\"'null'\"", "null");

        if (replacedStatement.Trim().StartsWith("{") && replacedStatement.Trim().EndsWith("}"))
        {
            replacedStatement = Regex.Replace(replacedStatement, @":(\s+)?("")(.*)+("")", match =>
            {
                var cleanItem = match.Value.Trim(':').Trim().Trim('\"');
                var jsRes = Evaluator.Engine.Execute($"let result = {cleanItem};")
                    .GetValue("result");
                var evaluatedExpression = jsRes.ToObject().ToString();
                Evaluator.Engine.ResetCallStack();
                return jsRes.Type == Types.String ? $": \"{evaluatedExpression!.Replace("\"", "\\\"").Replace("\'", "\\\'")}\"" : $": {evaluatedExpression}";
            });
            var parsed = JsonConvert.DeserializeObject(replacedStatement, inputStepType)!;
            return Convert(parsed, inputStepType);
        }
        else
        {
            var jsRes = Evaluator.Engine.Execute($"let result = {replacedStatement};")
                .GetValue("result");

            //evaluation for integers return number.0 (5.0) and must be convert to integer
            var evaluatedExpression = _jsonService.Serialize(jsRes.ToObject());
            context.ExecutionPointer.ExtensionAttributes[$"$${inputName}_In"] = evaluatedExpression;

            var inputTypeCode = Type.GetTypeCode(inputStepType);
            if (inputTypeCode is TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Byte
                    or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 ||
                (inputStepType.IsEnum && int.TryParse(evaluatedExpression, out var o)))
                evaluatedExpression = Regex.Replace(evaluatedExpression, @"\d+\.\d+", match =>
                {
                    var numberSplit = match.Value.Split(".");
                    if (System.Convert.ToInt32(numberSplit[1]) == 0)
                        return numberSplit[0];
                    return match.Value;
                });
            Evaluator.Engine.ResetCallStack();

            var parsed = JsonConvert.DeserializeObject(evaluatedExpression, inputStepType)!;
            return Convert(parsed, inputStepType);
        }
    }

    private void BindPrimitiveStepInputs(IStepExecutionContext context, IStepBody step, WorkflowStateModel stateModel)
    {
        var stepType = step.GetType();
        var state = stateModel.State;
        switch (stepType.Name)
        {
            case nameof(If):
                var ifStep = (If)step;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Condition_In"))
                    ifStep.Condition = (bool)ResolveCurrentStepInput("Condition", typeof(bool),
                        state.Items[$"$${context.Step.ExternalId}_Condition_In"], stateModel, context)!;
                return;
            case nameof(Foreach):
                var foreachStep = (Foreach)step;
                foreachStep.RunParallel = true;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Collection_In"))
                    foreachStep.Collection = ((Collection<object>)ResolveCurrentStepInput("Collection",
                        typeof(Collection<object>),
                        state.Items[$"$${context.Step.ExternalId}_Collection_In"], stateModel, context)!).Select(s =>
                    {
                        if (s is JObject j)
                            return j.ToString();
                        return s;
                    }).ToList();
                return;
            case nameof(While):
                var whileStep = (While)step;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Condition_In"))
                    whileStep.Condition = (bool)ResolveCurrentStepInput("Condition", typeof(bool),
                        state.Items[$"$${context.Step.ExternalId}_Condition_In"], stateModel, context)!;
                return;
            case nameof(Schedule):
                var scheduleStep = (Schedule)step;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Interval_In"))
                    scheduleStep.Interval = (TimeSpan)ResolveCurrentStepInput("Interval", typeof(TimeSpan),
                        state.Items[$"$${context.Step.ExternalId}_Interval_In"], stateModel, context)!;
                return;
            case nameof(Delay):
                var delayStep = (Delay)step;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Period_In"))
                    delayStep.Period = (TimeSpan)ResolveCurrentStepInput("Period", typeof(TimeSpan),
                        state.Items[$"$${context.Step.ExternalId}_Period_In"], stateModel, context)!;
                return;
            case nameof(Recur):
                var recurStep = (Recur)step;
                if (state.Items.Contains($"$${context.Step.ExternalId}_StopCondition_In"))
                    recurStep.StopCondition = (bool)ResolveCurrentStepInput("StopCondition", typeof(bool),
                        state.Items[$"$${context.Step.ExternalId}_StopCondition_In"], stateModel, context)!;
                if (state.Items.Contains($"$${context.Step.ExternalId}_Interval_In"))
                    recurStep.Interval = (TimeSpan)ResolveCurrentStepInput("Interval", typeof(TimeSpan),
                        state.Items[$"$${context.Step.ExternalId}_Interval_In"], stateModel, context)!;
                return;
        }
    }

    private async Task<ExecutionResult> ExecuteNextStep(IStepExecutionContext context, IStepBody body,
        WorkflowStepDelegate next)
    {
        // var result = await next();
        if (IsPrimitiveStep(body))
            return await next();

        if (context.PersistenceData == null)
            return ExecutionResult.Branch(new List<object> { context.Item },
                new ControlPersistenceData { ChildrenActive = true });

        if (context.PersistenceData is ControlPersistenceData &&
            (context.PersistenceData as ControlPersistenceData).ChildrenActive)
        {
            if (context.Workflow.IsBranchComplete(context.ExecutionPointer.Id))
                return await next();

            return ExecutionResult.Persist(context.PersistenceData);
        }

        return await next();
    }
}