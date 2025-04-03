using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WamBot;

internal static class Tools
{
    private static HttpClient _httpClient = new HttpClient();

    internal static async Task<Image<Rgba32>> GetAvatarAsync(this DiscordMember user, ImageFormat format = ImageFormat.Png, ushort size = 64)
    {
        using var stream = await _httpClient.GetStreamAsync(user.GetAvatarUrl(format, size));

        return await Image.LoadAsync<Rgba32>(stream);
    }

   
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Shared.Next(n + 1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }

    private static readonly string[] OWOIFY_FACES = ["(・`ω´・)", ";;w;;", "owo", "UwU", ">w<", "^w^", "😳", "🥺"];

    public static string Owofiy(this string str)
    {
        str = Regex.Replace(str, "(?:r|l)", "w", RegexOptions.ECMAScript);
        str = Regex.Replace(str, "(?:R|L)", "W", RegexOptions.ECMAScript);
        str = Regex.Replace(str, "n([aeiou])", (m) => $"ny{m.Groups[1].Value}", RegexOptions.ECMAScript);
        str = Regex.Replace(str, "N([aeiou])", (m) => $"Ny{m.Groups[1].Value}", RegexOptions.ECMAScript);
        str = Regex.Replace(str, "N([AEIOU])", (m) => $"Ny{m.Groups[1].Value}", RegexOptions.ECMAScript);
        str = Regex.Replace(str, "ove", "uv", RegexOptions.ECMAScript);
        // str = str.Replace("hi", "hewwo", StringComparison.OrdinalIgnoreCase);

        str += " " + OWOIFY_FACES[Random.Shared.Next(OWOIFY_FACES.Length)];

        return str;
    }
}

public class RandomList<T>
{
    private T[] _items;
    private int _index;
    private Random _random;

    public RandomList(T[] items)
    {
        _random = new Random();
        _items = new T[items.Length];
        Array.Copy(items, _items, items.Length);
        _items.Shuffle();
    }

    public T Next()
    {
        var item = _items[_index];
        if (_index++ >= (_items.Length - 1))
        {
            _index = 0;
            _items.Shuffle();
        }

        return item;
    }
}
