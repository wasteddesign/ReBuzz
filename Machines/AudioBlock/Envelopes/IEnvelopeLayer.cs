using System.Windows.Controls;

namespace WDE.AudioBlock
{

    /// <summary>
    /// Interface for envelopes
    /// </summary>
    interface IEnvelopeLayer
    {
        void Init(AudioBlock ab, Envelopes c);
        void SetLenghtInSeconds(double len);
        bool EnvelopeVisible { get; set; }
        void SetOrientation(ViewOrientationMode vmode);
        void Draw();
        double DrawLengthInSeconds { get; set; }
        void ResetEnvelopeBox(EnvelopeBox envelopeBox);
        void DeleteEnvelopeBox(EnvelopeBox envelopeBox);
        MenuItem CreateEnvelopeMenu();
        void SetFreezed(EnvelopeBox envelopeBox, bool v);
        void EnvelopeBoxMouseLeave();
        void EnvelopeBoxMouseEnter();
    }
}
