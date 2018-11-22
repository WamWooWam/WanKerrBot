using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using WamWooWam.Core;

namespace WanKerrBot.Commands
{
    [Description("Basic shit that is entirely useless.")]
    class BaseCommands : BaseCommandModule
    {
        static RandomNumberGenerator _random = RandomNumberGenerator.Create();
        static HttpClient _client = new HttpClient();

        [Command("Echo")]
        public async Task Echo(CommandContext ctx, [RemainingText, Description("The text to echo")] string echo)
        {
            foreach (var u in ctx.Message.MentionedUsers)
            {
                echo = echo.Replace(u.Mention, $"@{u.Username}#{u.Discriminator}");
            }

            echo = echo.Replace("@everyone", "\\@everyone");
            echo = echo.Replace("@here", "\\@here");

            await ctx.RespondAsync($"\"{echo}\"");
        }

        [Command("Roll")]
        [Description("Take a risk...")]
        public async Task Roll(
            CommandContext ctx,
            [Description("The number of dice to roll in the form <no>d<sides>")] string dice)
        {
            var splitd = dice.Split('d');

            if (splitd.Length == 2 && int.TryParse(splitd[0], out var count) && int.TryParse(splitd[1], out var max))
            {
                await Roll(ctx, count, max);
                return;
            }

            await ctx.RespondAsync("Hey! Something's very wrong here! Try again.");
        }

        [Aliases("char")]
        [Command("Character")]
        [Description("Details a character or characters")]
        public async Task Character(
            CommandContext ctx,
            [Description("The characters"), RemainingText] string str)
        {
            var dict = new Dictionary<string, Stream>();

            for (var i = 0; i < str.Length; i++)
            {
                var c = str[i];
                var resp = await _client.GetAsync(new Uri($"https://codepoints.net/api/v1/codepoint/{((ushort)c):x4}"));
                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                if (json.TryGetValue("image", out var thing))
                {
                    var stream = new MemoryStream(thing.ToObject<byte[]>());
                    dict.Add($"{i}.png", stream);
                }
            }

            await ctx.RespondWithFilesAsync(dict.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value));
        }

        [Command("Roll")]
        [Description("Take a risk...")]
        public async Task Roll(
            CommandContext ctx,
            [Description("The number of dice to roll")] int no,
            [Description("The maxiumum number of sides")]int sides)
        {
            if (sides > 0 && sides > 0)
            {
                if (sides <= 2048)
                {
                    var builder = new StringBuilder();
                    builder.Append($"{ctx.Member.DisplayName} rolled {no}d{sides} and got: ");

                    byte[] bytes = new byte[10];
                    int number = 0;
                    for (var i = 0; i < no - 1; i++)
                    {
                        _random.GetBytes(bytes);
                        number = bytes.Sum(b => b) % sides;

                        builder.Append($"{number}, ");
                    }

                    _random.GetBytes(bytes);
                    number = bytes.Sum(b => b) % sides;

                    builder.Append(number);
                    builder.Append("!");
                    await ctx.RespondAsync(builder.ToString());
                }
                else
                {
                    await ctx.RespondAsync("Yeah more than 2048 dice is probably not a great idea let's be honest.");
                }
            }
            else
            {
                await ctx.RespondAsync("I can't generate a negative number of dice you twat.");
            }
        }
    }
}
