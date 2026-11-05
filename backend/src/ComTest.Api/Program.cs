using ComTest.Api.Hosting;
using ComTest.Application;
using ComTest.Infrastructure;

if (args.Contains(HealthProbeCommand.Command))
{
    return await HealthProbeCommand.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();
builder.WebHost.ConfigureKestrel(RequestLimits.ConfigureKestrel);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApi(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.Contains(DevDataSeeder.Command))
{
    await DevDataSeeder.RunAsync(app);

    return 0;
}

app.UseApiPipeline();
app.MapApiEndpoints();

await app.RunAsync();

return 0;
