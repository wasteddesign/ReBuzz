namespace BuzzGUI.Interfaces
{
    /// <summary>
    /// Optionally implemented by machines. Gives the parameter layout a machine was saved with in the
    /// song it was loaded from. Data that refers to parameters by index, such as pattern editor data,
    /// is in that layout, which can differ from the machine's current one when a newer version of the
    /// machine adds, removes or reorders parameters.
    /// </summary>
    public interface ISavedParameterLayout
    {
        /// <summary>
        /// The number of global parameters in the saved layout, or -1 if the machine wasn't loaded
        /// from a song.
        /// </summary>
        int SavedGlobalParameterCount { get; }

        /// <summary>
        /// The current parameter for a parameter in the saved layout, given its group (0 = input,
        /// 1 = global, 2 = track) and its index in that group, or null if the machine no longer has it.
        /// </summary>
        IParameter GetCurrentParameter(int group, int savedIndexInGroup);
    }
}
