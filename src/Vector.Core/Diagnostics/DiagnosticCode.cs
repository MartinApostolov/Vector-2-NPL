namespace Vector.Core.Diagnostics;

/// <summary>
/// Stable machine-readable identifiers for Vector diagnostics.
/// Concrete lexer, parser, and runtime codes are added with the features that report them.
/// </summary>
public enum DiagnosticCode
{
    Unspecified = 0,
    InvalidCharacter,
    MalformedNumber,
    InvalidEscapeSequence,
    UnterminatedString,
    UnterminatedBlockComment,
    UnexpectedToken,
    ExpectedExpression,
    InvalidAssignmentTarget,
    InvalidLoopControl,
    InvalidReturn,
    DuplicateParameter,
    InvalidImportPlacement
}
