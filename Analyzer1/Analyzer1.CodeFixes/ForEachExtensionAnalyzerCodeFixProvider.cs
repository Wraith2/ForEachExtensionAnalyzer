using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editing;

namespace Analyzer1
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ForEachExtensionAnalyzerCodeFixProvider))]
	public class ForEachExtensionAnalyzerCodeFixProvider : CodeFixProvider
	{
		public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(ForEachExtensionAnalyzer.AN01.Id);

		public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

			if (root.FindNode(context.Span, getInnermostNodeForTie: true) is SyntaxNode node)
			{
				string title = "Convert to foreach";
				var codeAction = CodeAction.Create(
					title,
					cancellationToken => ConvertToForeachLoop(context.Document, node, cancellationToken),
					equivalenceKey: title);

				context.RegisterCodeFix(codeAction, context.Diagnostics);
			}
		}

		private static async Task<Document> ConvertToForeachLoop(Document document, SyntaxNode nodeToFix, CancellationToken cancellationToken)
		{
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

			if (
				ForEachExtensionAnalyzer.TryGetForEachExtensionParts(
					semanticModel.GetOperation(nodeToFix),
					out IInvocationOperation invocation,
					out IOperation source,
					out IDelegateCreationOperation action
				)
			)
			{
				if (
					action.Target is IAnonymousFunctionOperation { Symbol: { Parameters: { Length: 1 } parameters, ReturnsVoid: true } } lambda
				)
				{
					IParameterSymbol parameter = parameters[0];
					IBlockOperation body = lambda.Body;

					if (parameter.Name == "_")
					{
						// how do i rename the parameter within the current document scope?
					}

					StatementSyntax bodyStatement = null;
					if (body.Syntax is ExpressionSyntax bodyExpression)
					{
						bodyStatement = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(bodyExpression));
					}
					else if (body.Syntax is StatementSyntax statementBlock)
					{
						bodyStatement = statementBlock;
					}

					if (invocation.Parent is IExpressionStatementOperation containingStatement)
					{
						nodeToFix = containingStatement.Syntax;
					}

					TypeSyntax typeSyntax = SyntaxFactory.ParseTypeName(
						parameter.Type.ToMinimalDisplayString(
							semanticModel, 
							parameter.Type.Locations[0].SourceSpan.Start, 
							SymbolDisplayFormat.MinimallyQualifiedFormat
						)
					);

					SyntaxNode newNode = SyntaxFactory.ForEachStatement(
						typeSyntax, 
						parameter.Name, 
						source.Syntax as ExpressionSyntax,
						bodyStatement
					)
						.WithTriviaFrom(nodeToFix);
					
					var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
				
					editor.ReplaceNode(nodeToFix, newNode);
					
					return editor.GetChangedDocument();
				}

				return document;
			}
			else
			{
				return document;
			}
		}

	}
}
