using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WamBot.Data;
using WamBot.SlashCommands;

namespace WamBot;

internal class BotService(IConfiguration configuration, IServiceProvider services, ILoggerFactory loggerFactory) : IHostedService
{
    private const ulong WAM_SERVER = 185067273613082634;

    private DiscordClient _client;
    private SlashCommandsExtension _slashCommands;
    private InteractivityExtension _interactivity;
    private ILogger<SlashCommandsExtension> _slashCommandLogger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _client = new DiscordClient(new DiscordConfiguration()
        {
            Token = configuration["Token"],
            Intents = DiscordIntents.AllUnprivileged,
            LoggerFactory = loggerFactory,
        });

        _client.Ready += OnClientReady;

        _slashCommandLogger = loggerFactory.CreateLogger<SlashCommandsExtension>();

        _slashCommands = _client.UseSlashCommands(new() { Services = services });
        _slashCommands.ContextMenuExecuted += OnContextMenuExecuted;
        _slashCommands.ContextMenuErrored += OnContextMenuErrored;

        _slashCommands.SlashCommandExecuted += OnSlashCommandExecuted;
        _slashCommands.SlashCommandErrored += OnSlashCommandErrored;

        _slashCommands.RegisterCommands<BaseSlashCommands>();
        _slashCommands.RegisterCommands<ClippySlashCommand>();
#if DEBUG
        _slashCommands.RegisterCommands<ImageSlashCommands>();
        _slashCommands.RegisterCommands<ColourSlashCommands>();
#endif
#if !DEBUG
        _slashCommands.RegisterCommands<HomeChannelSlashCommands>(WAM_SERVER);
#endif

        _interactivity = _client.UseInteractivity();

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<WamBotDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await _client.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.Ready -= OnClientReady;
        _slashCommands.ContextMenuExecuted -= OnContextMenuExecuted;
        _slashCommands.ContextMenuErrored -= OnContextMenuErrored;
        _slashCommands.SlashCommandExecuted -= OnSlashCommandExecuted;
        _slashCommands.SlashCommandErrored -= OnSlashCommandErrored;

        await _client.DisconnectAsync();
    }

    private async Task OnClientReady(DiscordClient sender, ReadyEventArgs args)
    {
        await _slashCommands.RefreshCommands();
    }

    private Task OnSlashCommandExecuted(SlashCommandsExtension sender, SlashCommandExecutedEventArgs args)
    {
        _slashCommandLogger.LogDebug("Slash command {Command} executed by @{User}", args.Context.CommandName, args.Context.User.Username);
        return Task.CompletedTask;
    }

    private Task OnSlashCommandErrored(SlashCommandsExtension sender, SlashCommandErrorEventArgs args)
    {
        _slashCommandLogger.LogError(args.Exception, "Slash command {Command} by @{User} failed!", args.Context.CommandName, args.Context.User.Username);
        return Task.CompletedTask;
    }

    private Task OnContextMenuExecuted(SlashCommandsExtension sender, ContextMenuExecutedEventArgs args)
    {
        _slashCommandLogger.LogDebug("Slash command {Command} executed by @{User}", args.Context.CommandName, args.Context.User.Username);
        return Task.CompletedTask;
    }

    private Task OnContextMenuErrored(SlashCommandsExtension sender, ContextMenuErrorEventArgs args)
    {
        _slashCommandLogger.LogError(args.Exception, "Slash command {Command} by @{User} failed!", args.Context.CommandName, args.Context.User.Username);
        return Task.CompletedTask;
    }
}
