using AtmaFileSystem;
using Buzz.MachineInterface;
using BuzzGUI.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ReBuzzTests.Automation
{
    public class DynamicCompiler
    {
        public static void CompileAndSave(string code, AbsoluteFilePath assemblyLocation)
        {
            // Referencing these types only to ensure the dlls containing them are loaded
            _ = new[]
            {
                typeof(object),
                typeof(INotifyPropertyChanged),
                typeof(PropertyChangedEventHandler),
                typeof(int),
                typeof(double),
                typeof(Path),
                typeof(IBuzzMachine),
                typeof(AbsoluteFilePath),
                typeof(MenuItemVM),
                typeof(Attribute),
                typeof(Attribute),
                typeof(Enumerable),
                typeof(System.Runtime.GCSettings)
            };

            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);

            PortableExecutableReference[] references = AppDomain.CurrentDomain
                .GetAssemblies()
                .Distinct()
                .Where(a => !a.IsDynamic)
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToArray();

            var compilation = CSharpCompilation.Create(
                assemblyLocation.FileName().ToString(),
                [syntaxTree],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Compilation failed {Environment.NewLine}{string.Join(Environment.NewLine, ErrorsFrom(result))}");
            }

            ms.Seek(0, SeekOrigin.Begin);

            File.WriteAllBytes(assemblyLocation.ToString(), ms.ToArray());
        }

        private static IEnumerable<Diagnostic> ErrorsFrom(EmitResult result)
        {
            return result.Diagnostics.Where(diagnostic =>
                diagnostic.IsWarningAsError ||
                diagnostic.Severity == DiagnosticSeverity.Error);
        }
    }
}