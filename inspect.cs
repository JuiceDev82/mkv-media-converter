using System;
using System.IO;
using System.Linq;
using System.Reflection;
var paths = new[] {
Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\\.nuget\\packages\\jellyfin.controller\\10.11.6\\lib\\net9.0\\MediaBrowser.Controller.dll"),
Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\\.nuget\\packages\\jellyfin.common\\10.11.6\\lib\\net9.0\\MediaBrowser.Common.dll"),
Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\\.nuget\\packages\\jellyfin.model\\10.11.6\\lib\\net9.0\\MediaBrowser.Model.dll")
};
foreach (var path in paths)
{
    var asm = Assembly.LoadFrom(path);
    Console.WriteLine("ASM: " + Path.GetFileName(path));
    foreach (var t in asm.GetExportedTypes().Where(t => t.Name.Contains("Controller") || t.Name.Contains("Api") || t.Name.Contains("Page") || t.Name.Contains("WebPages")))
    {
        Console.WriteLine(t.FullName);
    }
}
