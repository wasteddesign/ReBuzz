using AtmaFileSystem;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ReBuzzTests.Automation;

public class DynamicCompiler
{
    public static void CompileAndSave(string code, AbsoluteFilePath assemblyLocation)
    {
        // Referencing these types only to ensure the dlls containing them are loaded
        _ = new[]
        {
            typeof(object), typeof(INotifyPropertyChanged), typeof(PropertyChangedEventHandler), typeof(int),
            typeof(double), typeof(Path), typeof(IBuzzMachine), typeof(AbsoluteFilePath), typeof(MenuItemVM),
            typeof(Attribute), typeof(Attribute), typeof(Enumerable), typeof(System.Runtime.GCSettings)
        };

        var syntaxTree = CSharpSyntaxTree.ParseText(code);

        var references = AppDomain.CurrentDomain
            .GetAssemblies()
            .Distinct()
            .Where(a => !a.IsDynamic)
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "DynamicAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var failures = result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);

            throw new InvalidOperationException(
                $"Compilation failed {Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }

        ms.Seek(0, SeekOrigin.Begin);

        while (true)
        {
            try
            {
                File.WriteAllBytes(assemblyLocation.ToString(), ms.ToArray());
                break;
            }
            catch (IOException)
            {
                TestContext.Progress.WriteLine("Waiting for file to not be needed by another process...");
            }
        }

    }
}