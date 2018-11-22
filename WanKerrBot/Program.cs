using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WamWooWam.Core;
using WanKerrBot.Commands;

namespace WanKerrBot
{
    class Program
    {
        private const string DOLLAR = "💵";
        private const string EURO = "💶";
        private const string POUND = "💷";
        private const string YEN = "💴";
        private const bool DO_REACTIONS = false;

        private static DiscordClient _client;
        private static CommandsNextExtension _commands;
        private static InteractivityExtension _interactivity;

        static async Task Main(string[] args)
        {
            try
            {
                _client = new DiscordClient(new DiscordConfiguration()
                {
                    Token = Settings.GetSetting<string>("Token", null),
                    LogLevel = LogLevel.Debug
                });

                _client.Ready += _client_Ready;
                _client.MessageCreated += _client_MessageCreated;
                _client.MessageReactionAdded += _client_MessageReactionAdded;
                _client.DebugLogger.LogMessageReceived += (o, e) =>
                {
                    var color = ConsoleColor.White;
                    switch (e.Level)
                    {
                        case LogLevel.Debug:
                            color = ConsoleColor.DarkGray;
                            break;
                        case LogLevel.Warning:
                            color = ConsoleColor.Yellow;
                            break;
                        case LogLevel.Error:
                            color = ConsoleColor.Red;
                            break;
                        case LogLevel.Critical:
                            color = ConsoleColor.DarkRed;
                            break;
                        default:
                            break;
                    }

                    Console.ForegroundColor = color;
                    Console.Write($"[{e.Timestamp}][{e.Application}] ");
                    Console.WriteLine(e.Message);
                };

                var services = new ServiceCollection();
                //services.AddDbContext<WanKerrDbContext>();

                _commands = _client.UseCommandsNext(new CommandsNextConfiguration()
                {
                    EnableDefaultHelp = false,
                    EnableMentionPrefix = true,
                    StringPrefixes = new[] { "!" }
                });

                _interactivity = _client.UseInteractivity(new InteractivityConfiguration()
                {
                    Timeout = TimeSpan.FromMinutes(5)
                });

                //_commands.SetHelpFormatter<HelpFormatter>();
                _commands.RegisterCommands<BaseCommands>();
                _commands.RegisterCommands<HomeChannelCommands>();
                _commands.CommandErrored += _commands_CommandErrored;

                await _client.ConnectAsync();
                await Task.Delay(-1);
            }
            catch (Exception ex)
            {

            }
        }

        private static async Task _client_Ready(ReadyEventArgs e)
        {

        }

        private static async Task _commands_CommandErrored(CommandErrorEventArgs e)
        {
            var embedBuilder = new DiscordEmbedBuilder()
                .WithAuthor("Welp, fuck - WanKerr Bot", iconUrl: e.Context.Channel.Guild.IconUrl)
                .WithDescription($"Something's gone very wrong executing the `{e.Command.Name}` command, and an {e.Exception.GetType().Name} occured.")
                .WithFooter("This message will be deleted in 10 seconds")
                .WithTimestamp(DateTimeOffset.UtcNow + TimeSpan.FromSeconds(10))
                .WithColor(new DiscordColor(255, 0, 0))
                .AddField("Message", $"```{e.Exception.Message.Truncate(1016)}```")
#if DEBUG
                .AddField("Stack Trace", $"```{e.Exception.StackTrace.Truncate(1016)}```")
#endif
                ;

            await Tools.SendTemporaryMessage(e.Context.Channel, emb: embedBuilder.Build(), timeout: 10_000);
        }

        private static string[] _currencies = new[] { DOLLAR, EURO, POUND, YEN };
        private static Regex _currencyRegex = new Regex(@"([£\$€¥])(\d+(?:\.\d{2})?)");
        private static ConcurrentBag<DiscordMessage> _bag = new ConcurrentBag<DiscordMessage>();

        private static async Task _client_MessageCreated(MessageCreateEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Message.Content) && e.Author.Id != _client.CurrentUser.Id && DO_REACTIONS)
            {
                if (_currencyRegex.IsMatch(e.Message.Content))
                {
                    _bag.Add(e.Message);
                    foreach (var item in _currencies)
                    {
                        await e.Message.CreateReactionAsync(DiscordEmoji.FromUnicode(item));
                        await Task.Delay(500);
                    }
                }
            }
        }

        private static async Task _client_MessageReactionAdded(MessageReactionAddEventArgs e)
        {
            var unicode = e.Emoji.ToString();
            if (_bag.Any(m => m == e.Message) && _currencies.Contains(unicode))
            {
                switch (unicode)
                {
                    case DOLLAR:

                        break;
                    default:
                        break;
                }
            }
        }
    }
}
