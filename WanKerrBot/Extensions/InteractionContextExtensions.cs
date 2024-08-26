using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace WamBot.Extensions
{
    internal static class InteractionContextExtensions
    {
        internal static async Task RespondWithErrorAsync(this BaseContext ctx, string error)
        {
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                .WithContent(error)
                .AsEphemeral());
        }

        internal static async Task RespondAsync(this BaseContext ctx, string error)
        {
            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                .WithContent(error));
        }

        internal static async Task FollowUpAsync(this BaseContext ctx, string text)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .WithContent(text));
        }

        internal static DiscordEmbedBuilder CreateEmbedBuilder(this BaseContext ctx, string title) 
            => new DiscordEmbedBuilder().WithAuthor((string.IsNullOrWhiteSpace(title) ? BOT_NAME : $"{title} - {BOT_NAME}"), iconUrl: ctx.Client.CurrentUser.GetAvatarUrl(ImageFormat.Png))
                .WithThumbnail(ctx.Client.CurrentUser.GetAvatarUrl(ImageFormat.Png))
                .WithColor(BOT_COLOR);
    }
}
