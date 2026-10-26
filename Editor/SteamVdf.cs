using System;
using System.Collections.Generic;

namespace KitchenLib.Studio.Editor
{
    public sealed class SteamVdfParseException : FormatException
    {
        public SteamVdfParseException(string message, int line, int column)
            : base(message + " at line " + line + ", column " + column + ".")
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }
        public int Column { get; }
    }

    public sealed class SteamVdfNode
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SteamVdfNode> _objects = new Dictionary<string, SteamVdfNode>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<KeyValuePair<string, string>> Values => _values;
        public IEnumerable<KeyValuePair<string, SteamVdfNode>> Objects => _objects;
        public string GetString(string key) => key != null && _values.TryGetValue(key, out string value) ? value : null;
        public SteamVdfNode GetObject(string key) => key != null && _objects.TryGetValue(key, out SteamVdfNode value) ? value : null;
        internal void Set(string key, string value) => _values[key] = value;
        internal void Set(string key, SteamVdfNode value) => _objects[key] = value;
    }

    public static class SteamVdf
    {
        public static SteamVdfNode Parse(string text)
        {
            var parser = new Parser(text ?? string.Empty);
            return parser.ParseRoot();
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _index;
            private int _line = 1;
            private int _column = 1;

            public Parser(string text) { _text = text; }

            public SteamVdfNode ParseRoot()
            {
                SteamVdfNode root = ParseNode(false);
                SkipTrivia();
                if (!AtEnd) Fail("Unexpected token");
                return root;
            }

            private SteamVdfNode ParseNode(bool requiresClose)
            {
                var node = new SteamVdfNode();
                while (true)
                {
                    SkipTrivia();
                    if (AtEnd)
                    {
                        if (requiresClose) Fail("Expected '}'");
                        return node;
                    }
                    if (Peek == '}')
                    {
                        if (!requiresClose) Fail("Unexpected '}'");
                        Advance();
                        return node;
                    }

                    string key = ReadQuoted();
                    SkipTrivia();
                    if (AtEnd) Fail("Expected value or object");
                    if (Peek == '{')
                    {
                        Advance();
                        node.Set(key, ParseNode(true));
                    }
                    else
                    {
                        node.Set(key, ReadQuoted());
                    }
                }
            }

            private string ReadQuoted()
            {
                SkipTrivia();
                if (AtEnd || Peek != '"') Fail("Expected quoted string");
                Advance();
                var value = new System.Text.StringBuilder();
                while (!AtEnd)
                {
                    char current = Peek;
                    Advance();
                    if (current == '"') return value.ToString();
                    if (current == '\\')
                    {
                        if (AtEnd) Fail("Unterminated escape sequence");
                        char escaped = Peek;
                        Advance();
                        value.Append(escaped == 'n' ? '\n' : escaped == 't' ? '\t' : escaped);
                    }
                    else value.Append(current);
                }
                Fail("Unterminated quoted string");
                return null;
            }

            private void SkipTrivia()
            {
                while (!AtEnd)
                {
                    if (char.IsWhiteSpace(Peek)) { Advance(); continue; }
                    if (Peek == '/' && _index + 1 < _text.Length && _text[_index + 1] == '/')
                    {
                        while (!AtEnd && Peek != '\n') Advance();
                        continue;
                    }
                    break;
                }
            }

            private bool AtEnd => _index >= _text.Length;
            private char Peek => _text[_index];

            private void Advance()
            {
                if (_text[_index++] == '\n') { _line++; _column = 1; }
                else _column++;
            }

            private void Fail(string message) { throw new SteamVdfParseException(message, _line, _column); }
        }
    }
}
