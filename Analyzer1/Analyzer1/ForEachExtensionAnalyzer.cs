using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer1
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public class ForEachExtensionAnalyzer : DiagnosticAnalyzer
	{
		public static readonly DiagnosticDescriptor AN01 = new DiagnosticDescriptor(nameof(AN01),
			"ForEach can be foreach",
			"Using ForEach extension method can be a language foreach loop",
			"Performance",
			DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "Replace an extension method call which allocates a delegate with a language foreach loop."
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AN01);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			if (!Debugger.IsAttached)
			{
				context.EnableConcurrentExecution();
			}

			context.RegisterOperationAction(AnalyzeInvocationOperation, OperationKind.Invocation);
		}

		private static void AnalyzeInvocationOperation(OperationAnalysisContext context)
		{
			if (TryGetForEachExtensionParts(context.Operation, out IInvocationOperation invocation, out _, out _))
			{
				context.ReportDiagnostic(Diagnostic.Create(AN01, invocation.Syntax.GetLocation()));
			}
		}

		public static bool TryGetForEachExtensionParts(IOperation operation, out IInvocationOperation extensionInvocation, out IOperation source, out IDelegateCreationOperation delegateCreation)
		{
			source = null; 
			delegateCreation = null;
			extensionInvocation = null;
			if (operation is IInvocationOperation
				{
					TargetMethod:
					{
						Name: "ForEach",
						IsExtensionMethod: true,
						TypeParameters: { Length: 1 },
						Parameters: { Length: 2 }
					},
					Arguments: { Length: 2 } args,
				} invocation &&
				args[1].Value is IDelegateCreationOperation delegateCreation1
			)
			{
				extensionInvocation = invocation;
				source = args[0].Value;
				delegateCreation = delegateCreation1;

				return true;
			}

			return false;
		}

	}
}
