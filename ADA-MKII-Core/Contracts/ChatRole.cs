namespace ADA_MKII_Core.Contracts;

/// <summary>Who produced a message in a conversation.</summary>
public enum ChatRole
{
    System = 0,
    User = 1,
    Assistant = 2,
    Tool = 3,
}
