using ModernSequenceEditor.Interfaces;
using System.Windows.Controls;

namespace EnvelopeBlock
{

    /// <summary>
    /// Interface for envelopes
    /// </summary>
    interface IEnvelopeLayer
    {
        void Init(EnvelopeBlockMachine ab, Envelopes c, int paramIndex, SequencerLayout layoutMode);
        void SetLenghtInSeconds(double len);
        bool EnvelopeVisible { get; set; }
        void Draw();
        double DrawLengthInSeconds { get; set; }
        void ResetEnvelopeBox(EnvelopeBox envelopeBox);
        void DeleteEnvelopeBox(EnvelopeBox envelopeBox);
        void SetEnvelopeBoxValue(EnvelopeBox envelopeBox);
        MenuItem CreateEnvelopeMenu();
        void SetFreezed(EnvelopeBox envelopeBox, bool v);
        void EnvelopeBoxMouseLeave();
        void EnvelopeBoxMouseEnter();
    }
}
