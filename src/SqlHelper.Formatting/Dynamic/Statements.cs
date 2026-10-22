using System.Collections.Generic;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Dynamic
{
    /// <summary>Finding statements, and the statement lists that hold them, anywhere in a script.</summary>
    internal static class Statements
    {
        public static List<TSqlStatement> All(TSqlFragment fragment)
        {
            var collector = new Collector();
            fragment.Accept(collector);
            return collector.Statements;
        }

        /// <summary>
        /// The list each statement sits in: a batch, <c>BEGIN … END</c>, <c>TRY</c> / <c>CATCH</c>, or a body after
        /// <c>AS</c>. A statement that is the whole body of an <c>IF</c>, <c>ELSE</c> or <c>WHILE</c> is in none.
        /// </summary>
        public static Dictionary<TSqlStatement, IList<TSqlStatement>> Containers(TSqlFragment fragment)
        {
            var collector = new Collector();
            fragment.Accept(collector);
            return collector.Containers;
        }

        sealed class Collector : TSqlFragmentVisitor
        {
            public List<TSqlStatement> Statements { get; } = new List<TSqlStatement>();
            public Dictionary<TSqlStatement, IList<TSqlStatement>> Containers { get; } = new Dictionary<TSqlStatement, IList<TSqlStatement>>();

            public override void Visit(TSqlStatement node) => Statements.Add(node);

            public override void Visit(TSqlBatch node) => Add(node.Statements);

            public override void Visit(StatementList node) => Add(node.Statements);

            void Add(IList<TSqlStatement> list)
            {
                foreach (var statement in list) Containers[statement] = list;
            }
        }
    }
}