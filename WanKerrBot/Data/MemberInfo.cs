using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace WamBot.Data;

[Owned]
internal class ColorInfo
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public override string ToString()
    {
        return $"#{R:x2}{G:x2}{B:x2}";
    }
}

internal class MemberInfo
{
    public ulong UserId { get; set; }
    public ulong GuildId { get; set; }
    public ColorInfo Color { get; set; } = null;

    public int? CanvasId { get; set; }
    public Canvas Canvas { get; set; } = null;
}
