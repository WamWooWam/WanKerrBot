using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WamBot;
using WamBot.Data;

var builder = Host.CreateApplicationBuilder();

builder.Configuration
    .AddJsonFile("settings.json", optional: false, reloadOnChange: true);

builder.Services.AddDbContext<WamBotDbContext>();

builder.Services.AddHttpClient("OpenElevation", c =>
    c.BaseAddress = new Uri("https://api.opentopodata.org/v1/"));
builder.Services.AddHttpClient("Discord");

builder.Services.AddHostedService<BotService>();

using var host = builder.Build();
await host.RunAsync();