namespace Vector.Core.Bytecode;

/// <summary>
/// Identifies one instruction in Vector bytecode v1.
/// </summary>
internal enum OpCode : byte
{
    Constant,
    Nothing,
    Pop,
    Negate,
    Not,
    Add,
    Subtract,
    Multiply,
    Divide,
    Remainder,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    EnterScope,
    ExitScope,
    DeclareVariable,
    GetVariable,
    AssignVariable,
    BuildList,
    RequireList,
    RequireBoolean,
    GetIndex,
    SetIndex,
    SnapshotList,
    ListCount,
    Jump,
    JumpIfFalse,
    JumpIfTrue,
    MakeClosure,
    Call,
    Return,
    Import,
    GetQualifiedMember,
    Halt
}
