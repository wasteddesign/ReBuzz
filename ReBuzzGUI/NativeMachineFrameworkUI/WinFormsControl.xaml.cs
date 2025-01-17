using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Input;


namespace NativeMachineFrameworkUI
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class WinFormsControl : System.Windows.Controls.UserControl
    {
        public WinFormsControl(System.Windows.Forms.UserControl control)
        {
            _control = control;
            InitializeComponent();
        }

        override protected void OnGotFocus(RoutedEventArgs args) 
        {
            _control.Focus();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Create the interop host control.
            System.Windows.Forms.Integration.WindowsFormsHost host =
                new System.Windows.Forms.Integration.WindowsFormsHost();

            // Assign the MaskedTextBox control as the host control's child.
            host.Child = _control;

            // Add the interop host control to the Grid
            // control's collection of child controls.
            this.NativeMachineGrid.Children.Add(host);

            host.KeyDown += Host_KeyDown;
            host.KeyUp += Host_Keyup;

            //Resize
            resize();
        }

        private void CallWinformsUserControlEvent(String eventName, object sender, object eventArgs)
        {
            Type objType = _control.GetType();
            if (objType != null)
            {
                /*MemberInfo[] members = objType.GetMembers(System.Reflection.BindingFlags.Instance |
                                                          System.Reflection.BindingFlags.FlattenHierarchy |
                                                          System.Reflection.BindingFlags.Public);


                var foundMembers = members.Where(x => x.Name == field);
                if (foundMembers.Count() != 1)
                    return;
                */
                /*FieldInfo? eventDelegateField = objType.GetField(field, System.Reflection.BindingFlags.Instance |
                                                                        System.Reflection.BindingFlags.FlattenHierarchy |
                                                                       System.Reflection.BindingFlags.Public);
                */

                PropertyInfo? propertyInfo = objType.GetProperty("Events", 
                                                                BindingFlags.NonPublic | 
                                                                BindingFlags.Static | 
                                                                BindingFlags.Instance);
                if (propertyInfo != null)
                {
                    EventHandlerList? eventHandlerList = propertyInfo.GetValue(_control, new object[] { }) as EventHandlerList;
               
                    if(eventHandlerList != null)
                    {
                        var eventFields = typeof(System.Windows.Forms.Control).GetFields(BindingFlags.NonPublic | BindingFlags.Static);

                        /*FieldInfo? fieldInfo = typeof(System.Windows.Forms.Control).GetField("Event" + eventName, 
                                                                                            BindingFlags.NonPublic | 
                                                                                            BindingFlags.Static);*/

                        var findFieldInfo = eventFields.Where(x => x.GetType().IsAssignableTo(typeof(FieldInfo)) && x.Name.Contains("s_" + eventName + ""));

                        FieldInfo? fieldInfo= findFieldInfo.First() as FieldInfo;
                        if (fieldInfo != null)
                        {
                            object? eventKey = fieldInfo.GetValue(_control);
                            if (eventKey != null)
                            {
                                Delegate? eventHandler = eventHandlerList[eventKey] as Delegate;
                                if (eventHandler != null)
                                {
                                    Delegate[] invocationList = eventHandler.GetInvocationList();
                                    foreach(var iv in invocationList)
                                    {
                                        iv.Method.Invoke(iv.Target, new object[] { sender, eventArgs });
                                    }
                                }
                            }
                        }
                    }
                }
            }

        }
            
        private void Host_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //Convert key event to Winforms
            var formsKey = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
            System.Windows.Forms.KeyEventArgs formsEvt = new System.Windows.Forms.KeyEventArgs(formsKey);
            
            //Pass the event to the WinForms child control
            CallWinformsUserControlEvent("keyDown", sender, formsEvt);
            e.Handled = true;
        }

        private void Host_Keyup(object sender, System.Windows.Input.KeyEventArgs e)
        {
            //Convert key event to Winforms
            var formsKey = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
            System.Windows.Forms.KeyEventArgs formsEvt = new System.Windows.Forms.KeyEventArgs(formsKey);

            //Pass the event to the WinForms child control
            CallWinformsUserControlEvent("keyUp", sender, formsEvt);
            e.Handled = true;
        }

        private void resize()
        {
            if (this.NativeMachineGrid.Children.Count != 0)
            {
                this.NativeMachineGrid.Children[0].SetValue(Window.WidthProperty, this.ActualWidth);
                this.NativeMachineGrid.Children[0].SetValue(Window.HeightProperty, this.ActualHeight);
            }
        }

        private void UserControl_SizeChanged(object sender, RoutedEventArgs e)
        {
            resize();
        }

        private System.Windows.Forms.UserControl _control;
    }

}
