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
        bool m_ban, m_oneTime = true, m_regex;
        string m_text, m_durationText;


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        public PurgeWindow(string text, int duration)
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
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
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

        public bool Regex
        {
            get
            {
                return m_regex;
            }
            set
            {
                if (m_regex != value)
                {
                    m_regex = value;
                    OnPropertyChanged("Regex");
                }
            }
        }
    }
}
