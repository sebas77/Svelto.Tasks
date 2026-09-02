using System.Text;

public static class StringBuilderUtils 
{
    public static StringBuilder AppendWithColor(this StringBuilder stringBuilder, string str, string color)
    {
        stringBuilder.Append("<color=" + color + ">" + str + "</color>");
        
        return stringBuilder;
    }

    // Allocation-free numeric appenders (fixed decimals for floats)
    public static StringBuilder AppendWithColor(this StringBuilder stringBuilder, float value, int decimals, string color)
    {
        stringBuilder.Append("<color=").Append(color).Append(">");
        AppendFixed(stringBuilder, value, decimals);
        stringBuilder.Append("</color>");
        return stringBuilder;
    }

    public static StringBuilder AppendWithColor(this StringBuilder stringBuilder, int value, string color)
    {
        stringBuilder.Append("<color=").Append(color).Append(">");
        AppendInteger(stringBuilder, value);
        stringBuilder.Append("</color>");
        return stringBuilder;
    }

    public static StringBuilder AppendWithColor(this StringBuilder stringBuilder, long value, string color)
    {
        stringBuilder.Append("<color=").Append(color).Append(">");
        AppendInteger(stringBuilder, value);
        stringBuilder.Append("</color>");
        return stringBuilder;
    }

    public static StringBuilder AppendValue(this StringBuilder stringBuilder, string str)
    {
        return stringBuilder.AppendWithColor(str, COLOR_VALUE);
    }

    public static StringBuilder AppendValue(this StringBuilder stringBuilder, float value, int decimals)
    {
        return stringBuilder.AppendWithColor(value, decimals, COLOR_VALUE);
    }

    public static StringBuilder AppendValue(this StringBuilder stringBuilder, int value)
    {
        return stringBuilder.AppendWithColor(value, COLOR_VALUE);
    }

    public static StringBuilder AppendValue(this StringBuilder stringBuilder, long value)
    {
        return stringBuilder.AppendWithColor(value, COLOR_VALUE);
    }

    // Helpers: avoid ToString allocations
    static void AppendFixed(StringBuilder sb, float value, int decimals)
    {
        if (value < 0f)
        {
            sb.Append('-');
            value = -value;
        }

        int pow10 = decimals == 0 ? 1 : (decimals == 1 ? 10 : (decimals == 2 ? 100 : Pow10(decimals)));
        // Round to required decimals
        long scaled = (long)System.MathF.Round(value * pow10);
        long whole = scaled / pow10;
        long frac = scaled  % pow10;

        AppendInteger(sb, whole);
        if (decimals > 0)
        {
            sb.Append('.');
            // leading zeros for fractional part
            int pad = frac == 0 ? decimals : (decimals - CountDigits((int)frac));
            for (int i = 0; i < pad; i++) sb.Append('0');
            if (frac          > 0) AppendInteger(sb, frac);
        }
    }

    static void AppendInteger(StringBuilder sb, long value)
    {
        if (value == 0)
        {
            sb.Append('0');
            return;
        }
        if (value < 0)
        {
            sb.Append('-');
            // handle long.MinValue safely
            ulong u = (ulong)(-(value + 1)) + 1UL;
            AppendUnsigned(sb, u);
            return;
        }
        AppendUnsigned(sb, (ulong)value);
    }

    static void AppendUnsigned(StringBuilder sb, ulong v)
    {
        if (v >= 10UL)
        {
            AppendUnsigned(sb, v / 10UL);
        }
        sb.Append((char)('0' + (int)(v % 10UL)));
    }

    static int CountDigits(int v)
    {
        if (v >= 1000000000) return 10;
        if (v >= 100000000) return 9;
        if (v >= 10000000) return 8;
        if (v >= 1000000) return 7;
        if (v >= 100000) return 6;
        if (v >= 10000) return 5;
        if (v >= 1000) return 4;
        if (v >= 100) return 3;
        if (v >= 10) return 2;
        return 1;
    }

    static int Pow10(int e)
    {
        int r = 1;
        for (int i = 0; i < e; i++) r *= 10;
        return r;
    }

    public static StringBuilder AppendWithColor(this StringBuilder stringBuilder, System.DateTime value, string color)
    {
        stringBuilder.Append("<color=").Append(color).Append(">");
        AppendTwoDigits(stringBuilder, value.Hour);
        stringBuilder.Append(':');
        AppendTwoDigits(stringBuilder, value.Minute);
        stringBuilder.Append(':');
        AppendTwoDigits(stringBuilder, value.Second);
        stringBuilder.Append("</color>");
        return stringBuilder;
    }

    public static StringBuilder AppendValue(this StringBuilder stringBuilder, System.DateTime value)
    {
        return stringBuilder.AppendWithColor(value, COLOR_VALUE);
    }

    static void AppendTwoDigits( StringBuilder sb, int v)
    {
        if (v >= 10)
        {
            AppendInteger(sb, v);
        }
        else
        {
            sb.Append('0');
            sb.Append((char)('0' + v));
        }
    }

    const string COLOR_VALUE = "#FFFFFF";
}