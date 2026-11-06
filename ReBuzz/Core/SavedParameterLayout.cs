using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;
using static ReBuzz.FileOps.BMXFile;

namespace ReBuzz.Core
{
    /// <summary>
    /// A saved parameter, as recorded in a song.
    /// </summary>
    internal readonly record struct SavedParameter(string Name, ParameterFlags Flags);

    /// <summary>
    /// A machine's parameter layout as saved in the song it was loaded from, matched by name to the
    /// machine's current parameters (see BMXFile.MatchSavedParameters).
    /// </summary>
    internal sealed class SavedParameterLayout
    {
        // Per group: for each current parameter, the saved index, or -1
        readonly int[][] savedIndex;
        // Per group: for each saved parameter, the current index, or -1
        readonly int[][] currentIndex;
        // The saved state parameters across all groups, in the order MIDI bindings count them
        readonly List<(int Group, int IndexInGroup)> savedStateParameters = new List<(int, int)>();

        /// <param name="machine">The machine as it is now.</param>
        /// <param name="savedGroups">The saved parameters of each group: input, global, track.</param>
        public SavedParameterLayout(MachineCore machine, IList<IList<SavedParameter>> savedGroups)
        {
            int groupCount = machine.ParameterGroupsList.Count;
            savedIndex = new int[groupCount][];
            currentIndex = new int[groupCount][];

            for (int g = 0; g < groupCount; g++)
            {
                var saved = SavedParameters(savedGroups, g);
                var current = machine.ParameterGroupsList[g].ParametersList;

                savedIndex[g] = MatchSavedParameters(
                    current.Select(p => p.Name).ToList(),
                    saved.Select(p => p.Name).ToList());

                currentIndex[g] = Enumerable.Repeat(-1, saved.Count).ToArray();
                for (int i = 0; i < savedIndex[g].Length; i++)
                {
                    if (savedIndex[g][i] >= 0)
                        currentIndex[g][savedIndex[g][i]] = i;
                }
            }

            for (int g = 0; g < savedGroups.Count; g++)
            {
                var saved = SavedParameters(savedGroups, g);
                for (int i = 0; i < saved.Count; i++)
                {
                    if (saved[i].Flags.HasFlag(ParameterFlags.State))
                        savedStateParameters.Add((g, i));
                }
            }

            SavedGlobalParameterCount = SavedParameters(savedGroups, 1).Count;
        }

        /// <summary>
        /// The layout saved for a machine whose saved parameters are held by another machine instance,
        /// such as the prototype the .bmx loader builds from the PARA section.
        /// </summary>
        public static SavedParameterLayout FromSavedMachine(MachineCore machine, MachineCore savedMachine)
        {
            var savedGroups = savedMachine.ParameterGroupsList
                .Select(g => (IList<SavedParameter>)g.ParametersList.Select(p => new SavedParameter(p.Name, p.Flags)).ToList())
                .ToList();
            return new SavedParameterLayout(machine, savedGroups);
        }

        public int SavedGlobalParameterCount { get; }

        /// <summary>
        /// For each current parameter in the group, the saved index, or -1 if the song doesn't have it.
        /// </summary>
        public int[] SavedIndex(int group) => savedIndex[group];

        /// <summary>
        /// The current index of a saved parameter, or -1 if the machine no longer has it.
        /// </summary>
        public int CurrentIndex(int group, int savedIndexInGroup)
        {
            if (group < 0 || group >= currentIndex.Length)
                return -1;
            var map = currentIndex[group];
            return savedIndexInGroup >= 0 && savedIndexInGroup < map.Length ? map[savedIndexInGroup] : -1;
        }

        /// <summary>
        /// The group and index of the saved state parameter at a position counted across all groups.
        /// </summary>
        public (int Group, int IndexInGroup)? GetSavedStateParameter(int stateParameterIndex)
        {
            return stateParameterIndex >= 0 && stateParameterIndex < savedStateParameters.Count ? savedStateParameters[stateParameterIndex] : null;
        }

        static IList<SavedParameter> SavedParameters(IList<IList<SavedParameter>> savedGroups, int group)
        {
            return group < savedGroups.Count && savedGroups[group] != null ? savedGroups[group] : new List<SavedParameter>();
        }
    }
}
