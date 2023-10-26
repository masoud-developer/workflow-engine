using System.Dynamic;
using MicroEthos.Workflow.Abstraction.Models.Workflow.Attributes;
using RestSharp;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace MicroEthos.Workflow.Business.Workflow.Steps;

[StepModule("Core")]
public class HttpRequest : StepBodyAsync
{
    [StepInput] public string BaseUrl { get; set; }

    [StepInput] public string Resource { get; set; }

    [StepInput] public IDictionary<string, object> Headers { get; set; }

    [StepInput] public IDictionary<string, object> Parameters { get; set; }

    [StepInput] public ExpandoObject Body { get; set; }

    [StepInput] public DataFormat Format { get; set; } = DataFormat.Json;

    [StepInput] public Method Method { get; set; } = Method.Get;


    [StepOutput] public int ResponseCode { get; set; }

    [StepOutput] public dynamic ResponseBody { get; set; }


    public string ErrorMessage { get; set; }
    public bool IsSuccessful { get; set; }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var client = new RestClient(BaseUrl);
        var request = new RestRequest(Resource, Method)
        {
            RequestFormat = Format
        };

        if (Headers != null)
            foreach (var header in Headers)
                request.AddHeader(header.Key, Convert.ToString(header.Value));

        if (Parameters != null)
            foreach (var param in Parameters)
                request.AddQueryParameter(param.Key, Convert.ToString(param.Value));

        if (Body != null)
            switch (Format)
            {
                case DataFormat.Json:
                    request.AddJsonBody(Body);
                    break;
                case DataFormat.Xml:
                    request.AddXmlBody(Body);
                    break;
            }

        var response = await client.ExecuteAsync<dynamic>(request);
        IsSuccessful = response.IsSuccessful;

        if (response.IsSuccessful)
        {
            ResponseCode = (int) response.StatusCode;
            ResponseBody = response.Data;
        }
        else
        {
            ErrorMessage = response.ErrorMessage;
        }

        return ExecutionResult.Next();
    }
}