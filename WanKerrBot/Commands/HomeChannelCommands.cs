using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using WamWooWam.Core;

namespace WanKerrBot.Commands
{
    [RequireGuild]
    [Group("home"), Description("Commands to add and manage home channels!")]
    class HomeChannelCommands : BaseCommandModule
    {
        public const long HOMES_ID = 473274788232560642;

        [Command("Create")]
        [Description("Make yourself a Home Channel")]
        public async Task Create(CommandContext ctx, DiscordChannel channel = null)
        {
            var homes = ctx.Guild.GetChannel(HOMES_ID);

            if (!homes.Children.Any(c =>
                c.PermissionOverwrites.Any(o =>
                    o.Type == OverwriteType.Member && o.Id == ctx.User.Id && o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed)))
            {
                var perms = ctx.Member.PermissionsIn(ctx.Guild.GetChannel(468876637342007327));

                if (perms.HasPermission(Permissions.SendMessages))
                {
                    var newChannel = await ctx.Guild.CreateTextChannelAsync(
                        $"{ctx.User.Username.ToLowerInvariant().Replace(" ", "-")}s-home", homes);

                    await newChannel.AddOverwriteAsync(ctx.Member, Permissions.ManageChannels | Permissions.ManageRoles | Permissions.ManageWebhooks | Permissions.ManageWebhooks);

                    await newChannel.SendMessageAsync(
                        $"Welcome, {ctx.User.Mention}, to your new personal Home Channel!\r\n" +
                        $"Feel free to change the name and topic, maybe the perms, pin your own rules, etc. Make this your home!\r\n" +
                        $"As soon as you're ready, you can delete this message.");

                    await ctx.RespondAsync("All done, have fun!");
                }
                else
                {
                    await ctx.RespondAsync("No way! No way! No way! No way?");
                }
            }
            else
            {
                await ctx.RespondAsync("You already have a Home Channel damnit! No more for you!");
            }
        }


        [Command("Info")]
        [Description("Get info on the current Home Channel")]
        public async Task Info(CommandContext ctx, DiscordChannel channel = null)
        {
            channel = channel ?? (ctx.Channel.ParentId == HOMES_ID ? ctx.Channel : null);
            if (channel != null)
            {
                var administrators = (await Task.WhenAll(channel.PermissionOverwrites
                    .Where(o => o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed)
                    .Select(o => o.GetMemberAsync())))
                    .OrderBy(u => u.DisplayName);

                var moderators = (await Task.WhenAll(channel.PermissionOverwrites
                    .Where(o => o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageMessages) == PermissionLevel.Allowed)
                    .Select(o => o.GetMemberAsync())))
                    .OrderBy(u => u.DisplayName)
                    .Except(administrators);

                var names = Strings.NaturalJoin(administrators.Select(o => o.Username));
                var builder = new DiscordEmbedBuilder()
                    .WithTitle($"#{channel.Name}")
                    .WithDescription($"Information about **{names}**'{(names.EndsWith('s') ? string.Empty : "s")} Home Channel.");

                if (!string.IsNullOrWhiteSpace(channel.Topic))
                    builder.AddField("Topic", channel.Topic);

                if (administrators.Any())
                    await AddFieldForUsersAsync(ctx, administrators, builder, administrators.Count() == 1 ? "Owner" : "Owners");
                if (moderators.Any())
                    await AddFieldForUsersAsync(ctx, moderators, builder, moderators.Count() == 1 ? "Moderator" : "Moderators");

                //var cul = member.GetUserCulture();

                builder.AddField("Created", channel.CreationTimestamp.ToString(), true);
                builder.AddField("Safe for work?", (!channel.IsNSFW).ToString(), true);

                await ctx.RespondAsync(embed: builder.Build());

                //foreach (var emote in emotes)
                //{
                //    await ctx.Guild.DeleteEmojiAsync(emote);
                //}
            }
            else
            {
                await ctx.RespondAsync("This isn't a home channel! Git outta 'ere!");
            }
        }

