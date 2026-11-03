using ComTest.Api.Hosting;
using ComTest.Application;
using ComTest.Infrastructure;

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

    return;
}

app.UseApiPipeline();
app.MapApiEndpoints();

await app.RunAsync();
