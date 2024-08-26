using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using WamBot.Extensions;
using Humanizer;
using WamBot.Resources.Base;

namespace WamBot.SlashCommands;

internal class BaseSlashCommands(IHttpClientFactory _httpFactory, ILogger<BaseSlashCommands> _logger)
    : ApplicationCommandModule
{
    private static readonly Lazy<RandomList<string>> _pickupLines
        = new(() => new(BaseResources.PickupLines.Split("\n")));

    [SlashCommand("stats", "Mildly uninteresting info and data about my current state.")]
    public async Task Stats(InteractionContext ctx)
    {
        static int CountCommands(InteractionContext ctx)
        {
            return ctx.SlashCommandsExtension.RegisteredCommands.Where(c => c.Key == ctx.Guild.Id || c.Key == null)
                .SelectMany(s => s.Value)
                .Distinct()
                .Count();
        }

        Process process = Process.GetCurrentProcess();
        AssemblyName mainAssembly = Assembly.GetExecutingAssembly().GetName();

        var frameworkName = RuntimeInformation.FrameworkDescription;
        var rid = RuntimeInformation.RuntimeIdentifier;
        var osDescription = RuntimeInformation.OSDescription;
        var osArchitecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        var builder = ctx.CreateEmbedBuilder("About");
        builder.AddField("Ping", $"{ctx.Client.Ping}ms", false);
        builder.AddField("Operating System", $"{osDescription} (`{osArchitecture}`)", false);
        builder.AddField("Target Framework", $"{frameworkName} (`{rid}`)", false);

        builder.AddField("RAM Usage (GC)", GC.GetTotalMemory(false).Bytes().ToString(), true);
        builder.AddField("RAM Usage (Process)", process.WorkingSet64.Bytes().ToString(), true);
        builder.AddField("Version", $"{mainAssembly.Version}", true);

        builder.AddField("Guilds", ctx.Client.Guilds.Count.ToString("G"), true);
        builder.AddField("Total Channels", (ctx.Client.PrivateChannels.Count + ctx.Client.Guilds.Values.SelectMany(g => g.Channels).Count()).ToString("G"), true);

        builder.AddField("Total Roles", ctx.Client.Guilds.Values.SelectMany(g => g.Roles).Count().ToString("G"), true);
        builder.AddField("Total Emotes", ctx.Client.Guilds.Values.SelectMany(g => g.Emojis).Count().ToString("G"), true);
        builder.AddField("Total Members", ctx.Client.Guilds.Values.Sum(g => g.MemberCount).ToString("G"), true);

        builder.AddField("Available Commands", CountCommands(ctx).ToString("G"), true);

        builder.AddField("Uptime", Formatter.Timestamp(process.StartTime - DateTime.Now));

        var responseBuilder = new DiscordInteractionResponseBuilder()
            .AddEmbed(builder);

        await ctx.CreateResponseAsync(responseBuilder);
    }

    [SlashCommand("roll", "Take a risk...")]
    public async Task Roll(
        InteractionContext ctx,
        [Option("dice", "The number of dice to roll in the form <no>d<sides>")] string dice)
    {
        static IEnumerable<int> GetNumbers(int count, int sides)
        {
            for (int i = 0; i < count; i++)
                yield return RandomNumberGenerator.GetInt32(1, sides + 1);
        }

        var splitd = dice.Split('d');

        if (splitd.Length == 2
            && int.TryParse(splitd[0], out var count)
            && int.TryParse(splitd[1], out var sides)
            && count > 1 && count < 1024
            && sides > 0)
        {
            var numbers = GetNumbers(count, sides).ToList();

            var builder = new StringBuilder();
            builder.Append($"{ctx.Member.Mention} rolled {count}d{sides} and got: {string.Join(", ", numbers)} = {numbers.Sum()}!");

            if (builder.Length > 2000)
            {
                await ctx.RespondWithErrorAsync("Woah there, that's too many numbers bucko!");
                return;
            }

            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                .WithContent(builder.ToString()));
            return;
        }

        await ctx.RespondWithErrorAsync("Something's gone wrong here! Check your input and try again.");
    }

    [SlashCommand("Location", "Where do they live?")]
    public async Task Location(
        InteractionContext ctx,
        [Option("User", "Where does this user live?")] DiscordUser user = null)
    {
        await ctx.DeferAsync();

        var client = _httpFactory.CreateClient("OpenElevation");

        var latCoord = 0.0M;
        var longCoord = 0.0M;
        var elevation = 0.0f;

        do
        {
            latCoord = Random.Shared.NextInt64(-65_000_000_000, 75_000_000_000) / 1_000_000_000.0M;
            longCoord = Random.Shared.NextInt64(-180_000_000_000, 180_000_000_000) / 1_000_000_000.0M;

            var resp = await client.GetStringAsync("aster30m?locations=" + Uri.EscapeDataString(latCoord + "," + longCoord));
            var respJson = JObject.Parse(resp);
            var respElevation = respJson["results"][0]["elevation"];
            if (respElevation.Type != JTokenType.Null)
                elevation = respElevation.ToObject<float>();

            _logger.LogInformation($"{latCoord}, {longCoord} - {elevation}M above sea level");

            if (elevation >= 0) break;

            await Task.Delay(2000);
        }
        while (elevation < 0);

        var format =
            Random.Shared.NextDouble() > 0.5 ?
            "https://www.google.com/maps?q={0}%2C{1}&z=6&t=k" :
            "https://beta.maps.apple.com/?ll={0}%2C{1}&lsp=7618&q=My+House";

        if (user != null)
        {
            await ctx.FollowUpAsync($"{user.Mention}'s location is {string.Format(format, latCoord, longCoord)}");
        }
        else
        {
            await ctx.FollowUpAsync($"My location is {string.Format(format, latCoord, longCoord)}");
        }
    }

    [SlashCommand("IP", "IP!??!?")]
    public async Task IPAddress(InteractionContext ctx, [Option("user", "What is this user's IP address?")] DiscordUser user = null)
    {
        var arr = new byte[2];
        Random.Shared.NextBytes(arr);

        var subnet = Random.Shared.NextDouble() < 0.5 ? "192.168" : "127.0";

        if (user != null)
        {
            await ctx.RespondAsync($"{user.Mention}'s IP address is {subnet}.{arr[0]}.{arr[1]}");
        }
        else
        {
            await ctx.RespondAsync($"My IP address is {subnet}.{arr[0]}.{arr[1]}");
        }
    }

    [SlashCommand("Pickup", "Shoot your (miserable) shot with someone ;)")]
    public async Task Pickup(InteractionContext ctx, [Option(nameof(user), "Who do you want to \"flirt\" with today?")] DiscordUser user = null)
    {
        string line = _pickupLines.Value.Next();
        string[] tripWords = ["Hey", "Hi", "Babe", "Bitch", "Damn", "Girl"]; // words we remove from lines so targeted lines make sense
        var text = new StringBuilder();
        if (user != null)
        {
            text.Append($"Hey {user.Mention} ");
            foreach (var word in tripWords)
            {
                if (line.StartsWith(word, StringComparison.InvariantCultureIgnoreCase))
                    line = line.Substring(word.Length).TrimStart();
            }

            line = line.Trim().TrimStart(',');
            if (!line.StartsWith("I ") && !line.StartsWith("I'"))
                line = char.ToLower(line[0]) + line.Substring(1);
        }

        text.Append(string.Format(line, ctx.User.Mention));

        await ctx.RespondAsync(Random.Shared.NextDouble() > 0.9 ? text.ToString().Owofiy() : text.ToString());
    }
}
