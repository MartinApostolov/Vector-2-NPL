namespace Vector.Npl.Semantics;

using System.Text;
using System.Text.Json;

public static class SemanticJson
{
    public static string Serialize(SemanticExpression? expression)
    {
        IReadOnlyList<SemanticValidationIssue> issues = SemanticValidator.Validate(expression);
        if (issues.Count != 0)
        {
            throw new SemanticValidationException(issues);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            WriteExpression(writer, expression!);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static SemanticExpression Deserialize(string json)
    {
        if (TryDeserialize(json, out SemanticExpression? expression, out IReadOnlyList<SemanticValidationIssue> issues))
        {
            return expression!;
        }

        throw new SemanticJsonException(issues);
    }

    public static bool TryDeserialize(
        string? json,
        out SemanticExpression? expression,
        out IReadOnlyList<SemanticValidationIssue> issues)
    {
        expression = null;
        var collectedIssues = new List<SemanticValidationIssue>();

        if (json is null)
        {
            collectedIssues.Add(new SemanticValidationIssue(
                "JSON_REQUIRED",
                "$",
                "Semantic JSON is required."));
            issues = collectedIssues;
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });

            expression = ReadExpression(document.RootElement, "$", collectedIssues);
        }
        catch (JsonException exception)
        {
            collectedIssues.Add(new SemanticValidationIssue(
                "JSON_MALFORMED",
                "$",
                exception.Message));
        }

        if (collectedIssues.Count == 0)
        {
            collectedIssues.AddRange(SemanticValidator.Validate(expression));
        }

        if (collectedIssues.Count != 0)
        {
            expression = null;
        }

        issues = collectedIssues;
        return expression is not null;
    }

    private static void WriteExpression(Utf8JsonWriter writer, SemanticExpression expression)
    {
        writer.WriteStartObject();

        switch (expression)
        {
            case Comparison comparison:
                writer.WriteString("type", "comparison");
                writer.WriteString("kind", comparison.Kind.ToString());
                writer.WritePropertyName("left");
                WriteExpression(writer, comparison.Left!);
                writer.WritePropertyName("right");
                WriteExpression(writer, comparison.Right!);
                break;

            case Identifier identifier:
                writer.WriteString("type", "identifier");
                writer.WriteString("name", identifier.Name);
                break;

            case NumberLiteral number:
                writer.WriteString("type", "numberLiteral");
                writer.WriteNumber("value", number.Value);
                break;

            case TextLiteral text:
                writer.WriteString("type", "textLiteral");
                writer.WriteString("value", text.Value);
                break;

            case BooleanLiteral boolean:
                writer.WriteString("type", "booleanLiteral");
                writer.WriteBoolean("value", boolean.Value);
                break;

            case CurrentItem:
                writer.WriteString("type", "currentItem");
                break;

            default:
                throw new InvalidOperationException("Validation allowed an unsupported semantic node.");
        }

        writer.WriteEndObject();
    }

    private static SemanticExpression? ReadExpression(
        JsonElement element,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            AddIssue(issues, "JSON_NODE_OBJECT_REQUIRED", path, "A semantic node must be a JSON object.");
            return null;
        }

        Dictionary<string, JsonElement> properties = ReadProperties(element, path, issues);
        if (!properties.TryGetValue("type", out JsonElement typeElement))
        {
            AddIssue(issues, "JSON_MEMBER_REQUIRED", $"{path}.type", "The 'type' member is required.");
            return null;
        }

        if (typeElement.ValueKind != JsonValueKind.String)
        {
            AddIssue(issues, "JSON_STRING_REQUIRED", $"{path}.type", "The 'type' member must be a string.");
            return null;
        }

