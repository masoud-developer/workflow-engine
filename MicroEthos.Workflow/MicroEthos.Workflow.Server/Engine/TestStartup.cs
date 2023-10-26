using MicroEthos.Common.Extensions;
using MicroEthos.Common.Logging.Extensions;
using MicroEthos.Common.Utils.Helpers;
using MicroEthos.Workflow.Business;

namespace MicroEthos.Workflow.Server.Engine;

public class TestStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMicroEthosCommon();
        services.AddMicroEthosLogging();
        services.AddCors();
        services.AddControllers();
        services.AddBusiness();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (EnvironmentHelper.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseCors(c => c
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
        app.UseRouting();
        // app.MapControllers();
    }
}