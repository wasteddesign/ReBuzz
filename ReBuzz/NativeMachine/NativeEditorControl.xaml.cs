using System;
using System.Windows.Controls;

namespace ReBuzz.NativeMachine
{
    /// <summary>
    /// Interaction logic for NativeEditorControl.xaml
    /// </summary>
    public partial class NativeEditorControl : UserControl
    {
        readonly NativeEditorHost editorHost;
        public NativeEditorControl(IntPtr editorHandle)
        {
            InitializeComponent();
            //editorHost = new NativeEditorHost(100, 100, editorHandle);

        }
    }
}
