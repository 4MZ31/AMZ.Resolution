using System.Text;

namespace ResolutionTray.Core;

/// <summary>Small text KeyValues reader for installed Steam manifests; no binary app cache parsing.</summary>
public sealed class ValveKeyValues
{
    public string? Value { get; private init; }
    public Dictionary<string, ValveKeyValues> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ValveKeyValues? Child(string key) => Children.GetValueOrDefault(key);
    public string? Text(string key) => Child(key)?.Value;

    public static ValveKeyValues Parse(string text)
    {
        if (text.Length > 4_000_000) throw new FormatException("Manifeste Steam trop volumineux.");
        return new Parser(text.TrimStart('\uFEFF')).ReadObject(false, 0);
    }

    private sealed class Parser(string text)
    {
        private int cursor;
        internal ValveKeyValues ReadObject(bool nested, int depth)
        {
            if (depth > 32) throw new FormatException("Manifeste Steam trop imbriqué.");
            var node = new ValveKeyValues();
            while (true)
            {
                var key = Token();
                if (key is null) { if (nested) throw new FormatException("Objet Steam incomplet."); return node; }
                if (key == "}") { if (!nested) throw new FormatException("Accolade Steam inattendue."); return node; }
                if (key == "{") throw new FormatException("Clé Steam manquante.");
                var value = Token() ?? throw new FormatException("Valeur Steam manquante.");
                if (value == "}") throw new FormatException("Valeur Steam invalide.");
                node.Children[key] = value == "{" ? ReadObject(true, depth + 1) : new ValveKeyValues { Value = value };
            }
        }
        private string? Token()
        {
            while (cursor < text.Length)
            {
                if (char.IsWhiteSpace(text[cursor])) { cursor++; continue; }
                if (text[cursor] == '/' && cursor + 1 < text.Length && text[cursor + 1] == '/')
                {
                    while (cursor < text.Length && text[cursor] != '\n') cursor++;
                    continue;
                }
                break;
            }
            if (cursor >= text.Length) return null;
            var first = text[cursor++];
            if (first is '{' or '}') return first.ToString();
            if (first != '"')
            {
                var start = cursor - 1;
                while (cursor < text.Length && !char.IsWhiteSpace(text[cursor]) && text[cursor] is not '{' and not '}') cursor++;
                return text[start..cursor];
            }
            var token = new StringBuilder();
            while (cursor < text.Length)
            {
                var c = text[cursor++];
                if (c == '"') return token.ToString();
                if (c == '\\' && cursor < text.Length && text[cursor] is '\\' or '"') c = text[cursor++];
                token.Append(c);
            }
            throw new FormatException("Chaîne Steam incomplète.");
        }
    }
}
