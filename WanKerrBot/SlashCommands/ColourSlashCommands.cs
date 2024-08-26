using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WamBot.Data;
using WamBot.Extensions;

namespace WamBot.SlashCommands;

[SlashModuleLifespan(SlashModuleLifespan.Scoped)]
internal class ColourSlashCommands(WamBotDbContext _dbContext) : ApplicationCommandModule
{
    [SlashCommand("Color", "Change your role color!")]
    public Task Color(InteractionContext ctx, [Option(nameof(color), "The colour in #rgb, #rrggbb, rgb(), hsl(), hsv() or cmyk() form.")] string color)
    {
        return Colour(ctx, color);
    }

    [SlashCommand("Colour", "Change your role colour!")]
    public async Task Colour(InteractionContext ctx, [Option(nameof(colour), "The colour in #rgb, #rrggbb, rgb(), hsl(), hsv() or cmyk() form.")] string colour)
    {
        if (!colour.TryParseColor(out var col))
        {
            await ctx.RespondWithErrorAsync("That doesn't look like a colour to me! Try again.");
            return;
        }

        col = col.WithAlpha(1.0f);

        var vec = (Vector4)col;
        var hsl = ColorSpaceConverter.ToHsl(new Rgb(vec.X, vec.Y, vec.Z));
        var hsv = ColorSpaceConverter.ToHsv(new Rgb(vec.X, vec.Y, vec.Z));
        var cmyk = ColorSpaceConverter.ToCmyk(new Rgb(vec.X, vec.Y, vec.Z));

        var hslString = $"hsl({hsl.H:0}deg, {hsl.S:0.00}, {hsl.L:0.00})";
        var hsvString = $"hsv({hsv.H:0}deg, {hsv.S:0.00}, {hsv.V:0.00})";
        var cmykString = $"cmyk({cmyk.C:0.00}, {cmyk.M:0.00}, {cmyk.Y:0.00}, {cmyk.K:0.00})";

        using var image = new Image<Rgba32>(128, 128);
        image.Mutate(m => m.Fill(col));

        using var memoryStream = new MemoryStream();
        image.SaveAsPng(memoryStream);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var builder = ctx.CreateEmbedBuilder($"#{col.ToHex()[..6]}")
            .WithColor(new DiscordColor(col.ToHex()[..6]))
            .WithThumbnail("attachment://colour.png")
            .AddField("Input", $"{colour}", false)
            .AddField("Colour", $"#{col.ToHex()[..6]}", true)
            .AddField("Colour (HSL)", hslString, true)
            .AddField("Colour (HSV)", hsvString, true)
            .AddField("Colour (CMYK)", cmykString, true);

        var responseBuilder = new DiscordInteractionResponseBuilder()
            .AddEmbed(builder)
            .AddFile("colour.png", memoryStream);

        await ctx.CreateResponseAsync(responseBuilder);
    }
}
