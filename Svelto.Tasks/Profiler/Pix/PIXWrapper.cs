using System;
using System.Runtime.InteropServices;

public static class PixWrapper  
{
    [DllImport("PixWrapperUnity.dll")]
    public static extern void PIXBeginEventEx(UInt64 color, string text);
    [DllImport("PixWrapperUnity.dll")]
    public static extern void PIXEndEventEx();
}  