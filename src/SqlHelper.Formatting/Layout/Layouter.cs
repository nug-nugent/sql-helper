using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Layout
{
    /// <summary>
    /// Walks the ScriptDom tree and decides where lines break and how far each line is indented, writing those
    /// decisions into the <see cref="TokenLayout"/>. This is the one place layout is decided.
    /// </summary>
    /// <remarks>
    /// Convention: a method that lays out a fragment at <c>level</c> assumes the fragment's first token is (or will be)
    /// on a line indented to <c>level</c>, and never sets the break before that first token; the caller does.
    /// </remarks>
    internal sealed partial class Layouter
    {
        readonly TokenLayout _layout;
        readonly ExpressionLayout _expressions;

        public Layouter(TokenLayout layout)
        {
            _layout = layout;
            _expressions = new ExpressionLayout(this);
        }

        internal TokenLayout TokenLayout => _layout;

        public void LayOut(TSqlScript script)
        {
            for (int b = 0; b < script.Batches.Count; b++)
                LayOutStatements(script.Batches[b].Statements, 0, blankLineBeforeFirst: b > 0);

            for (int i = 0; i < _layout.Tokens.Count; i++)
                if (_layout.Is(i, TSqlTokenType.Go)) _layout.NewLine(i, 0);
        }

        /// <summary>
        /// Lays out a statement list and the blank lines between its statements. Statements between BEGIN TRAN and the
        /// COMMIT / ROLLBACK that closes it are indented one level further.
        /// </summary>
        void LayOutStatements(IList<TSqlStatement> statements, int level, bool blankLineBeforeFirst = false)
        {
            int openTransactions = 0;
            TSqlStatement previous = null;
            bool previousOpensBlock = false;

            foreach (var statement in statements)
            {
                bool closesTransaction = openTransactions > 0
                    && (statement is CommitTransactionStatement || statement is RollbackTransactionStatement);
                if (closesTransaction)
                {
                    openTransactions--;
                    level--;
                }

                LayOutStatement(statement, level);

                var separator =
                    previous == null ? (blankLineBeforeFirst ? Break.BlankLine : Break.NewLine)
                    : previousOpensBlock || closesTransaction || AreGrouped(previous, statement) ? Break.NewLine
                    : Break.BlankLine;
                int start = statement.FirstTokenIndex;
                if (_layout.Is(start - 1, TSqlTokenType.Semicolon) && (previous == null || start - 1 > previous.LastTokenIndex))
                {
                    // ;WITH and friends: a semicolon written hard against a statement stays in front of it.
                    start--;
                    _layout.NoSpaceAfter[start] = true;
                }
                _layout.SetBreak(start, separator, level);

                previousOpensBlock = statement is BeginTransactionStatement;
                if (previousOpensBlock)
                {
                    openTransactions++;
                    level++;
                }
                previous = statement;
            }
        }

        /// <summary>Consecutive single-line DECLARE, SET or PRINT statements sit together without a blank line.</summary>
        bool AreGrouped(TSqlStatement previous, TSqlStatement current)
        {
            string kind = _layout.Tokens[current.FirstTokenIndex].Text.ToUpperInvariant();
            return (kind == "DECLARE" || kind == "SET" || kind == "PRINT")
                && _layout.IsWord(previous.FirstTokenIndex, kind)
                && !_layout.SpansLines(previous)
                && !_layout.SpansLines(current);
        }

        void LayOutStatement(TSqlStatement statement, int level)
        {
            switch (statement)
            {
                case SelectStatement s: LayOutSelectStatement(s, level); break;
                case InsertStatement s: LayOutCtes(s.WithCtesAndXmlNamespaces, s.InsertSpecification, level); LayOutInsert(s.InsertSpecification, level); break;
                case UpdateStatement s: LayOutCtes(s.WithCtesAndXmlNamespaces, s.UpdateSpecification, level); LayOutUpdate(s.UpdateSpecification, level); break;
                case DeleteStatement s: LayOutCtes(s.WithCtesAndXmlNamespaces, s.DeleteSpecification, level); LayOutDelete(s.DeleteSpecification, level); break;
                case MergeStatement s: LayOutCtes(s.WithCtesAndXmlNamespaces, s.MergeSpecification, level); LayOutMerge(s.MergeSpecification, level); break;
                case IfStatement s: LayOutIf(s, level); break;
                case WhileStatement s: LayOutCondition(s.Predicate, level, level + 1); LayOutBody(s.Statement, level); break;
                case BeginEndBlockStatement s: LayOutBeginEnd(s, level); break;
                case TryCatchStatement s: LayOutTryCatch(s, level); break;
                case DeclareVariableStatement s: LayOutDeclare(s, level); break;
                case DeclareTableVariableStatement s: LayOutTableDefinition(s.Body.Definition, level); break;
                case ExecuteStatement s: LayOutExecute(s, level); break;
                case ProcedureStatementBody s: LayOutProcedure(s, level); break;
                case FunctionStatementBody s: LayOutFunction(s, level); break;
                case ViewStatementBody s: LayOutView(s, level); break;
                case CreateTableStatement s: LayOutTableDefinition(s.Definition, level); break;
                case CreateIndexStatement s: LayOutCreateIndex(s, level); break;

                // Statements that stay on one line, apart from any subqueries or CASE expressions inside them.
                case BeginTransactionStatement _:
                case CommitTransactionStatement _:
                case RollbackTransactionStatement _:
                case SaveTransactionStatement _:
                case SetVariableStatement _:
                case PredicateSetStatement _:
                case SetCommandStatement _:
                case PrintStatement _:
                case ReturnStatement _:
                case ThrowStatement _:
                case RaiseErrorStatement _:
                case UseStatement _:
                case BreakStatement _:
                case ContinueStatement _:
                case GoToStatement _:
                case LabelStatement _:
                case WaitForStatement _:
                case DropObjectsStatement _:
                case TruncateTableStatement _:
                    _expressions.LayOut(statement, level);
                    break;

                default:
                    // Not handled yet: keep the statement exactly as written, apart from keyword case.
                    _layout.VerbatimRanges.Add((statement.FirstTokenIndex, statement.LastTokenIndex));
                    break;
            }
        }

        void LayOutIf(IfStatement statement, int level)
        {
            LayOutCondition(statement.Predicate, level, level + 1);
            LayOutBody(statement.ThenStatement, level);
            if (statement.ElseStatement == null) return;

            _layout.NewLine(_layout.PreviousCode(statement.ElseStatement.FirstTokenIndex), level);
            if (statement.ElseStatement is IfStatement elseIf)
                LayOutIf(elseIf, level); // ELSE IF stays on one line
            else
                LayOutBody(statement.ElseStatement, level);
        }

        /// <summary>The body of an IF, ELSE or WHILE: a BEGIN…END block lines up with the keyword, anything else goes one level in.</summary>
        void LayOutBody(TSqlStatement body, int level)
        {
            int bodyLevel = body is BeginEndBlockStatement ? level : level + 1;
            _layout.NewLine(body.FirstTokenIndex, bodyLevel);
            LayOutStatement(body, bodyLevel);
        }

        void LayOutBeginEnd(BeginEndBlockStatement block, int level)
        {
            LayOutStatements(block.StatementList.Statements, level + 1);
            _layout.NewLine(_layout.LastBeforeSemicolon(block), level);
        }

        void LayOutTryCatch(TryCatchStatement statement, int level)
        {
            var tryStatements = statement.TryStatements.Statements;
            LayOutStatements(tryStatements, level + 1);

            // BEGIN TRY … END TRY BEGIN CATCH … END CATCH: the keywords belong to no child fragment, so find them.
            int endTry = tryStatements.Count > 0
                ? _layout.NextCode(tryStatements[tryStatements.Count - 1].LastTokenIndex)
                : _layout.NextCode(_layout.NextCode(statement.FirstTokenIndex));
            int beginCatch = _layout.NextCode(_layout.NextCode(endTry));
            _layout.NewLine(endTry, level);
            _layout.NewLine(beginCatch, level);

            LayOutStatements(statement.CatchStatements.Statements, level + 1);
            _layout.NewLine(_layout.PreviousCode(_layout.LastBeforeSemicolon(statement)), level);
        }

        void LayOutDeclare(DeclareVariableStatement statement, int level)
        {
            if (statement.Declarations.Count > 1)
                LayOutList(statement.Declarations, level + 1);
            else
                _expressions.LayOut(statement, level);
        }

        void LayOutExecute(ExecuteStatement statement, int level)
        {
            if (statement.ExecuteSpecification.ExecutableEntity is ExecutableProcedureReference procedure && procedure.Parameters.Count > 0)
                LayOutList(procedure.Parameters, level + 1);
            else
                _expressions.LayOut(statement, level);
        }

        /// <summary>
        /// One item per line at <paramref name="level"/>, with leading commas. The first item starts a new line too,
        /// unless <paramref name="firstOnNewLine"/> is false.
        /// </summary>
        void LayOutList<T>(IList<T> items, int level, bool firstOnNewLine = true) where T : TSqlFragment
        {
            for (int k = 0; k < items.Count; k++)
            {
                if (k == 0 && firstOnNewLine) _layout.NewLine(items[k].FirstTokenIndex, level);
                if (k > 0) _layout.NewLine(_layout.PreviousCode(items[k].FirstTokenIndex), level);
                _expressions.LayOut(items[k], level);
            }
        }

        /// <summary>A bracketed list: <c>(</c> stays at the end of the current line, items one level in, <c>)</c> back at <paramref name="level"/>.</summary>
        void LayOutBracketedList<T>(IList<T> items, int level) where T : TSqlFragment
        {
            if (items.Count == 0) return;
            LayOutList(items, level + 1);
            _layout.NewLine(_layout.NextCode(items[items.Count - 1].LastTokenIndex), level);
        }
    }
}
