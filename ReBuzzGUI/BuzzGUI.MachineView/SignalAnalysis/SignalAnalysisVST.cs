using BuzzGUI.Common;
using Jacobi.Vst.Host.Interop;
using System;
using System.Diagnostics;
using System.Windows;


namespace BuzzGUI.MachineView.SignalAnalysis
{
    internal class SignalAnalysisVST
    {
        VstPluginContext ctx;
        private string pluginPath;

        public string PluginPath { get => pluginPath; set => pluginPath = value; }
        public VstPluginContext Ctx { get => ctx; set => ctx = value; }

        public SignalAnalysisVST(string path)
        {
            this.PluginPath = path;
        }

        public VstPluginContext OpenPlugin(IntPtr hostWnd)
        {
            try
            {
                HostCommandStub hostCmdStub = new HostCommandStub();
                hostCmdStub.PluginCalled += new EventHandler<PluginCalledEventArgs>(HostCmdStub_PluginCalled);

                Ctx = VstPluginContext.Create(PluginPath, hostCmdStub);

                if (hostCmdStub.Status == -1)
                {
                    return null;
                }

                // add custom data to the context
                Ctx.Set("PluginPath", PluginPath);
                Ctx.Set("HostCmdStub", hostCmdStub);

                // actually open the plugin itself
                Ctx.PluginCommandStub.Commands.Open();
                Ctx.PluginCommandStub.Commands.SetSampleRate(Global.Buzz.SelectedAudioDriverSampleRate);
                Ctx.PluginCommandStub.Commands.EditorOpen(hostWnd);
                Ctx.PluginCommandStub.Commands.SetBypass(false);
                return Ctx;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString(), "VST Loading Error");
            }

            return null;
        }


        private void HostCmdStub_PluginCalled(object sender, PluginCalledEventArgs e)
        {
            HostCommandStub hostCmdStub = (HostCommandStub)sender;

            // can be null when called from inside the plugin main entry point.
            if (hostCmdStub.PluginContext.PluginInfo != null)
            {
                Debug.WriteLine("Plugin " + hostCmdStub.PluginContext.PluginInfo.PluginID + " called:" + e.Message);
            }
            else
            {
                Debug.WriteLine("The loading Plugin called:" + e.Message);
            }
        }

        public void Release()
        {
            if (Ctx != null)
            {
                Ctx.Dispose();
            }
        }
    }
}
