using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;

namespace WamBot.Data
{
    internal class Canvas
    {
        [Key]
        public int Id { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public List<Layer> Layers { get; set; } = new List<Layer>();
    }

    [Owned]
    [Index(nameof(Position))]
    internal class Layer
    {
        [Key]
        public int Id { get; set; }
        public int CanvasId { get; set; }
        public int Position { get; set; }

        public PixelColorBlendingMode BlendingMode { get; set; }
        public double Opacity { get; set; }
        public byte[] LayerData { get; set; }
    }
}
