using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace TwitchChat
{
    /// <summary>
    /// Interaction logic for PurgeWindow.xaml
    /// </summary>
    public partial class PurgeWindow : Window, INotifyPropertyChanged
    {
        bool m_onTop;
        bool m_ban, m_oneTime = true;
        string m_text, m_durationText;


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        public PurgeWindow(Window parent, string text, int duration)
        {
            InitializeComponent();

            if (text != null)
                Text = text;

            DurationText = "1";
            if (duration < 0)
                Ban = true;
            else if (duration > 1)
                DurationText = duration.ToString();

            TextBox.Focus();


            Left = parent.Left + (parent.Width - ActualWidth) / 2;
            Top = parent.Top + (parent.Height - ActualHeight) / 2;

        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public string DurationText
        {
            get
            {
                return m_durationText;
            }
            set
            {
                if (m_durationText != value)
                {
                    m_durationText = value;
                    OnPropertyChanged("DurationText");
                }
            }
        }

        public string Text
        {
            get
            {
                return m_text;
            }
            set
            {
                if (m_text != value)
                {
                    m_text = value;
                    OnPropertyChanged("Text");
                }
            }
        }

        public bool Ban
        {
            get
            {
                return m_ban;
            }
            set
            {
                if (m_ban != value)
                {
                    DurationBox.IsEnabled = !value;

                    m_ban = value;
                    OnPropertyChanged("Ban");
                }
            }
        }

        public bool OneTime
        {
            get
            {
                return m_oneTime;
            }
            set
            {
                if (m_oneTime != value)
                {
                    m_oneTime = value;
                    OnPropertyChanged("OneTime");
                }
            }
        }

        public bool OnTop
        {
            get
            {
                return m_onTop;
            }
            set
            {
                if (m_onTop != value)
                {
                    m_onTop = value;

                    if (value)
                    {
                        this.Topmost = true;
                        this.Deactivated += Window_Deactivated;
                    }
                    else
                    {
                        this.Topmost = false;
                        this.Deactivated -= Window_Deactivated;
                    }
                }
            }
        }

        void Window_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
        }
    }
}
