using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WamBot.Data;
using WamBot.Extensions;

namespace WamBot.SlashCommands;

[SlashModuleLifespan(SlashModuleLifespan.Scoped)]
[SlashCommandGroup("canvas", "Play with some images")]
internal class ImageSlashCommands(WamBotDbContext dbContext, IHttpClientFactory httpClientFactory) : ApplicationCommandModule
{
    [SlashCommand("show", "Show your canvas to the world!")]
    public async Task Show(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var userInfo = await GetOrCreateMemberInfo(ctx);
        if (userInfo.Canvas == null)
        {
            await ctx.FollowUpAsync("You don't have a canvas!");
            return;
        }

        await RespondWithCanvasAsync(ctx, userInfo.Canvas);
    }

    [SlashCommand("load", "Load an image into your canvas.")]
    public async Task Load(InteractionContext ctx, [Option("attachment", "An image to load")] DiscordAttachment attachment)
    {
        await ctx.DeferAsync(true);

        try
        {
            await LoadCanvasFromAttachmentAsync(ctx, attachment);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    [SlashCommand("create", "Create a new blank canvas.")]
    public async Task Load(InteractionContext ctx,
        [Option("width", "Width of the canvas")] long width,
        [Option("height", "Height of the canvas")] long height,
        [Option("colour", $"New background colour of the canvas ({COLOUR_FORMATS}).")] string colour = "#fff")
    {
        await ctx.DeferAsync(true);
        if (width > 4096 || height > 4096)
        {
            await ctx.RespondAsync("That image is too big! Try something smaller please!");
            return;
        }

        if (height <= 4 || width <= 4)
        {
            await ctx.RespondAsync("That image is too small! Try something bigger please!");
            return;
        }

        if (!colour.TryParseColor(out var color))
        {
            await ctx.RespondAsync("That's not a real colour!");
            return;
        }

        try
        {
            var httpClient = httpClientFactory.CreateClient("Discord");
            var userInfo = await GetOrCreateMemberInfo(ctx);
            DiscordMessage followup = null;
            if (userInfo.Canvas != null)
            {
                (var overwrite, followup) = await EnsureOverwriteAsync(ctx);
                if (!overwrite)
                    return;

                dbContext.Canvases.Remove(userInfo.Canvas);
                userInfo.Canvas = null;
                userInfo.CanvasId = null;
            }

            var image = new Image<Rgba32>((int)width, (int)height, color);
            var canvas = new Canvas() { Width = image.Width, Height = image.Height };
            var layer = new Layer()
            {
                Opacity = 1,
                Position = 0,
                BlendingMode = PixelColorBlendingMode.Normal
            };

            SaveToLayer(layer, image);

            canvas.Layers.Add(layer);
            userInfo.Canvas = canvas;

            await RespondWithCanvasAsync(ctx, canvas, followup);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    [SlashCommand("pixelate", "Apply a pixelation filter to the current layer.")]
    public async Task Pixelate(InteractionContext ctx, [Option("amount", "Will create nxn pixels.")] long amount = 5)
    {
        await ProcessLayerCommandAsync(ctx, m => m.Pixelate((int)amount));
    }

    [SlashCommand("brightness", "Adjust the brightness of the current layer")]
    public async Task Brightness(InteractionContext ctx,
        [Option("amount", "A value of 0 will create an image that is completely black. A value of 1 leaves the input unchanged.")] double amount = 1f)
    {
        await ProcessLayerCommandAsync(ctx, m => m.Brightness((float)amount));
    }

    [SlashCommand("contrast", "Adjust the contrast of the current layer")]
    public async Task Contrast(InteractionContext ctx,
        [Option("amount", "A value of 0 will create an image that is completely grey. A value of 1 leaves the input unchanged.")] double amount = 1f)
    {
        await ProcessLayerCommandAsync(ctx, m => m.Contrast((float)amount));
    }

    [SlashCommand("hue", "Adjust the hue of the current layer")]
    public async Task Hue(InteractionContext ctx,
        [Option("amount", "Rotation angle in degrees to adjust the hue")] double amount = 0f)
    {
        await ProcessLayerCommandAsync(ctx, m => m.Hue((float)amount));
    }

    [SlashCommand("saturation", "Adjust the saturation of the current layer")]
    public async Task Saturation(InteractionContext ctx,
        [Option("amount", "A value of 0 will create an image that is purely greyscale. A value of 1 leaves the input unchanged.")] double amount = 1f)
    {
        await ProcessLayerCommandAsync(ctx, m => m.Saturate((float)amount));
    }

    public enum BlurType
    {
        [ChoiceName("Gaussian")]
        Gaussian,
        [ChoiceName("Box")]
        Box,
        [ChoiceName("Bokeh")]
        Bokeh
    }

    [SlashCommand("blur", "Blur the current layer")]
    public async Task Blur(InteractionContext ctx,
        [Option("amount", "The weight of the blur")] double amount = 1f,
        [Option("type", "The type of the blur")] BlurType type = BlurType.Gaussian)
    {
        await ProcessLayerCommandAsync(ctx, m =>
        {
            switch (type)
            {
                case BlurType.Gaussian:
                    m.GaussianBlur((float)amount);
                    break;
                case BlurType.Box:
                    m.BoxBlur((int)amount);
                    break;
                case BlurType.Bokeh:
                    m.BokehBlur((int)amount, 4, 2.0f);
                    break;
                default:
                    break;
            }
        });
    }

    [SlashCommand("oil", "Apply an oil-painting effect to the current layer")]
    public async Task Oil(InteractionContext ctx,
        [Option("levels", "The number of intensity levels. Higher values result in a  broader range of colors.")] long levels = 15,
        [Option("brushSize", "The number of neighboring pixels used in calculating each individual pixel value.")] long brushSize = 30)
    {
        await ProcessLayerCommandAsync(ctx, m => m.OilPaint((int)levels, (int)brushSize));
    }

    [SlashCommand("morejpeg", "mmmmmmmmmm jpeg")]
    public async Task MoreJpeg(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var userInfo = await GetOrCreateMemberInfo(ctx);
        if (userInfo.Canvas == null)
        {
            await ctx.FollowUpAsync("You don't have a canvas!");
            return;
        }

        try
        {
            var layer = userInfo.Canvas.Layers[0];
            using var image = LoadFromLayer(layer);

            using var stream = new MemoryStream();
            image.SaveAsJpeg(stream, new JpegEncoder() { Quality = 1 });
            stream.Seek(0, SeekOrigin.Begin);
            using var image2 = Image.Load<Rgba32>(stream);

            SaveToLayer(layer, image2);

            await RespondWithCanvasAsync(ctx, userInfo.Canvas);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    [SlashCommand("h264ify", "mmmmmmmmmm compression")]
    public async Task H264ify(InteractionContext ctx,
        [Option("crustiness", "The amount of compression to apply from 0 (basically lossless) to 16 (fucked)")] long crustiness = 4)
    {
        await ctx.DeferAsync(true);

        var userInfo = await GetOrCreateMemberInfo(ctx);
        if (userInfo.Canvas == null)
        {
            await ctx.FollowUpAsync("You don't have a canvas!");
            return;
        }

        try
        {
            var layer = userInfo.Canvas.Layers[0];
            using var image = LoadFromLayer(layer);

            var inFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".png");
            var outMp4FileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".mp4");
            var outPngFileName = Path.ChangeExtension(outMp4FileName, "png");

            var bitrate = Math.Min(Math.Max(7, Math.Floor(7 * (image.Width * image.Height) / 250000.0)), 28);
            var quality = Math.Floor(bitrate + (Math.Pow(1.66, 17 - crustiness)));

            {
                using var stream = File.Create(inFileName, 16 * 1024, FileOptions.Asynchronous);
                await image.SaveAsPngAsync(stream);
            }

            string[] commands = ["-i", inFileName, "-vframes", "1", "-vf", $"scale={image.Width}x{image.Height}", "-c:v", "libx264", "-b:v", $"{quality}k", outMp4FileName];
            var psi = new ProcessStartInfo("ffmpeg", commands);
            var process = Process.Start(psi);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("ffmpeg command failed!");

            commands = ["-i", outMp4FileName, "-vframes", "1", outPngFileName];
            psi = new ProcessStartInfo("ffmpeg", commands);
            process = Process.Start(psi);
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception("ffmpeg command failed!");

            {
                using var stream = File.Open(outPngFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var image2 = await Image.LoadAsync<Rgba32>(stream);
                SaveToLayer(layer, image2);
            }


            await RespondWithCanvasAsync(ctx, userInfo.Canvas);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    [ContextMenu(ApplicationCommandType.MessageContextMenu, "Open Image")]
    public async Task OpenImageContextMenu(ContextMenuContext ctx)
    {
        await ctx.DeferAsync(true);
        if (ctx.TargetMessage.Attachments.Count == 0 || ctx.TargetMessage.Attachments[0].Width == 0)
        {
            await ctx.FollowUpAsync("There's nothing I can do with that that message!");
            return;
        }

        try
        {
            var attachment = ctx.TargetMessage.Attachments[0];
            await LoadCanvasFromAttachmentAsync(ctx, attachment);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task ProcessLayerCommandAsync(InteractionContext ctx, Action<IImageProcessingContext> action)
    {
        await ctx.DeferAsync(true);

        var userInfo = await GetOrCreateMemberInfo(ctx);
        if (userInfo.Canvas == null)
        {
            await ctx.FollowUpAsync("You don't have a canvas!");
            return;
        }

        try
        {
            var layer = userInfo.Canvas.Layers[0];
            using var image = LoadFromLayer(layer);

            image.Mutate(action);

            SaveToLayer(layer, image);

            await RespondWithCanvasAsync(ctx, userInfo.Canvas);
        }
        finally
        {
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task LoadCanvasFromAttachmentAsync(BaseContext ctx, DiscordAttachment attachment)
    {
        var httpClient = httpClientFactory.CreateClient("Discord");
        using var image = await LoadImageFromAttachmentAsync(httpClient, attachment);
        if (image.Width > 4096 || image.Height > 4096)
        {
            await ctx.RespondAsync("That image is too big! Try something smaller please!");
            return;
        }

        var userInfo = await GetOrCreateMemberInfo(ctx);
        DiscordMessage followup = null;
        if (userInfo.Canvas != null)
        {
            (var overwrite, followup) = await EnsureOverwriteAsync(ctx);
            if (!overwrite)
                return;

            dbContext.Remove(userInfo.Canvas);
        }

        var canvas = new Canvas() { Width = image.Width, Height = image.Height };
        var layer = new Layer()
        {
            Opacity = 1,
            Position = 0,
            BlendingMode = PixelColorBlendingMode.Normal
        };

        SaveToLayer(layer, image);

        canvas.Layers.Add(layer);
        userInfo.Canvas = canvas;

        await RespondWithCanvasAsync(ctx, canvas, followup);
    }

    private static Image<Rgba32> LoadFromLayer(Layer layer)
    {
        return Image.Load<Rgba32>(layer.LayerData);
    }

    private static void SaveToLayer(Layer layer, Image<Rgba32> image)
    {
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        layer.LayerData = stream.ToArray();
    }

    #region Helpers (mess)
    private async Task<MemberInfo> GetOrCreateMemberInfo(BaseContext ctx)
    {
        var userInfo = await dbContext.Members
            .Include(c => c.Canvas)
            .FirstOrDefaultAsync(c => c.UserId == ctx.User.Id && c.GuildId == ctx.Guild.Id);
        if (userInfo == null)
        {
            userInfo = new MemberInfo() { GuildId = ctx.Guild.Id, UserId = ctx.User.Id };
            dbContext.Add(userInfo);
        }

        return userInfo;
    }

    private static async Task<(bool overwrite, DiscordMessage followup)> EnsureOverwriteAsync(BaseContext ctx)
    {
        var interactivity = ctx.Client.GetInteractivity();
        var builder = new DiscordFollowupMessageBuilder()
            .WithContent("This will erase your current canvas, continue?")
            .AddComponents([
                new DiscordButtonComponent(ButtonStyle.Danger, "yes_button", "Yes"),
                        new DiscordButtonComponent(ButtonStyle.Danger, "no_button", "No")]
                );

        var followup = await ctx.FollowUpAsync(builder);
        var button = await interactivity.WaitForButtonAsync(followup, CancellationToken.None);
        if (button.TimedOut || button.Result.Id == "no_button")
        {
            await ctx.DeleteFollowupAsync(followup.Id);
            return (false, null);
        }

        return (true, followup);
    }

    private static async Task<Stream> RespondWithCanvasAsync(BaseContext ctx, Canvas canvas, DiscordMessage followup = null)
    {
        using var stream = await RenderToStreamAsync(canvas);
        var newBuilder = new DiscordMessageBuilder();
        var embed = ctx.CreateEmbedBuilder($"@{ctx.User.Username}'s canvas")
            .WithImageUrl("attachment://canvas.png")
            .AddField("Dimensions", $"{canvas.Width}x{canvas.Height}", true)
            .AddField("Layers", $"{canvas.Layers.Count}", true);

        newBuilder.AddFile("canvas.png", stream)
            .AddEmbed(embed);

        if (followup != null)
        {
            await ctx.EditFollowupAsync(followup.Id, new DiscordWebhookBuilder(newBuilder));
        }
        else
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder(newBuilder));
        }

        return stream;
    }

    private static async Task<Stream> RenderToStreamAsync(Canvas canvas)
    {
        var image = new Image<Rgba32>(canvas.Width, canvas.Height);
        image.Mutate(m =>
        {
            foreach (var layer in canvas.Layers.OrderBy(c => c.Position))
            {
                var layerImage = Image.Load<Rgba32>(layer.LayerData);
                m.DrawImage(layerImage, layer.BlendingMode, (float)layer.Opacity);
            }
        });

        var memoryStream = new MemoryStream();
        await image.SaveAsPngAsync(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    private static async Task<Image<Rgba32>> LoadImageFromAttachmentAsync(HttpClient client, DiscordAttachment attachment)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, attachment.ProxyUrl + "?format=png");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var attachmentStream = await response.Content.ReadAsStreamAsync();
        var image = await Image.LoadAsync<Rgba32>(attachmentStream);

        return image;
    }
    #endregion
}
