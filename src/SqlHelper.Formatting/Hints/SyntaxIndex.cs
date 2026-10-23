using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace SqlHelper.Formatting.Hints
{
    /// <summary>
    /// ScriptDom's tree with the links it doesn't have: every fragment's parent, and its children in script order.
    /// ScriptDom's own visitor gives neither, and most hints depend on where a fragment sits (in a <c>WHERE</c>, under a
    /// <c>WHILE</c>, inside a subquery).
    /// </summary>
    internal sealed class SyntaxIndex
    {
        static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties = new ConcurrentDictionary<Type, PropertyInfo[]>();

        readonly Dictionary<TSqlFragment, TSqlFragment> _parents = new Dictionary<TSqlFragment, TSqlFragment>(ReferenceComparer.Instance);
        readonly Dictionary<TSqlFragment, List<TSqlFragment>> _children = new Dictionary<TSqlFragment, List<TSqlFragment>>(ReferenceComparer.Instance);
        readonly List<TSqlFragment> _all = new List<TSqlFragment>();

        public SyntaxIndex(TSqlScript script)
        {
            Script = script;
            _all.Add(script);
            var pending = new Stack<TSqlFragment>();
            pending.Push(script);
            while (pending.Count > 0)
            {
                var fragment = pending.Pop();
                var children = new List<TSqlFragment>();
                foreach (var child in ChildrenOf(fragment))
                {
                    // ScriptDom shares some fragments between properties (a SchemaObjectName's BaseIdentifier is also in
                    // its Identifiers); the first parent found keeps them.
                    if (_parents.ContainsKey(child) || ReferenceEquals(child, script)) continue;
                    _parents[child] = fragment;
                    children.Add(child);
                    _all.Add(child);
                    pending.Push(child);
                }
                children.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
                _children[fragment] = children;
            }
            _all.Sort((a, b) => a.StartOffset != b.StartOffset ? a.StartOffset.CompareTo(b.StartOffset) : b.FragmentLength.CompareTo(a.FragmentLength));
        }

        public TSqlScript Script { get; }

        /// <summary>Every fragment, in script order (an outer fragment before the ones it starts with).</summary>
        public IEnumerable<T> All<T>() where T : TSqlFragment => _all.OfType<T>();

        public TSqlFragment Parent(TSqlFragment fragment) => _parents.TryGetValue(fragment, out var parent) ? parent : null;

        public IEnumerable<TSqlFragment> Ancestors(TSqlFragment fragment)
        {
            for (var parent = Parent(fragment); parent != null; parent = Parent(parent)) yield return parent;
        }

        public T Ancestor<T>(TSqlFragment fragment) where T : TSqlFragment => Ancestors(fragment).OfType<T>().FirstOrDefault();

        public IReadOnlyList<TSqlFragment> Children(TSqlFragment fragment) =>
            _children.TryGetValue(fragment, out var children) ? children : (IReadOnlyList<TSqlFragment>)new TSqlFragment[0];

        /// <summary>
        /// The fragments under <paramref name="fragment"/>, in script order. <paramref name="enter"/>, if given, says
        /// whether to look inside a descendant (it is returned either way).
        /// </summary>
        public IEnumerable<TSqlFragment> Descendants(TSqlFragment fragment, Func<TSqlFragment, bool> enter = null)
        {
            foreach (var child in Children(fragment))
            {
                yield return child;
                if (enter != null && !enter(child)) continue;
                foreach (var descendant in Descendants(child, enter)) yield return descendant;
            }
        }

        static IEnumerable<TSqlFragment> ChildrenOf(TSqlFragment fragment)
        {
            foreach (var property in ChildProperties.GetOrAdd(fragment.GetType(), FindChildProperties))
            {
                object value = property.GetValue(fragment);
                if (value is TSqlFragment child) yield return child;
                else if (value is IEnumerable list)
                    foreach (var item in list)
                        if (item is TSqlFragment listed) yield return listed;
            }
        }

        static PropertyInfo[] FindChildProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.DeclaringType != typeof(TSqlFragment)
                    && (typeof(TSqlFragment).IsAssignableFrom(p.PropertyType)
                        || (p.PropertyType.IsGenericType && typeof(IEnumerable).IsAssignableFrom(p.PropertyType)
                            && typeof(TSqlFragment).IsAssignableFrom(p.PropertyType.GetGenericArguments()[0]))))
                .ToArray();

        sealed class ReferenceComparer : IEqualityComparer<TSqlFragment>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(TSqlFragment x, TSqlFragment y) => ReferenceEquals(x, y);

            public int GetHashCode(TSqlFragment obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}