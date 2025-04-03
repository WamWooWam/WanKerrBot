global using static WamBot.Constants; // everywhere!
using DSharpPlus.Entities;

namespace WamBot;

// TODO: Rebrand back to WamBot :)
internal class Constants
{
#if DEBUG
    public const string BOT_NAME = "WamBot (Dev)";
    public const string BOT_COLOR_HEX = "#FF7800";
#else
    public const string BOT_NAME = "WamBot";
    public const string BOT_COLOR_HEX = "#009CFF";
#endif
    public static readonly DiscordColor BOT_COLOR = new(BOT_COLOR_HEX);

    public const string COLOUR_FORMATS = "#rgb, #rrggbb, rgb(), hsl(), hsv() or cmyk()";
}
