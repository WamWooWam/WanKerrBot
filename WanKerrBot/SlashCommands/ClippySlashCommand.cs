using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using WamBot.Extensions;

namespace WamBot.SlashCommands;

internal class ClippySlashCommand(IHttpClientFactory httpClientFactory) : ApplicationCommandModule
{
    internal enum ClippyCharacter
    {
        [ChoiceName("Clippit")]
        Clippy,
        [ChoiceName("The Dot")]
        Dot,
        [ChoiceName("F1")]
        HoverBot,
        [ChoiceName("Mother Earth")]
        Nature,
        [ChoiceName("Office Logo")]
        Office,
        [ChoiceName("Rocky")]
        PowerPup,
        [ChoiceName("Links")]
        Scribble,
        [ChoiceName("Merlin")]
        Wizard,
        [ChoiceName("Rover")]
        Rover,
        [ChoiceName("The Genius")]
        Einstein,
        [ChoiceName("BonziBUDDY")]
        Bonzi
    }

    internal enum ClippyFont
    {
        [ChoiceName("Tahoma")]
        Tahoma,
        [ChoiceName("Comic Sans MS")]
        ComicSans,
        [ChoiceName("Microsoft Sans Serif")]
        MSSansSerif,
        [ChoiceName("Times New Roman")]
        Times,
        [ChoiceName("Courier New")]
        CourierNew,
        [ChoiceName("MS Gothic (Japan)")]
        MSGothic
    }

    private const ClippyCharacter CLIPPY_CHARACTER_INVALID = (ClippyCharacter)(-1);
    private const ClippyCharacter CLIPPY_CHARACTER_MAX = (ClippyCharacter.Bonzi + 1);

    private const ClippyFont CLIPPY_FONT_INVALID = (ClippyFont)(-1);
    private const ClippyFont CLIPPY_FONT_MAX = (ClippyFont.MSGothic + 1);

    [SlashCommand("Clippy", "Generates an image of clippy asking a question.")]
    public async Task Clippy(InteractionContext ctx,
        [Option(nameof(text), "What do you want the office assistant to say?")] string text,
        [Option(nameof(character), "What character do you want?")] ClippyCharacter character = CLIPPY_CHARACTER_INVALID,
        [Option(nameof(font), "What font do you want?")] ClippyFont font = CLIPPY_FONT_INVALID,
        [Option(nameof(image), "Add an image")] DiscordAttachment image = null)
    {
        await ctx.DeferAsync();

        PickCharacterAndFont(ctx.User, ref character, ref font);

        try
        {
            using var stream = await GenerateClippyAsync(text, character, font, [image]);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .AddFile("clippit.png", stream));
        }
        catch (Exception ex)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .WithContent($"Something went wrong generating your image. {ex.Message} Sorry!"));
        }
    }

    [ContextMenu(ApplicationCommandType.MessageContextMenu, "Clippy")]
    public async Task ClippyContextMenu(ContextMenuContext ctx)
    {
        await ctx.DeferAsync();

        var text = ctx.TargetMessage.Content;
        if (string.IsNullOrWhiteSpace(text) && ctx.TargetMessage.Attachments.Count == 0)
        {
            await ctx.FollowUpAsync("There's nothing I can do with that that message!");
            return;
        }

        var character = CLIPPY_CHARACTER_INVALID;
        var font = CLIPPY_FONT_INVALID;
        PickCharacterAndFont(ctx.TargetMessage.Author, ref character, ref font);

        try
        {
            using var stream = await GenerateClippyAsync(text, character, font, [.. ctx.TargetMessage.Attachments]);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .AddFile("clippit.png", stream));
        }
        catch (Exception ex)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .WithContent($"Something went wrong generating your image. {ex.Message} Sorry!"));
        }
    }

    private static void PickCharacterAndFont(DiscordUser user, ref ClippyCharacter character, ref ClippyFont font)
    {
        if (character == CLIPPY_CHARACTER_INVALID)
            character = (ClippyCharacter)(((user.Id >> 22) + 3) % (int)CLIPPY_CHARACTER_MAX);
        if (font == CLIPPY_FONT_INVALID)
            font = (ClippyFont)((user.Id >> 22) % (int)CLIPPY_FONT_MAX);
    }

    private async Task<MemoryStream> GenerateClippyAsync(
        string text,
        ClippyCharacter character,
        ClippyFont font,
        DiscordAttachment[] attachments = null)
    {
        using var httpClient = httpClientFactory.CreateClient("ClippyService");

        var characterString = ((int)character).ToString(CultureInfo.InvariantCulture);
        var fontString = ((int)font).ToString(CultureInfo.InvariantCulture);

        var content = new MultipartFormDataContent
        {
            { new StringContent(text), "text" },
            { new StringContent(fontString), "font" }
        };

        var attachment = attachments?.FirstOrDefault(a => a?.Width != null && a?.Height != null && (a?.MediaType.StartsWith("image/") ?? false));
        if (attachment != null)
        {
            using var discordHttpClient = httpClientFactory.CreateClient("Discord");
            using var attachmentRequest = new HttpRequestMessage(HttpMethod.Get, attachment.ProxyUrl);
            var attachmentResponse = await httpClient.SendAsync(attachmentRequest, HttpCompletionOption.ResponseContentRead);
            var attachmentStream = await attachmentResponse.Content.ReadAsStreamAsync();
            var streamContent = new StreamContent(attachmentStream);

            content.Add(streamContent, "attachment", "image.png");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{Uri.EscapeDataString(characterString)}/generate") { Content = content };
        var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(await response.Content.ReadAsStringAsync());
        }

        var memoryStream = new MemoryStream();
        using var stream = await response.Content.ReadAsStreamAsync();
        await stream.CopyToAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }
}
