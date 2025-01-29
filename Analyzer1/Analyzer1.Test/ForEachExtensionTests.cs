using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using VerifyVar = Analyzer1.Test.CSharpAnalyzerVerifier<Analyzer1.ForEachExtensionAnalyzer>;
using VerifyFix = Analyzer1.Test.CSharpCodeFixVerifier<Analyzer1.ForEachExtensionAnalyzer, Analyzer1.ForEachExtensionAnalyzerCodeFixProvider>;

namespace Analyzer1.Test
{
	[TestClass]
	public class ForEachExtensionTests
	{
        public const string ExtensionFragment = @"
namespace Company.Core
{
    using System;
    using System.Collections.Generic;

    public static class Extensions
    {
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
        }
	}
}
";

        public const string TemplateFragment = @"
#nullable disable
using System;
using System.Linq;
using System.Collections.Generic;

namespace Test
{
    using Company.Core;

    public static class Provider
    {
        public static void Method()
        {
            string[] values = new string[] { ""first"",""second"" };
            List<string> list = new List<string>();
            @@Statements@@
        }
    }
}

@@ExtensionFragment@@";
		private static string ReplaceFragments(string statement, params (string Name, string Value)[] pairs)
        {
            string output = null;
            foreach (var pair in pairs)
            {
	            output = (output ?? pair.Value).Replace("@@" + pair.Name + "@@", pair.Value);
			}
            output = output?.Replace("@@Statements@@", statement.TrimStart());
            return output;
        } 

		[TestMethod]
        public async Task BasicInferredExpression()
        {
            string input = @"values.ForEach(_ => list.Add(_));";

            string output = @"
            foreach (string item in values)
            {
                list.Add(item);
            }";

            var replacements = new[] {
                (nameof(TemplateFragment), TemplateFragment),
                (nameof(ExtensionFragment), ExtensionFragment),
				};

			input = ReplaceFragments(input, replacements);
			output = ReplaceFragments(output, replacements);

			DiagnosticResult[] diagnostics = new DiagnosticResult[] { 
                VerifyVar.Diagnostic(ForEachExtensionAnalyzer.AN01).WithLocation(17, 13) 
            };
			await VerifyVar.VerifyAnalyzerAsync(input, diagnostics);

            await VerifyFix.VerifyCodeFixAsync(input, diagnostics, output);
		}

		[TestMethod]
		public async Task BasicExplicitExpression()
		{
			string input = @"values.ForEach((string str) => list.Add(str));";

			string output = @"foreach (string str in values)
            {
                list.Add(str);
            }";

			var replacements = new[] {
				(nameof(TemplateFragment), TemplateFragment),
				(nameof(ExtensionFragment), ExtensionFragment),
			};

			input = ReplaceFragments(input, replacements);
			output = ReplaceFragments(output, replacements);

			DiagnosticResult[] diagnostics = new DiagnosticResult[] {
				VerifyVar.Diagnostic(ForEachExtensionAnalyzer.AN01).WithLocation(17, 13)
			};
			await VerifyVar.VerifyAnalyzerAsync(input, diagnostics);

			await VerifyFix.VerifyCodeFixAsync(input, diagnostics, output);
		}

		[TestMethod]
		public async Task BasicInferredStatement()
		{
			string input = @"values.ForEach(_ => 
            {
                list.Add(_);
            }
        );";

			string output = @"
            foreach (string item in values)
            {
                list.Add(item);
            }";

			var replacements = new[] {
				(nameof(TemplateFragment), TemplateFragment),
				(nameof(ExtensionFragment), ExtensionFragment),
			};

			input = ReplaceFragments(input, replacements);
			output = ReplaceFragments(output, replacements);

			DiagnosticResult[] diagnostics = new DiagnosticResult[] {
				VerifyVar.Diagnostic(ForEachExtensionAnalyzer.AN01).WithLocation(17, 13)
			};
			await VerifyVar.VerifyAnalyzerAsync(input, diagnostics);

			await VerifyFix.VerifyCodeFixAsync(input, diagnostics, output);
		}


   
    }
}
