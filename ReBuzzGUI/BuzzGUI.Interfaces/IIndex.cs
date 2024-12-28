namespace BuzzGUI.Interfaces
{
    public interface IIndex<K, V>
    {
        V this[K index] { get; }
    }
}