        [Command("NSFW")]
        [Description("Marks your current home channell as NSFW, creating overrides for <&424695889211162627> in the process.")]
        public async Task Nsfw(CommandContext ctx)
        {
            await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
            {
                if (c.IsNSFW)
                {
                    await c.ModifyAsync(e => e.Nsfw = false);
                    await c.AddOverwriteAsync(ctx.Guild.GetRole(424695889211162627), allow: Permissions.AccessChannels);
                    await ctx.RespondAsync($"This channel is no longer marked as NSFW!");
                }
                else
                {
                    await c.ModifyAsync(e => e.Nsfw = true);
                    await c.AddOverwriteAsync(ctx.Guild.GetRole(424695889211162627), deny: Permissions.AccessChannels);
                    await ctx.RespondAsync($"This channel is now marked as NSFW!");
                }
            });
        }

        [Group("mod")]
        class ModeratorCommands : BaseCommandModule
        {
            [Aliases("add")]
            [Command("Grant")]
            [Description("Grants a user moderation perms in this Home Channel")]
            public async Task Grant(CommandContext ctx, DiscordMember user)
            {
                await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
                {
                    await c.AddOverwriteAsync(user, Permissions.ManageMessages);
                    await ctx.RespondAsync($"{user.Mention} can now moderate this channel!");
                });
            }

            [Aliases("remove")]
            [Command("Revoke")]
            [Description("Revokes a user's moderation perms in this Home Channel")]
            public async Task Revoke(CommandContext ctx, DiscordMember user)
            {
                await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
                {
                    await c.PermissionOverwrites.FirstOrDefault(o => o.Id == user.Id).DeleteAsync();
                    await ctx.RespondAsync($"{user.Mention} can no longer moderate this channel!");
                });
            }

            [Command("Ban")]
            [Description("Bans a user this Home Channel")]
            public async Task Ban(CommandContext ctx, DiscordMember user)
            {
                await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
                {
                    await c.AddOverwriteAsync(user, deny: Permissions.AccessChannels | Permissions.SendMessages);
                    await ctx.RespondAsync($"{user.Mention} is now banned from this channel!");
                });
            }

            [Command("Unban")]
            [Description("Revokes a user's ban from this Home Channel")]
            public async Task UnBan(CommandContext ctx, DiscordMember user)
            {
                await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
                {
                    await c.PermissionOverwrites.FirstOrDefault(o => o.Id == user.Id && o.CheckPermission(Permissions.SendMessages) == PermissionLevel.Denied).DeleteAsync();
                    await ctx.RespondAsync($"{user.Mention} is no longer banned from this channel!");
                });
            }
        }

        static async Task AddFieldForUsersAsync(CommandContext context, IEnumerable<DiscordMember> users, DiscordEmbedBuilder builder, string name)
        {
            var stringBuilder = new StringBuilder();

            foreach (var user in users)
            {
                var emote = await GetUserEmoteAsync(context, user);
                stringBuilder.Append(" ");
                stringBuilder.Append(emote.ToString());
                stringBuilder.Append(" ");
                stringBuilder.Append(user.Username);
            }

            builder.AddField(name, stringBuilder.ToString());

            await Task.Delay(1000);
        }

        static async Task<DiscordEmoji> GetUserEmoteAsync(CommandContext ctx, DiscordMember user)
        {
            var guild = ctx.Client.Guilds[488016189142859778];

            var name = user.AvatarHash;
            var emote = guild.Emojis.FirstOrDefault(e => e.Name == name);
            if (emote == null)
            {
                if (guild.Emojis.Count == 50)
                {
                    await guild.DeleteEmojiAsync(guild.Emojis.FirstOrDefault() as DiscordGuildEmoji);
                }

                using (var image = await Tools.GetImageForUserAsync(user))
                {
                    image.Mutate(m => m.Apply((i => Tools.ApplyRoundedCorners(i, i.Width / 2))));

                    using (var stream = new MemoryStream())
                    {
                        image.SaveAsPng(stream);
                        stream.Seek(0, SeekOrigin.Begin);

                        emote = await guild.CreateEmojiAsync(name, stream);
                    }
                }
            }

            return emote;
        }

        internal static async Task RunIfChannelAdministratorAsync(CommandContext context, Func<DiscordMember, DiscordChannel, Task> func)
        {
            if (context.Channel.ParentId == HOMES_ID)
            {
                if (context.Channel.PermissionOverwrites.Any(o => o.Id == context.User.Id && o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed))
                {
                    await func(context.Member, context.Channel);
                }
                else
                {
                    await context.Channel.SendMessageAsync("This isn't a home channel you can administrate! Feck off!");
                }
            }
            else
            {
                await context.Channel.SendMessageAsync("This isn't a home channel! Git outta 'ere!");
            }
        }
    }
}
