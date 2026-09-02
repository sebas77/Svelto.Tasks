using System.Text;
using System.Threading;

public static class FastConcatUtility
{
    static readonly ThreadLocal<StringBuilder> stringBuilder =
        new ThreadLocal<StringBuilder>(() => new StringBuilder(256));

    public static string FastConcat(this string str1, string value)
    {
        var builder = _stringBuilder;

        builder.Clear();

        builder.Append(str1).Append(value);

        return builder.ToString();
    }
    
    public static string FastConcat(this string str1, int value)
    {
        var builder = _stringBuilder;

        builder.Clear();

        builder.Append(str1).Append(value);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, uint value)
    {
        var builder = _stringBuilder;

        builder.Clear();

        builder.Append(str1).Append(value);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, long value)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1).Append(value);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, float value)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1).Append(value);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, double value)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1).Append(value);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, string str2, string str3)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1);
        builder.Append(str2);
        builder.Append(str3);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, string str2, string str3, string str4)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1);
        builder.Append(str2);
        builder.Append(str3);
        builder.Append(str4);

        return builder.ToString();
    }

    public static string FastConcat(this string str1, string str2, string str3, string str4, string str5)
    {
        var builder = _stringBuilder;

        builder.Length = 0;

        builder.Append(str1);
        builder.Append(str2);
        builder.Append(str3);
        builder.Append(str4);
        builder.Append(str5);

        return builder.ToString();
    }
    
    
    static StringBuilder _stringBuilder
    {
        get
        {
            try
            {
                return stringBuilder.Value;
            }
            catch
            {
                return new StringBuilder(); //this is just to handle finalizer that could be called after the _threadSafeStrings is finalized. So pretty rare
            }
        }
    }
}