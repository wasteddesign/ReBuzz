using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ReBuzz.Common
{
    public class ConcurrentStreamWriter : StreamWriter
    {
        private readonly ConcurrentQueue<String> _stringQueue = new ConcurrentQueue<String>();
        private bool _disposing;
        private readonly TextBox _textBox;
        readonly AutoResetEvent updateEvent = new AutoResetEvent(false);

        public ConcurrentStreamWriter(Stream stream)
            : base(stream)
        {
            CreateQueueListener();
        }

        public ConcurrentStreamWriter(Stream stream, TextBox textBox)
            : this(stream)
        {
            _textBox = textBox;
        }

        public override void WriteLine()
        {
            base.WriteLine();
            _stringQueue.Enqueue(Environment.NewLine);
            updateEvent.Set();
        }

        public override void WriteLine(string value)
        {
            base.WriteLine(value);
            _stringQueue.Enqueue(String.Format("{0}\n", value));
            updateEvent.Set();
        }

        public override void Write(string value)
        {
            base.Write(value);
            _stringQueue.Enqueue(value);
            updateEvent.Set();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _disposing = disposing;
            updateEvent.Set();
        }

        private void CreateQueueListener()
        {
            var bw = new BackgroundWorker();

            bw.DoWork += (sender, args) =>
            {
                while (true)
                {
                    updateEvent.WaitOne();
                    if (_disposing)
                        break;

                    if (_stringQueue.Count > 0)
                    {
                        string value = string.Empty;
                        if (_stringQueue.TryDequeue(out value))
                        {
                            if (_textBox != null)
                            {
                                if (!Application.Current.Dispatcher.CheckAccess())
                                {
                                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        _textBox.AppendText(value);
                                        _textBox.ScrollToEnd();
                                    }));
                                }
                                else
                                {
                                    _textBox.AppendText(value);
                                    _textBox.ScrollToEnd();
                                }
                            }
                        }
                    }
                }
            };

            bw.RunWorkerAsync();

        }

    }
}
