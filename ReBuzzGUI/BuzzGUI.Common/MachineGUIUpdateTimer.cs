using System;
using System.Windows;
using System.Windows.Threading;

namespace BuzzGUI.Common
{
    public class MachineGUIUpdateTimer
    {
        const int UpdateInterval = 20;  // TODO: -> general settings

        DispatcherTimer timer;

        public MachineGUIUpdateTimer(UIElement gui, Action update)
        {
            gui.IsVisibleChanged += (sender, e) =>
            {
                if (gui.IsVisible && timer == null)
                {
                    timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromMilliseconds(UpdateInterval);
                    timer.Tick += (sender2, e2) => { update(); };
                    timer.Start();
                }
                else if (!gui.IsVisible && timer != null)
                {
                    timer.Stop();
                    timer = null;
                }
            };

        }
    }
}
