using System.ComponentModel;

namespace Pianoroll.GUI
{
    public class BindableData : INotifyPropertyChanged
    {
        int stateFlags = 0;
        public bool Playing { get { return (stateFlags & 1) != 0; } }
        public bool Recording { get { return (stateFlags & 2) != 0; } }

        public string EditorMode
        {
            get
            {
                if (Recording)
                    return "Record";
                else if (Playing)
                    return "Play";
                else
                    return "Edit";

            }
        }


        public void SetStateFlags(int sf)
        {
            if (stateFlags != sf)
            {
                stateFlags = sf;
                OnPropertyChanged("EditorMode");
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

    }
}
