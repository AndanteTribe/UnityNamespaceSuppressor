// Ordinary types can use file-scoped namespaces.
// Changing this to a block-scoped namespace will report IDE0161.
namespace Game;

/// <summary>
/// Demonstrates a regular type that can use a file-scoped namespace.
/// </summary>
public sealed class PlayerData
{
    /// <summary>
    /// Gets or sets the player's score.
    /// </summary>
    public int Score { get; set; }
}