using BuzzGUI.Common;
using ReBuzz.Common;
using System;
using System.Windows;

namespace BespokeFusion
{
    // Based on https://github.com/denpalrius/Material-Message-Box

    //    The MIT License(MIT)
    //
    // Copyright(c) 2021, Bespoke Fusion
    //
    // Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
    // to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
    // and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
    //
    // The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

    /// <summary>
    /// Interaction logic for MessageBoxWindow.xaml
    /// </summary>
    public partial class MessageBoxWindow: IDisposable
    {
        public MessageBoxResult Result { get; set; }

        public MessageBoxWindow()
        {
            InitializeComponent();
            Result = MessageBoxResult.Cancel;
        }
        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        private void BtnCopyMessage_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
               Clipboard.SetText(TxtMessage.Text);
            }
            catch (Exception ex)
            {
                _ = ex.Message;
            }
        }

        private void TitleBackgroundPanel_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        public static MessageBoxResult ShowYesNoWindow(string title, string message, bool isRTL = false)
        {
            using (MessageBoxWindow msg = new MessageBoxWindow())
            {
                msg.Title = title;
                msg.TxtTitle.Text = "";
                msg.TxtMessage.Text = message;
                msg.BtnOk.Content = "Yes";
                msg.BtnCancel.Content = "No";
                if (isRTL)
                {
                    msg.FlowDirection = FlowDirection.RightToLeft;
                }
                msg.BtnOk.Focus();

                var md = Utils.GetUserControlXAML<Window>("ParameterWindowShell.xaml", Global.BuzzPath);
                if (md != null)
                {
                    msg.Resources.MergedDictionaries.Add(md.Resources);
                }

                msg.ShowDialog();
                return msg.Result == MessageBoxResult.OK ? MessageBoxResult.Yes : MessageBoxResult.No;
            }
        }
    }
}
