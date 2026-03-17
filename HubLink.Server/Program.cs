var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

builder.Services.AddHostedService<HubLinkServer>();

var host = builder.Build();

await host.RunAsync();
