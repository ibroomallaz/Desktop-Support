using System;
using System.Collections.Generic;

namespace DSAMVVM.Core.Models
{
    public sealed class StatusItemBuilder
    {
        private readonly List<StatusSpan> _spans = [];
        private string _key = $"msg:{DateTime.UtcNow:yyyyMMddHHmmss-ffff}";
        private StatusLevel _level = StatusLevel.Info;
        private bool _sticky;
        private int _priority;

        public StatusItemBuilder Key(string key) { _key = key; return this; }
        public StatusItemBuilder Level(StatusLevel level) { _level = level; return this; }
        public StatusItemBuilder Sticky(bool sticky = true) { _sticky = sticky; return this; }
        public StatusItemBuilder Priority(int p) { _priority = p; return this; }

        public StatusItemBuilder Text(string t) { _spans.Add(StatusSpans.Text(t)); return this; }
        public StatusItemBuilder Bold(string t) { _spans.Add(StatusSpans.Bold(t)); return this; }
        public StatusItemBuilder Colored(string t, string color) { _spans.Add(StatusSpans.Colored(t, color)); return this; }
        public StatusItemBuilder Link(string t, Uri uri) { _spans.Add(StatusSpans.Link(t, uri)); return this; }
        public StatusItemBuilder Action(string t, string cmd, string? arg = null) { _spans.Add(StatusSpans.Action(t, cmd, arg)); return this; }
        public StatusItemBuilder Tooltip(string t, string tooltip) { _spans.Add(StatusSpans.Tooltip(t, tooltip)); return this; }
        public StatusItemBuilder Space() { _spans.Add(StatusSpans.Text(" ")); return this; }

        public StatusItem Build() => new(_key, _level, _sticky, _priority, _spans);
    }
}