        string? type = typeElement.GetString();
        return type switch
        {
            "comparison" => ReadComparison(properties, path, issues),
            "identifier" => ReadIdentifier(properties, path, issues),
            "numberLiteral" => ReadNumberLiteral(properties, path, issues),
            "textLiteral" => ReadTextLiteral(properties, path, issues),
            "booleanLiteral" => ReadBooleanLiteral(properties, path, issues),
            "currentItem" => ReadCurrentItem(properties, path, issues),
            _ => RejectUnknownType(type, path, issues),
        };
    }

    private static Comparison? ReadComparison(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type", "kind", "left", "right");

        ComparisonKind kind = default;
        bool hasKind = TryReadRequiredString(properties, "kind", path, issues, out string? kindText);
        if (hasKind && (!Enum.TryParse(kindText, ignoreCase: false, out kind) || !Enum.IsDefined(kind)))
        {
            AddIssue(issues, "JSON_COMPARISON_KIND_INVALID", $"{path}.kind", "The comparison kind is not supported.");
            hasKind = false;
        }

        SemanticExpression? left = ReadRequiredExpression(properties, "left", path, issues);
        SemanticExpression? right = ReadRequiredExpression(properties, "right", path, issues);

        return hasKind && left is not null && right is not null
            ? new Comparison(kind, left, right)
            : null;
    }

    private static Identifier? ReadIdentifier(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type", "name");
        return TryReadRequiredString(properties, "name", path, issues, out string? name)
            ? new Identifier(name)
            : null;
    }

    private static NumberLiteral? ReadNumberLiteral(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type", "value");
        if (!properties.TryGetValue("value", out JsonElement value))
        {
            AddIssue(issues, "JSON_MEMBER_REQUIRED", $"{path}.value", "The 'value' member is required.");
            return null;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number))
        {
            AddIssue(issues, "JSON_NUMBER_REQUIRED", $"{path}.value", "The 'value' member must be a JSON number.");
            return null;
        }

        return new NumberLiteral(number);
    }

    private static TextLiteral? ReadTextLiteral(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type", "value");
        return TryReadRequiredString(properties, "value", path, issues, out string? value)
            ? new TextLiteral(value)
            : null;
    }

    private static BooleanLiteral? ReadBooleanLiteral(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type", "value");
        if (!properties.TryGetValue("value", out JsonElement value))
        {
            AddIssue(issues, "JSON_MEMBER_REQUIRED", $"{path}.value", "The 'value' member is required.");
            return null;
        }

        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            AddIssue(issues, "JSON_BOOLEAN_REQUIRED", $"{path}.value", "The 'value' member must be a JSON boolean.");
            return null;
        }

        return new BooleanLiteral(value.GetBoolean());
    }

    private static CurrentItem ReadCurrentItem(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        RejectUnknownMembers(properties, path, issues, "type");
        return new CurrentItem();
    }

    private static SemanticExpression? ReadRequiredExpression(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        if (!properties.TryGetValue(name, out JsonElement value))
        {
            AddIssue(issues, "JSON_MEMBER_REQUIRED", $"{path}.{name}", $"The '{name}' member is required.");
            return null;
        }

        return ReadExpression(value, $"{path}.{name}", issues);
    }

    private static bool TryReadRequiredString(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name,
        string path,
        ICollection<SemanticValidationIssue> issues,
        out string? result)
    {
        result = null;
        if (!properties.TryGetValue(name, out JsonElement value))
        {
            AddIssue(issues, "JSON_MEMBER_REQUIRED", $"{path}.{name}", $"The '{name}' member is required.");
            return false;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            AddIssue(issues, "JSON_STRING_REQUIRED", $"{path}.{name}", $"The '{name}' member must be a string.");
            return false;
        }

        result = value.GetString();
        return true;
    }

    private static Dictionary<string, JsonElement> ReadProperties(
        JsonElement element,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!properties.TryAdd(property.Name, property.Value))
            {
                AddIssue(
                    issues,
                    "JSON_MEMBER_DUPLICATE",
                    $"{path}.{property.Name}",
                    $"The JSON member '{property.Name}' appears more than once.");
            }
        }

        return properties;
    }

    private static void RejectUnknownMembers(
        IReadOnlyDictionary<string, JsonElement> properties,
        string path,
        ICollection<SemanticValidationIssue> issues,
        params string[] allowedNames)
    {
        foreach (string name in properties.Keys)
        {
            if (!allowedNames.Contains(name, StringComparer.Ordinal))
            {
                AddIssue(
                    issues,
                    "JSON_MEMBER_UNKNOWN",
                    $"{path}.{name}",
                    $"The JSON member '{name}' is not allowed for this semantic node.");
            }
        }
    }

    private static SemanticExpression? RejectUnknownType(
        string? type,
        string path,
        ICollection<SemanticValidationIssue> issues)
    {
        AddIssue(
            issues,
            "JSON_NODE_TYPE_UNKNOWN",
            $"{path}.type",
            $"Semantic node type '{type}' is not supported.");
        return null;
    }

    private static void AddIssue(
        ICollection<SemanticValidationIssue> issues,
        string code,
        string path,
        string message) => issues.Add(new SemanticValidationIssue(code, path, message));
}

public sealed class SemanticJsonException : FormatException
{
    public SemanticJsonException(IReadOnlyList<SemanticValidationIssue> issues)
        : base("The semantic JSON is invalid.")
    {
        Issues = issues;
    }

    public IReadOnlyList<SemanticValidationIssue> Issues { get; }
}
