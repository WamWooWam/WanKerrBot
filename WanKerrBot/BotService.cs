using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WamBot.Data;
using WamBot.SlashCommands;

namespace WamBot
{
    internal class BotService(IConfiguration configuration, IServiceProvider services, ILoggerFactory loggerFactory) : IHostedService
    {
        private const ulong WAM_SERVER = 185067273613082634;

        private DiscordClient _client;
        private SlashCommandsExtension _slashCommands;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client = new DiscordClient(new DiscordConfiguration()
            {
                Token = configuration["Token"],
                Intents = DiscordIntents.AllUnprivileged,
                LoggerFactory = loggerFactory,
            });

            _client.Ready += OnClientReady;
            _client.GuildAvailable += OnGuildAvailable;
            _slashCommands = _client.UseSlashCommands(new() { Services = services });

            _slashCommands.RegisterCommands<BaseSlashCommands>();
            _slashCommands.RegisterCommands<ClippySlashCommand>();
#if DEBUG
            _slashCommands.RegisterCommands<ColourSlashCommands>();
#endif
#if !DEBUG
            _slashCommands.RegisterCommands<HomeChannelSlashCommands>(WAM_SERVER);
#endif

            await _client.ConnectAsync();
        }

        private async Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs args)
        {
            return;

            await using var scope = services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetService<WamBotDbContext>();

            // so this is awful
            var members = await args.Guild.GetAllMembersAsync();

            // migrate role colours
            var regex = new Regex("kasumi#([0-9a-zA-Z]{6})");
            foreach (var role in args.Guild.Roles.Values)
            {
                var match = regex.Match(role.Name);
                if (!match.Success)
                    continue;

                var colour = match.Groups[1];
                var users = members.Where(m => m.Roles.Any(r => r.Id == role.Id));
                foreach (var user in users)
                {
                    var memberInfo = await dbContext.FindAsync<MemberInfo>([args.Guild.Id, user.Id]);
                    if (memberInfo == null)
                    {
                        memberInfo = new MemberInfo() { UserId = user.Id, GuildId = args.Guild.Id };
                        dbContext.Members.Add(memberInfo);
                    }


                    //memberInfo.Color = new ColorInfo() { }
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.DisconnectAsync();
        }

        private async Task OnClientReady(DiscordClient sender, ReadyEventArgs args)
        {
            await _slashCommands.RefreshCommands();
        }
    }
}
