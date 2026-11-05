// Ordinary types can use file-scoped namespaces.
// Changing this to a block-scoped namespace will report IDE0161.
namespace Game;

public sealed class PlayerData
{
    public int Score { get; set; }
}