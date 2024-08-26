using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WamBot.Extensions;
using WamBot.Resources.Clippy;

namespace WamBot.SlashCommands;

internal class ClippySlashCommand(IHttpClientFactory httpClientFactory) : ApplicationCommandModule
{
    private static readonly Rgba32 CLIPPY_BACKGROUND = new(0xFF, 0xFF, 0xCC); // clippy background colour
    private static readonly Lazy<FontCollection> CLIPPY_FONT_COLLECTION = new(() =>
    {
        var collection = new FontCollection();
        collection.Add(new MemoryStream(ClippyResources.Tahoma));
        collection.Add(new MemoryStream(ClippyResources.MicrosoftSansSerif));
        collection.Add(new MemoryStream(ClippyResources.ComicSansMS));
        collection.Add(new MemoryStream(ClippyResources.TimesNewRoman));
        collection.Add(new MemoryStream(ClippyResources.CourierNew));
        collection.Add(new MemoryStream(ClippyResources.NotoEmoji));
        collection.Add(new MemoryStream(ClippyResources.SegoeUISymbol));
        collection.AddCollection(new MemoryStream(ClippyResources.MSGothic));

        return collection;
    });

    private static readonly Lazy<Image<Rgba32>> CLIPPY_TOP_LEFT = new(() =>
    {
        var image = Image.Load<Rgba32>(ClippyResources.ClippyCorner);
        return image;
    });

    private static readonly Lazy<Image<Rgba32>> CLIPPY_TOP_RIGHT = new(() =>
    {
        var image = Image.Load<Rgba32>(ClippyResources.ClippyCorner);
        image.Mutate(i => i.Rotate(RotateMode.Rotate90));
        return image;
    });

    private static readonly Lazy<Image<Rgba32>> CLIPPY_BOTTOM_RIGHT = new(() =>
    {
        var image = Image.Load<Rgba32>(ClippyResources.ClippyCorner);
        image.Mutate(i => i.Rotate(RotateMode.Rotate180));
        return image;
    });

    private static readonly Lazy<Image<Rgba32>> CLIPPY_BOTTOM_LEFT = new(() =>
    {
        var image = Image.Load<Rgba32>(ClippyResources.ClippyCorner);
        image.Mutate(i => i.Rotate(RotateMode.Rotate270));
        return image;
    });

    private static readonly Lazy<Image<Rgba32>> CLIPPY_ARROW
        = new(() => Image.Load<Rgba32>(ClippyResources.ClippyArrow));

    private static readonly string[] CLIPPY_CHARACTERS
        = ["clippy", "dot", "hoverbot", "nature", "office", "powerpup", "scribble", "wizard", "rover", "einstein", "bonzi"];

