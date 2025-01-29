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
using System.Linq;
using Microsoft.CodeAnalysis.Rename;

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
					equivalenceKey: title
				);

				context.RegisterCodeFix(codeAction, context.Diagnostics);
			}
		}

		private static async Task<Solution> ConvertToForeachLoop(Document document, SyntaxNode nodeToFix, CancellationToken cancellationToken)
		{
			var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

			if (
				TryGetForEachExtensionParts(nodeToFix, semanticModel, out var invocation, out var source, out var parameters, out var lambda)
			)
			{
				DocumentId documentId = document.Id;
				
				if (parameters[0].Name == "_")
				{
					SyntaxAnnotation marker = new SyntaxAnnotation("marker");
					SolutionEditor solutionEditor = new SolutionEditor(document.Project.Solution);
					var docEditor = await solutionEditor.GetDocumentEditorAsync(documentId);
					docEditor.ReplaceNode(nodeToFix, nodeToFix.WithAdditionalAnnotations(marker));
					docEditor.GetChangedDocument();
					Solution solution = solutionEditor.GetChangedSolution();
					document = solution.GetDocument(documentId);
					nodeToFix = (await document.GetSyntaxRootAsync()).GetAnnotatedNodes(marker.Kind).FirstOrDefault();
					semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

					if (
						nodeToFix !=null && 
						TryGetForEachExtensionParts(nodeToFix, semanticModel, out invocation, out source, out parameters, out lambda)
					)
					{
						solution = await Renamer.RenameSymbolAsync(
							solution,
							parameters[0],
							new SymbolRenameOptions(),
							"item",
							cancellationToken
						);

						document = solution.GetDocument(documentId);
						nodeToFix = (await document.GetSyntaxRootAsync()).GetAnnotatedNodes(marker.Kind).FirstOrDefault();
						semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

						if (
							nodeToFix == null ||
							!TryGetForEachExtensionParts(nodeToFix, semanticModel, out invocation, out source, out parameters, out lambda)
						)
						{
							return solutionEditor.OriginalSolution;
						}
					}
				}

				IBlockOperation body = lambda.Body;

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

				IParameterSymbol parameter = parameters[0];

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


				SolutionEditor solutionEditor2 = new SolutionEditor(document.Project.Solution);
				DocumentEditor editor = await solutionEditor2.GetDocumentEditorAsync(documentId, cancellationToken).ConfigureAwait(false);

				editor.ReplaceNode(nodeToFix, newNode);

				return solutionEditor2.GetChangedSolution();

			}
			else
			{
				return document.Project.Solution;
			}
		}

		private static bool TryGetForEachExtensionParts(
			SyntaxNode nodeToFix, 
			SemanticModel semanticModel, 
			out IInvocationOperation invocation, 
			out IOperation source, 
			out ImmutableArray<IParameterSymbol> parameters, 
			out IAnonymousFunctionOperation lambda
		)
		{
			lambda = null;
			if (
				ForEachExtensionAnalyzer.TryGetForEachExtensionParts(
					semanticModel.GetOperation(nodeToFix),
					out invocation,
					out source,
					out IDelegateCreationOperation action
				) &&
				action.Target is IAnonymousFunctionOperation { Symbol: { Parameters: { Length: 1 } parameterSymbols, ReturnsVoid: true } } anonymousFunctionOperation )
			{
				parameters = parameterSymbols;
				lambda = anonymousFunctionOperation;
				return true;
			}
			return false;

		}
	}
}
