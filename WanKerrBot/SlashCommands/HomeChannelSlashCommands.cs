using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Humanizer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using WamBot.Extensions;

namespace WamBot.SlashCommands;

[SlashCommandGroup("home", "Manage your home channel!")]
internal class HomeChannelSlashCommands : ApplicationCommandModule
{
    public const long HOMES_CATEGORY_ID = 1270516428440731753;

    [SlashCommand("create", "Make yourself a Home Channel")]
    public async Task Create(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        if (GetHomeChannel(ctx, ctx.User) == null)
        {
            var homes = ctx.Guild.GetChannel(HOMES_CATEGORY_ID);
            var newChannel = await ctx.Guild.CreateTextChannelAsync(
                $"{ctx.User.Username}s-home", homes);

            await newChannel.AddOverwriteAsync(ctx.Member, Permissions.ManageChannels | Permissions.ManageRoles | Permissions.ManageWebhooks | Permissions.ManageWebhooks);
            await newChannel.SendMessageAsync(
                $"Welcome, {ctx.User.Mention}, to your new personal Home Channel!\r\n" +
                $"Feel free to change the name and topic, maybe the perms, pin your own rules, etc. Make this your home!\r\n" +
                $"As soon as you're ready, you can delete this message.");

            await ctx.FollowUpAsync("All done, have fun!");
        }
        else
        {
            await ctx.FollowUpAsync("[No way! No way! No way! No way?](<https://www.youtube.com/watch?v=zILpjFqlOak>)");
        }
    }


    [SlashCommand("Info", "Get info on the current Home Channel")]
    public async Task Info(InteractionContext ctx, [Option(nameof(channel), "The home channel you want info from.")] DiscordChannel channel = null)
    {
        await ctx.DeferAsync();

        channel = channel ?? (ctx.Channel.ParentId == HOMES_CATEGORY_ID ? ctx.Channel : null);
        if (channel == null)
        {
            await ctx.FollowUpAsync("This isn't a home channel! Git outta 'ere!");
            return;
        }

        // fetch the channel again because sometimes discord is dumb with the cache
        channel = (await ctx.Guild.GetChannelsAsync())
                .First(c => c.Id == channel.Id);

        var administrators = (await Task.WhenAll(channel.PermissionOverwrites
            .Where(o => o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed)
            .Select(o => o.GetMemberAsync())))
            .OrderBy(u => u.DisplayName);

        var moderators = (await Task.WhenAll(channel.PermissionOverwrites
            .Where(o => o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageMessages) == PermissionLevel.Allowed)
            .Select(o => o.GetMemberAsync())))
            .OrderBy(u => u.DisplayName)
            .Except(administrators);

        var names = administrators.Select(o => o.Username).Humanize();
        var builder = ctx.CreateEmbedBuilder($"#{channel.Name}")
            .WithDescription($"Information about **{names}**'{(names.EndsWith('s') ? string.Empty : "s")} Home Channel.");

        if (!string.IsNullOrWhiteSpace(channel.Topic))
            builder.AddField("Topic", channel.Topic);

        if (administrators.Any())
            await AddFieldForUsersAsync(ctx, administrators, builder, administrators.Count() == 1 ? "Owner" : "Owners");
        if (moderators.Any())
            await AddFieldForUsersAsync(ctx, moderators, builder, moderators.Count() == 1 ? "Moderator" : "Moderators");

