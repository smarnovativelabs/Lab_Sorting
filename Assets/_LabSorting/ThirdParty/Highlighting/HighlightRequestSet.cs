using System;
using System.Collections.Generic;

namespace IAmJanitor.Rendering.Highlighting
{
    public enum HighlightChannel { Task = 0, Hover = 1, Scripted = 2 }

    /// <summary>Small ownership ledger. Removing one request never cancels another owner's request.</summary>
    public sealed class HighlightRequestSet<T> where T : IEquatable<T>
    {
        private struct Entry
        {
            public int Owner;
            public HighlightChannel Channel;
            public int Priority;
            public long Order;
            public T Value;
        }

        private readonly List<Entry> entries = new List<Entry>(4);
        private long order;
        public int Count => entries.Count;

        public bool Set(int owner, HighlightChannel channel, int priority, T value)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.Owner != owner || entry.Channel != channel) continue;
                if (entry.Priority == priority && entry.Value.Equals(value)) return false;
                entry.Priority = priority;
                entry.Value = value;
                entry.Order = ++order;
                entries[i] = entry;
                return true;
            }
            entries.Add(new Entry { Owner = owner, Channel = channel, Priority = priority, Order = ++order, Value = value });
            return true;
        }

        public bool Release(int owner, HighlightChannel channel)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Owner != owner || entries[i].Channel != channel) continue;
                entries.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool ReleaseAll(int owner)
        {
            bool changed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Owner != owner) continue;
                entries.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        public bool TryGetWinner(out T value)
        {
            if (entries.Count == 0) { value = default; return false; }
            Entry winner = entries[0];
            for (int i = 1; i < entries.Count; i++)
            {
                Entry candidate = entries[i];
                if (candidate.Priority > winner.Priority || candidate.Priority == winner.Priority && candidate.Order > winner.Order)
                    winner = candidate;
            }
            value = winner.Value;
            return true;
        }

        public void Clear() => entries.Clear();
    }
}