    private const int CLIPPY_TOP_HEIGHT = 8;
    private const int CLIPPY_CORNER_SIZE = 8;
    private const int CLIPPY_BOTTOM_HEIGHT = 23;
    private const int CLIPPY_DEFAULT_MAX_WIDTH = 300;
    private const int CLIPPY_MIN_WIDTH = 150;
    private const int CLIPPY_MIN_WIDTH_WITH_IMAGE = 200;

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
        [Option(nameof(wrap), "Wrap text?")] bool wrap = true,
        [Option(nameof(image), "Add an image")] DiscordAttachment image = null)
    {
        await ctx.DeferAsync();

        PickCharacterAndFont(ctx.User, ref character, ref font);

        using var stream = await GenerateClippyAsync(UnescapeText(text), character, font, wrap, [image]);
        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
            .AddFile("clippit.png", stream));
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
        
        var character = CLIPPY_CHARACTER_MAX;
        var font = CLIPPY_FONT_MAX;
        PickCharacterAndFont(ctx.TargetMessage.Author, ref character, ref font);

        using var stream = await GenerateClippyAsync(text, character, font, true, [.. ctx.TargetMessage.Attachments]);
        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
            .AddFile("clippit.png", stream));
    }

    private static void PickCharacterAndFont(DiscordUser user, ref ClippyCharacter character, ref ClippyFont font)
    {
        if (character == CLIPPY_CHARACTER_INVALID)
            character = (ClippyCharacter)(((user.Id >> 22) + 3) % (int)CLIPPY_CHARACTER_MAX);
        if (font == CLIPPY_FONT_INVALID)
            font = (ClippyFont)((user.Id >> 22) % (int)CLIPPY_FONT_MAX);
    }

    private async Task<MemoryStream> GenerateClippyAsync(string text, ClippyCharacter character, ClippyFont font, bool wrap, DiscordAttachment[] attachments = null)
    {
        using var characterImage = Image.Load<Rgba32>((byte[])ClippyResources.ResourceManager.GetObject(CLIPPY_CHARACTERS[(int)character]));
        var collection = CLIPPY_FONT_COLLECTION.Value;
        var clippyFont = font switch
        {
            ClippyFont.Tahoma => collection.Get("Tahoma").CreateFont(10.5f),
            ClippyFont.Times => collection.Get("Times New Roman").CreateFont(11f),
            ClippyFont.ComicSans => collection.Get("Comic Sans MS").CreateFont(11f),
            ClippyFont.MSSansSerif => collection.Get("Microsoft Sans Serif").CreateFont(10.5f),
            ClippyFont.MSGothic => collection.Get("MS Gothic").CreateFont(10.5f),
            ClippyFont.CourierNew => collection.Get("Courier New").CreateFont(11f),
            _ => throw new InvalidOperationException()
        };

        var topLeft = CLIPPY_TOP_LEFT.Value;
        var topRight = CLIPPY_TOP_RIGHT.Value;
        var bottomLeft = CLIPPY_BOTTOM_LEFT.Value;
        var bottomRight = CLIPPY_BOTTOM_RIGHT.Value;
        var arrow = CLIPPY_ARROW.Value;

        var basicPen = new SolidPen(Brushes.Solid(Color.Black), 1);

        var imageWidth = CLIPPY_DEFAULT_MAX_WIDTH;
        var textOptions = new RichTextOptions(clippyFont)
        {
            WrappingLength = wrap ? imageWidth - 20 : -1,
            HintingMode = HintingMode.Standard,
            Font = clippyFont,
            Dpi = 96,
            WordBreaking = WordBreaking.BreakWord,
            FallbackFontFamilies = [
                collection.Get("MS Gothic"),
                collection.Get("Noto Emoji"),
                collection.Get("Times New Roman"),
                collection.Get("Segoe UI Symbol"),
            ]
        };

        var attachment = attachments?.FirstOrDefault(a => a?.Width != null && a?.Height != null);

        var textSize = TextMeasurer.MeasureSize(text, textOptions);
        var size = new Size((int)textSize.Right, (int)textSize.Bottom);
        imageWidth = Math.Max((int)size.Width + 20, attachment != null ? CLIPPY_MIN_WIDTH_WITH_IMAGE : CLIPPY_MIN_WIDTH);

        Image<Rgba32> attachmentImage = null;
        if (attachment != null)
        {
            var ratio = Math.Min((double)Math.Max(imageWidth, 200) / attachment.Width.Value, 400.0 / attachment.Height.Value);
            var width = (int)Math.Ceiling(attachment.Width.Value * ratio);
            var height = (int)Math.Ceiling(attachment.Height.Value * ratio);

            var decodeOptions = new DecoderOptions() { Sampler = KnownResamplers.NearestNeighbor, TargetSize = new Size(width - 2, height) };

            using var httpClient = httpClientFactory.CreateClient("Discord");
            using var request = new HttpRequestMessage(HttpMethod.Get, attachment.ProxyUrl + "?format=png");
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            response.EnsureSuccessStatusCode();

            using var attachmentStream = await response.Content.ReadAsStreamAsync();
            using var image = await Image.LoadAsync<Rgba32>(decodeOptions, attachmentStream);

            attachmentImage = image.Clone();
            attachmentImage.Mutate(m => m.Quantize(KnownQuantizers.WebSafe));
        }

        Image<Rgba32> textImage = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            // Generate image containing text
            textImage = new(imageWidth, (int)size.Height + 10);
            textImage.Mutate(m => m
                .SetGraphicsOptions(options => options.Antialias = true)
                .SetGraphicsOptions(options => options.AntialiasSubpixelDepth = 1)
                .Fill(Color.Transparent)
                .DrawText(textOptions, text, Brushes.Solid(Color.Black), null));
        }

        using var topImage = new Image<Rgba32>(imageWidth, 8);
        topImage.Mutate(m => m
                    .SetGraphicsOptions(options => options.Antialias = false)
                    .Fill(CLIPPY_BACKGROUND, new RectangleF(CLIPPY_CORNER_SIZE, 0, imageWidth - (CLIPPY_CORNER_SIZE * 2), CLIPPY_CORNER_SIZE))
                    .DrawImage(topLeft, new Point(0, 0), 1.0f)
                    .DrawImage(topRight, new Point(imageWidth - CLIPPY_CORNER_SIZE, 0), 1.0f)
                    .DrawLine(basicPen, [new((CLIPPY_CORNER_SIZE - 1), 0), new(imageWidth - (CLIPPY_CORNER_SIZE), 0)])); // i hate off by ones

        var arrowPosition = ((imageWidth / 2.0f) - (characterImage.Width / 2)) + 4; // vibes based maths
        using var bottomImage = new Image<Rgba32>(imageWidth, CLIPPY_BOTTOM_HEIGHT);
        bottomImage.Mutate(m => m
                    .SetGraphicsOptions(options => options.Antialias = false)
                    .Fill(CLIPPY_BACKGROUND, new RectangleF(8, 0, imageWidth - (CLIPPY_CORNER_SIZE * 2), CLIPPY_CORNER_SIZE))
                    .DrawImage(bottomLeft, new Point(0, 0), 1.0f)
                    .DrawImage(bottomRight, new Point(imageWidth - CLIPPY_CORNER_SIZE, 0), 1.0f)
                    .DrawLine(basicPen, [new((CLIPPY_CORNER_SIZE - 1), (CLIPPY_CORNER_SIZE - 1)), new(imageWidth - (CLIPPY_CORNER_SIZE + 1), (CLIPPY_CORNER_SIZE - 1))])
                    .DrawImage(arrow, new Point((int)arrowPosition, 0), 1));

        if (attachmentImage != null)
            size = new Size(size.Width, size.Height + attachmentImage.Height + (textImage != null ? 5 : 0));

        var textRectangle = new Rectangle(10, CLIPPY_TOP_HEIGHT - 1, imageWidth, (int)(size.Height) + 1);
        var innerRectangle = new Rectangle(0, CLIPPY_TOP_HEIGHT, imageWidth, (int)(size.Height) + 2);

        var imageHeight = CLIPPY_TOP_HEIGHT + size.Height + CLIPPY_BOTTOM_HEIGHT + characterImage.Height;
        using var returnImage = new Image<Rgba32>(imageWidth, (int)imageHeight);
        returnImage.Mutate(m =>
        {
            m.SetGraphicsOptions(options => options.Antialias = false)
             .Fill(Color.Transparent)
             .Fill(CLIPPY_BACKGROUND, innerRectangle)
             .DrawImage(topImage, new Point(0, 0), 1)
             .DrawImage(bottomImage, new Point(0, (int)(CLIPPY_TOP_HEIGHT + size.Height)), 1);

            if (attachmentImage != null)
                m.DrawImage(attachmentImage, new Point(Math.Max(1, (int)((imageWidth - attachmentImage.Width) / 2.0f)), CLIPPY_TOP_HEIGHT + size.Height - attachmentImage.Height), 1.0f);

            m.DrawLine(Color.Black, 1, [new PointF(innerRectangle.Left, innerRectangle.Top - 1), new PointF(innerRectangle.Left, innerRectangle.Bottom - 1)])
             .DrawLine(Color.Black, 1, [new PointF(innerRectangle.Right - 1, innerRectangle.Top - 1), new PointF(innerRectangle.Right - 1, innerRectangle.Bottom - 1)]);

            if (textImage != null)
                m.DrawImage(textImage, new Point(textRectangle.Left, textRectangle.Top), 1);

            m.DrawImage(characterImage, new Point((imageWidth - characterImage.Width) / 2, (int)(CLIPPY_TOP_HEIGHT + size.Height + CLIPPY_BOTTOM_HEIGHT)), 1);
        });

        var stream = new MemoryStream();
        await returnImage.SaveAsPngAsync(stream);
        stream.Seek(0, SeekOrigin.Begin);

        attachmentImage?.Dispose();
        textImage?.Dispose();

        return stream;
    }

    private static string UnescapeText(string text)
    {
        var builder = new StringBuilder();
        var lastChar = char.MinValue;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '\\')
            {
                if (lastChar == '\\')
                {
                    builder.Append('\\');
                    lastChar = char.MinValue;
                    continue;
                }

                lastChar = c;
                continue;
            }

            if (lastChar == '\\')
            {
                if (c == 'n')
                    builder.Append('\n');
                else if (c == 'r')
                    builder.Append('\r');
                else if (c == 't')
                    builder.Append('\t');
                else if (c == ' ')
                    builder.Append(' ');
                else
                    builder.Append(c);

                lastChar = c;
                continue;
            }

            builder.Append(c);
            lastChar = c;
        }

        text = builder.ToString();
        return text;
    }
}