        builder.AddField("Created", Formatter.Timestamp(channel.CreationTimestamp, TimestampFormat.LongDateTime), true);

        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
            .AddEmbed(builder.Build()));
    }


    [SlashCommand("add", "Grants a user moderation perms in this Home Channel")]
    public async Task Grant(InteractionContext ctx, [Option(nameof(user), "The user you want to give permission to.")] DiscordUser user)
    {
        await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
        {
            await c.AddOverwriteAsync((DiscordMember)user, Permissions.ManageMessages);
            await ctx.FollowUpAsync($"{user.Mention} can now moderate this channel!");
        });
    }

    [SlashCommand("remove", "Revokes a user's moderation perms in this Home Channel")]
    public async Task Revoke(InteractionContext ctx, [Option(nameof(user), "The user you want to revoke permission from.")] DiscordUser user)
    {
        await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
        {
            await c.PermissionOverwrites.FirstOrDefault(o => o.Id == user.Id).DeleteAsync();
            await ctx.FollowUpAsync($"{user.Mention} can no longer moderate this channel!");
        });
    }

    [SlashCommand("Ban", "Bans a user this Home Channel")]
    public async Task Ban(InteractionContext ctx, [Option(nameof(user), "The user you want to ban.")] DiscordUser user)
    {
        await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
        {
            await c.AddOverwriteAsync((DiscordMember)user, deny: Permissions.AccessChannels | Permissions.SendMessages);
            await ctx.FollowUpAsync($"{user.Mention} is now banned from this channel!");
        });
    }

    [SlashCommand("Unban", "Revokes a user's ban from this Home Channel")]
    public async Task UnBan(InteractionContext ctx, [Option(nameof(user), "The user you want to give unban.")] DiscordUser user)
    {
        await RunIfChannelAdministratorAsync(ctx, async (m, c) =>
        {
            await c.PermissionOverwrites.FirstOrDefault(o => o.Id == user.Id && o.CheckPermission(Permissions.SendMessages) == PermissionLevel.Denied).DeleteAsync();
            await ctx.FollowUpAsync($"{user.Mention} is no longer banned from this channel!");
        });
    }

    private static DiscordChannel GetHomeChannel(InteractionContext ctx, DiscordUser user)
    {
        var homes = ctx.Guild.GetChannel(HOMES_CATEGORY_ID);
        return homes.Children.FirstOrDefault(c =>
                        c.PermissionOverwrites.Any(o =>
                            o.Type == OverwriteType.Member &&
                            o.Id == user.Id &&
                            o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed));
    }

    internal static async Task AddFieldForUsersAsync(InteractionContext context, IEnumerable<DiscordMember> users, DiscordEmbedBuilder builder, string name)
    {
        var stringBuilder = new StringBuilder();

        foreach (var user in users)
        {
            var emote = await GetUserEmoteAsync(context, user);
            stringBuilder.Append(" ");
            stringBuilder.Append(emote.ToString());
            stringBuilder.Append(" ");
            stringBuilder.Append(user.Mention);
        }

        builder.AddField(name, stringBuilder.ToString());
    }

    internal static async Task<DiscordEmoji> GetUserEmoteAsync(InteractionContext ctx, DiscordMember user)
    {
        var emojis = await ctx.Client.CurrentApplication.GetApplicationEmojisAsync();
        var name = user.AvatarHash;
        var emote = emojis.FirstOrDefault(e => e.Name == name);
        if (emote == null)
        {
            using var image = await user.GetAvatarAsync();
            image.Mutate(m => m.ApplyRoundedCorners(m.GetCurrentSize().Width / 2));

            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            stream.Seek(0, SeekOrigin.Begin);

            emote = await ctx.Client.CurrentApplication.CreateApplicationEmojiAsync(name, stream);
        }

        return emote;
    }

    internal static async Task RunIfChannelAdministratorAsync(InteractionContext context, Func<DiscordMember, DiscordChannel, Task> func)
    {
        await context.DeferAsync();

        if (context.Channel.ParentId != HOMES_CATEGORY_ID)
        {
            await context.FollowUpAsync("This isn't a home channel! Git outta 'ere!");
            return;
        }

        // make sure we have the latest permission overrides
        var channel = await context.Client.GetChannelAsync(context.Channel.Id);
        if (!channel.PermissionOverwrites.Any(o => o.Id == context.User.Id && o.Type == OverwriteType.Member && o.CheckPermission(Permissions.ManageChannels) == PermissionLevel.Allowed))
        {
            await context.FollowUpAsync("This isn't a home channel you can administrate!");
            return;
        }

        await func(context.Member, context.Channel);

    }
}
