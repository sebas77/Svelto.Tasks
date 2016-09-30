// Copyright (C) 2010 by Andrew Zhilin <andrew_zhilin@yahoo.com>

#region Usings

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnitLite.Runner;
using UnityEngine;


#endregion

/* NOTE:
 *
 * This is a test runner for NUnitLite, that redirects test results
 * to Unity console.
 *
 * After compilation of C# files Unity gives you two assemblies:
 *
 * - Assembly-CSharp-firstpass.dll for 'Plugins' and 'Standard Assets'
 * - Assembly-CSharp.dll           for another scripts
 *
 * (Note, that Unity uses criptic names like
 * '9cda786f9571f9a4d863974e5a5a9142')
 *
 * Then, if you want have tests in both places - you should call
 * NUnitLiteUnityRunner.RunTests() from both places. One call per assembly
 * is enough, but you can call it as many times as you want - all
 * calls after first are ignored.
 *
 * You can use 'MonoBahavior' classes for tests, but Unity give you
 * one harmless warning per class. Using special Test classes would be
 * better idea.
 */


public static class NUnitLiteUnityRunner
{
    private static readonly HashSet<Assembly> _tested =
        new HashSet<Assembly>();

    public static Action<string, string> Presenter { get; set; }

    static NUnitLiteUnityRunner()
    {
        Presenter = UnityConsolePresenter;
    }


    public static void RunTests()
    {
        RunTests(Assembly.GetCallingAssembly());
    }


    public static void RunTests(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException("assembly");

        if (_tested.Contains(assembly))
            return;
        _tested.Add(assembly);

        using (var sw = new StringWriter())
        {
            var runner = new TextUI(sw);
            runner.Execute(new[] {"/nologo", assembly.FullName});
            var resultText = sw.GetStringBuilder().ToString();
            var assemblyName = assembly.GetName().Name;
            Presenter(assemblyName, resultText);
        }
    }


    private static void UnityConsolePresenter(string assemblyName,
                                              string longResult)
    {
        var lines = longResult.Split(new[] {'\n', '\r'},
                                     StringSplitOptions.RemoveEmptyEntries);
        var shortResult = lines[0];

        var shortName = assemblyName.Substring(0, 5);

        if (shortResult.Contains("0 Fail") && shortResult.Contains("0 Err"))
        {
            Debug.Log(string.Format("{0} / Success: {1}", shortName,
                                    shortResult));
        }
        else
        {
            Debug.LogWarning(string.Format("{0} / Failure: {1}", shortName,
                                           longResult));
        }
    }
}
