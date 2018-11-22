using DSharpPlus.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace WanKerrBot
{
    class Tools
    {
        private static HttpClient _httpClient = new HttpClient();
        internal static async Task<Image<Rgba32>> GetImageForUserAsync(DiscordMember user)
        {
            using (var stream = await _httpClient.GetStreamAsync(user.GetAvatarUrl(DSharpPlus.ImageFormat.Png, 64)))
            {
                return Image.Load(stream);
            }
        }

        internal static async Task SendTemporaryMessage(DiscordChannel c, string mess = null, DiscordEmbed emb = null, int timeout = 5_000)
        {
            await Task.Yield();
            new Task(async () =>
            {
                var message = await c.SendMessageAsync(mess, embed: emb);
                await Task.Delay(timeout);
                await message.DeleteAsync();
            }).Start();
        }

        internal static void ApplyRoundedCorners(Image<Rgba32> img, float cornerRadius)
        {
            var corners = BuildCorners(img.Width, img.Height, cornerRadius);

            var graphicOptions = new GraphicsOptions(true)
            {
                BlenderMode = PixelBlenderMode.Src // enforces that any part of this shape that has color is punched out of the background
            };
            // mutating in here as we already have a cloned original
            img.Mutate(x => x.Fill(graphicOptions, Rgba32.Transparent, corners));
        }

        internal static IPathCollection BuildCorners(int imageWidth, int imageHeight, float cornerRadius)
        {
            // first create a square
            var rect = new RectangularPolygon(-0.5f, -0.5f, cornerRadius, cornerRadius);

            // then cut out of the square a circle so we are left with a corner
            var cornerToptLeft = rect.Clip(new EllipsePolygon(cornerRadius - 0.5f, cornerRadius - 0.5f, cornerRadius));

            // corner is now a corner shape positions top left
            //lets make 3 more positioned correctly, we can do that by translating the orgional artound the center of the image
            var center = new Vector2(imageWidth / 2F, imageHeight / 2F);

            var rightPos = imageWidth - cornerToptLeft.Bounds.Width + 1;
            var bottomPos = imageHeight - cornerToptLeft.Bounds.Height + 1;

            // move it across the widthof the image - the width of the shape
            var cornerTopRight = cornerToptLeft.RotateDegree(90).Translate(rightPos, 0);
            var cornerBottomLeft = cornerToptLeft.RotateDegree(-90).Translate(0, bottomPos);
            var cornerBottomRight = cornerToptLeft.RotateDegree(180).Translate(rightPos, bottomPos);

            return new PathCollection(cornerToptLeft, cornerBottomLeft, cornerTopRight, cornerBottomRight);
        }
    }
}
