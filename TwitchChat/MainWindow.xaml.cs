using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Winter;
using WinterBotLogging;

namespace TwitchChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        bool m_playSounds, m_confirmTimeouts, m_confirmBans, m_highlightQuestions, m_showIcons, m_onTop, m_showTimestamp;
        int m_fontSize;
        Visibility m_modButtonVisible = Visibility.Collapsed;
        Thread m_thread;
        ChatOptions m_options;
        TwitchClient m_twitch;
        TwitchUsers m_users;
        string m_channel;
        bool m_modListRequested;

        public event PropertyChangedEventHandler PropertyChanged;
        bool m_reconnect;

        SoundPlayer m_subSound = new SoundPlayer(Properties.Resources.Subscriber);
        
        public MainWindow()
        {
            if (!Debugger.IsAttached)
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            m_options = new ChatOptions();
            m_playSounds = m_options.GetOption("PlaySounds", true);
            m_highlightQuestions = m_options.GetOption("HighlightQuestions", true);
            m_confirmBans = m_options.GetOption("ConfirmBans", true);
            m_confirmTimeouts = m_options.GetOption("ConfirmTimeouts", false);
            m_showIcons = m_options.GetOption("ShowIcons", true);
            m_showTimestamp = m_options.GetOption("ShowTimestamps", false);
            m_fontSize = 14;
            OnTop = m_options.GetOption("OnTop", false);
            m_channel = m_options.Stream.ToLower();
            TwitchHttp.Instance.PollChannelData(m_channel);
            TwitchHttp.Instance.ChannelDataReceived += Instance_ChannelDataReceived;

            m_thread = new Thread(ThreadProc);
            m_thread.Start();

            Messages = new ObservableCollection<ChatItem>();

            InitializeComponent();
            Channel.Text = m_channel;
            ChatInput.Focus();
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                // This will drop an "error.txt" file if we encounter an unhandled exception.
                string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                Exception obj = (Exception)e.ExceptionObject;
                string text = string.Format("{0}: {1}\n{2}", obj.GetType().ToString(), obj.Message, obj.StackTrace.ToString());

                using (var file = File.CreateText(System.IO.Path.Combine(myDocuments, "winterbot_error.txt")))
                    file.WriteLine(text);

                MessageBox.Show(text, "Unhandled Exception");
            }
            catch
            {
                // Ignore errors here.
            }
        }

        public void Unban(TwitchUser user)
        {
            m_twitch.Unban(user.Name);
        }

        public bool Ban(TwitchUser user)
        {
            MessageBoxResult res = MessageBoxResult.Yes;
            if (ConfirmBans)
                res = MessageBox.Show(string.Format("Ban user {0}?", user.Name), "Ban User", MessageBoxButton.YesNo);

            if (res == MessageBoxResult.Yes)
            {
                m_twitch.Ban(user.Name);
                return true;
            }

            return false;
        }

        public bool Timeout(TwitchUser user, int duration)
        {
            if (duration <= 0)
                duration = 1;

            MessageBoxResult res = MessageBoxResult.Yes;
            if (ConfirmTimeouts && duration > 1)
                res = MessageBox.Show(string.Format("Timeout user {0}?", user.Name), "Timeout User", MessageBoxButton.YesNo);

            if (res == MessageBoxResult.Yes)
            {
                m_twitch.Timeout(user.Name, duration);
                return true;
            }

            return false;
        }

        public void ThreadProc()
        {
            m_twitch = new TwitchClient();

            if (!Connect())
                return;

            const int pingDelay = 20;
            DateTime lastPing = DateTime.Now;
            while (true)
            {
                Thread.Sleep(1000);

                var lastEvent = m_twitch.LastEvent;
                if (lastEvent.Elapsed().TotalSeconds >= pingDelay && lastPing.Elapsed().TotalSeconds >= pingDelay)
                {
                    m_twitch.Ping();
                    lastPing = DateTime.Now;
                }

                if (lastEvent.Elapsed().TotalMinutes >= 1)
                {
                    WinterBotSource.Log.BeginReconnect();
                    WriteStatus("Disconnected!");

                    m_twitch.Quit(250);

                    if (!Connect())
                        return;

                    WinterBotSource.Log.EndReconnect();
                }
                else if (m_reconnect)
                {
                    m_reconnect = false;
                    if (!Connect())
                        return;
                }
            }
        }


        bool Connect()
        {
            if (m_twitch != null)
            {
                var user = CurrentUser;
                if (user != null)
                    user.ModeratorStatusChanged -= ModStatusChanged;
                m_twitch.InformChatClear -= ClearChatHandler;
                m_twitch.MessageReceived -= ChatMessageReceived;
                m_twitch.JtvMessageReceived -= JtvMessageReceived;
                m_twitch.ActionReceived -= ChatActionReceived;
                m_twitch.UserSubscribed -= SubscribeHandler;
                m_twitch.StatusUpdate -= StatusUpdate;
                m_twitch.Quit();
            }

            string channel = m_channel.ToLower();
            m_users = new TwitchUsers(channel);
            m_twitch = new TwitchClient(m_users);
            m_twitch.InformChatClear += ClearChatHandler;
            m_twitch.MessageReceived += ChatMessageReceived;
            m_twitch.JtvMessageReceived += JtvMessageReceived;
            m_twitch.ActionReceived += ChatActionReceived;
            m_twitch.UserSubscribed += SubscribeHandler;
            m_twitch.StatusUpdate += StatusUpdate;

            var currUser = CurrentUser = m_users.GetUser(m_options.User);
            currUser.ModeratorStatusChanged += ModStatusChanged;
            ModStatusChanged(currUser, currUser.IsStreamer || currUser.IsModerator);
            
            bool first = true;
            ConnectResult result;
            const int sleepTime = 5000;
            do
            {
                if (!NativeMethods.IsConnectedToInternet())
                {
                    WriteStatus("Not connected to the internet.");

                    do
                    {
                        Thread.Sleep(sleepTime);
                    } while (!NativeMethods.IsConnectedToInternet());

                    WriteStatus("Re-connected to the internet.");
                }

                if (!first)
                    Thread.Sleep(sleepTime);

                first = false;
                result = m_twitch.Connect(channel, m_options.User, m_options.Pass);

                if (result == ConnectResult.LoginFailed)
                {
                    WriteStatus("Failed to login, please change options.ini and restart the application.");
                    return false;
                }
                else if (result != ConnectResult.Success)
                {
                    WriteStatus("Failed to connect: {0}", result == ConnectResult.NetworkFailed ? "network failed" : "failed");
                }
            } while (result != ConnectResult.Success);

            WriteStatus("Connected to channel {0}.", channel);
            return true;
        }

        private void StatusUpdate(TwitchClient sender, string message)
        {
            WriteStatus(message);
        }

        #region Event Handlers
        private void SubscribeHandler(TwitchClient sender, TwitchUser user)
        {
            if (PlaySounds)
                m_subSound.Play();

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<TwitchUser>(DispatcherUserSubscribed), user);
        }

        private void ChatActionReceived(TwitchClient sender, TwitchUser user, string text)
        {
            if (m_options.Ignore.Contains(user.Name))
                return;

            user.EnsureIconsDownloaded();
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ChatItem>(AddItem), new ChatAction(this, user, text));
        }


        private void JtvMessageReceived(TwitchClient sender, TwitchUser user, string text)
        {
            if (text.StartsWith("The moderators of this"))
            {
                if (m_modListRequested)
                    m_modListRequested = false;
                else
                    return;
            }

            WriteStatus(text);
        }

        private void ChatMessageReceived(TwitchClient sender, TwitchUser user, string text)
        {
            if (m_options.Ignore.Contains(user.Name))
                return;

            user.EnsureIconsDownloaded();
            bool question = false;
            if (HighlightQuestions)
            {
                foreach (var highlight in m_options.Highlights)
                {
                    if (text.ToLower().Contains(highlight))
                    {
                        question = true;
                        break;
                    }
                }
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ChatItem>(AddItem), new ChatMessage(this, user, text, question));
        }


        void Instance_ChannelDataReceived(string arg1, List<TwitchChannelResponse> arg2)
        {
            string text = "OFFLINE";
            if (arg2.Count == 1)
                text = arg2[0].channel_count.ToString() + " viewers";

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<string>(SetViewers), text);
        }

        private void SetViewers(string value)
        {
            Viewers.Content = value;
        }


        private void ClearChatHandler(TwitchClient sender, TwitchUser user)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<TwitchUser>(DispatcherClearChat), user);
        }

        void WriteStatus(string msg, params string[] args)
        {
            if (args.Length > 0)
                msg = string.Format(msg, args);

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action<ChatItem>(AddItem), new StatusMessage(this, msg));
        }


        private void DispatcherClearChat(TwitchUser user)
        {
            foreach (var msg in Messages)
            {
                if (msg.User != user)
                    continue;

                msg.ClearChat();
            }
        }


        private void DispatcherUserSubscribed(TwitchUser user)
        {
            AddItem(new Subscriber(this, user));
        }

        void AddItem(ChatItem item)
        {
            bool gotoEnd = ScrollBar.VerticalOffset == ScrollBar.ScrollableHeight;

            if (gotoEnd)
                while (Messages.Count >= 250)
                    Messages.RemoveAt(0);

            Messages.Add(item);

            if (gotoEnd)
                ScrollBar.ScrollToEnd();
        }
        #endregion

        #region UI Properties
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        public ObservableCollection<ChatItem> Messages { get; set; }

        public TwitchUser CurrentUser { get; private set; }


        public Visibility ModButtonVisibility
        {
            get
            {
                return m_modButtonVisible;
            }
            set
            {
                if (m_modButtonVisible != value)
                {
                    m_modButtonVisible = value;
                    OnPropertyChanged("ModButtonVisibility");
                }
            }
        }

        private void ModStatusChanged(TwitchUser user, bool modStatus)
        {
            ModButtonVisibility = modStatus ? Visibility.Visible : Visibility.Collapsed;
        }
        
        public bool PlaySounds
        {
            get
            {
                return m_playSounds;
            }
            set
            {
                if (m_playSounds != value)
                {
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("PlaySounds", value));
                    m_playSounds = value;
                    OnPropertyChanged("PlaySounds");
                }
            }
        }

        public bool ConfirmTimeouts
        {
            get
            {
                return m_confirmTimeouts;
            }
            set
            {
                if (m_confirmTimeouts != value)
                {
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("ConfirmTimeouts", value));
                    m_confirmTimeouts = value;
                    OnPropertyChanged("ConfirmTimeouts");
                }
            }
        }

        public bool ConfirmBans
        {
            get
            {
                return m_confirmBans;
            }
            set
            {
                if (m_confirmBans != value)
                {
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("ConfirmBans", value));
                    m_confirmBans = value;
                    OnPropertyChanged("ConfirmBans");
                }
            }
        }


        public bool ShowIcons
        {
            get
            {
                return m_showIcons;
            }
            set
            {
                if (m_showIcons != value)
                {
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("ShowIcons", value));
                    m_showIcons = value;
                    OnPropertyChanged("ShowIcons");
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
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("OnTop", value));
                    m_onTop = value;
                    OnPropertyChanged("OnTop");

                    if (value)
                    {
                        this.Topmost = true;
                        this.Deactivated += MainWindow_Deactivated;
                    }
                    else
                    {
                        this.Topmost = false;
                        this.Deactivated -= MainWindow_Deactivated;
                    }
                }
            }
        }

        public int DynamicFontSize
        {
            get
            {
                return m_fontSize;
            }
            set
            {
                if (value < 8 || value > 24)
                    return;

                if (m_fontSize != value)
                {
                    m_fontSize = value;

                    OnPropertyChanged("DynamicFontSize");
                }
            }
        }

        public bool ShowTimestamps
        {
            get
            {
                return m_showTimestamp;
            }
            set
            {
                if (m_showTimestamp != value)
                {
                    ThreadPool.QueueUserWorkItem((o) => m_options.SetOption("ShowTimestamps", value));
                    m_showTimestamp = value;
                    OnPropertyChanged("ShowTimestamps");
                }
            }
        }

        void MainWindow_Deactivated(object sender, EventArgs e)
        {
            this.Topmost = true;
        }

        public bool HighlightQuestions
        {
            get
            {
                return m_highlightQuestions;
            }
            set
            {
                if (m_highlightQuestions != value)
                {
                    m_options.SetOption("HighlightQuestions", value);
                    m_highlightQuestions = value;
                    OnPropertyChanged("HighlightQuestions");
                }
            }
        }
        #endregion

        #region Event Handlers
        private void Window_Closed(object sender, EventArgs e)
        {
            m_twitch.Quit();
            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        private void OnReconnect(object sender, RoutedEventArgs e)
        {
            m_reconnect = true;
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            Messages.Clear();
        }

        private void OnCommercial(object sender, RoutedEventArgs e)
        {
            int time;
            if (TryParseMenuTime(sender as MenuItem, out time))
                m_twitch.SendMessage(Importance.High, string.Format(".commercial {0}", time));
        }

        bool TryParseMenuTime(MenuItem item, out int time)
        {
            time = 0;

            Debug.Assert(item != null);
            if (item == null)
                return false;

            string[] header = item.Header.ToString().Split(' ');
            Debug.Assert(header.Length >= 2);

            string text = header[header.Length - 1];
            Debug.Assert(int.TryParse(text, out time));

            return int.TryParse(text, out time);
        }


        void OnClearChat(object sender, RoutedEventArgs e)
        {
            m_twitch.SendMessage(Importance.High, ".clear");
        }

        void OnSlowMode(object sender, RoutedEventArgs e)
        {
            int time;
            if (TryParseMenuTime(sender as MenuItem, out time))
                m_twitch.SendMessage(Importance.High, string.Format(".slow {0}", time));
        }

        void OnSlowModeOff(object sender, RoutedEventArgs e)
        {
            m_twitch.SendMessage(Importance.High, ".slowoff");
        }
        void OnSubMode(object sender, RoutedEventArgs e)
        {
            m_twitch.SendMessage(Importance.High, ".subscribers");
        }

        void OnSubModeOff(object sender, RoutedEventArgs e)
        {
            m_twitch.SendMessage(Importance.High, ".subscribersoff");
        }

        void OnFontSize(object sender, RoutedEventArgs e)
        {

            var menuItem = (MenuItem)sender;

            ContextMenu menu = (ContextMenu)menuItem.Parent;
            for (int i = 0; i < 4; i++)
                ((MenuItem)menu.Items[i]).IsChecked = false;

            switch (menuItem.Header.ToString())
            {
                case "Small Font":
                    DynamicFontSize = 12;
                    ((MenuItem)menu.Items[0]).IsChecked = true;
                    break;


                case "Medium Font":
                    DynamicFontSize = 14;
                    ((MenuItem)menu.Items[1]).IsChecked = true;
                    break;


                case "Large Font":
                    DynamicFontSize = 16;
                    ((MenuItem)menu.Items[2]).IsChecked = true;
                    break;


                case "Huge Font":
                    DynamicFontSize = 18;
                    ((MenuItem)menu.Items[3]).IsChecked = true;
                    break;

                default:
                    Debug.Assert(false);
                    break;
            }
        }


        private void Channel_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateChannel(Channel.Text);
        }

        private void Channel_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                UpdateChannel(Channel.Text);
                ChatInput.Focus();
            }
        }

        private void UpdateChannel(string channel)
        {
            channel = channel.ToLower();
            if (m_channel != channel)
            {
                TwitchHttp.Instance.StopPolling();
                m_channel = channel;
                TwitchHttp.Instance.PollChannelData(m_channel);
                OnReconnect(null, null);
            }
        }
        private void AllItems_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollBar.ScrollToVerticalOffset(ScrollBar.VerticalOffset - e.Delta);
            e.Handled = true;
        }
        private void Chat_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                SendTextInput();
            }
        }
        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            SendTextInput();
        }



        private void SendTextInput()
        {
            string text = ChatInput.Text;
            ChatInput.Text = "";

            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            else if (text.StartsWith("/"))
            {
                HandleCommand(text);
                return;
            }
            else if (text.StartsWith("."))
            {
                text = " " + text;
            }

            text = text.Replace('\n', ' ');

            m_twitch.SendMessage(Importance.High, text);

            var user = m_twitch.ChannelData.GetUser(m_options.User);
            AddItem(new ChatMessage(this, ItemType.Message, user, text));
        }

        private void HandleCommand(string text)
        {
            text = "." + text.Substring(1);

            if (text.ToLower().StartsWith(".mods"))
                m_modListRequested = true;

            m_twitch.SendMessage(Importance.High, text);
        }

        private void ScrollBar_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (ScrollBar.VerticalOffset == ScrollBar.ScrollableHeight)
                ScrollBar.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            else
                ScrollBar.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
        #endregion

        private void Button_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            Button button = (Button)sender;

            var menu = button.ContextMenu;
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }

        List<UIElement> m_streamerItems;
        bool m_streamerMenu = true;

        private void ModButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            Button button = (Button)sender;
            ContextMenu menu = button.ContextMenu;

            if (m_streamerItems == null)
            {
                m_streamerItems = new List<UIElement>(menu.Items.Count);
                foreach (UIElement item in menu.Items)
                    m_streamerItems.Add(item);
            }

            if (CurrentUser != null && (CurrentUser.IsStreamer != m_streamerMenu))
            {
                if (CurrentUser.IsStreamer)
                {
                    menu.Items.Clear();
                    foreach (var item in m_streamerItems)
                        menu.Items.Add(item);

                    m_streamerMenu = true;
                }
                else
                {
                    menu.Items.Clear();
                    int i;
                    for (i = 0; i < m_streamerItems.Count; ++i)
                        if (m_streamerItems[i] is Separator)
                            break;

                    for (i = i + 1; i < m_streamerItems.Count; ++i)
                        menu.Items.Add(m_streamerItems[i]);

                    m_streamerMenu = false;
                }
            }

            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            menu.IsOpen = true;
        }


        private void Button_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }

    static class Extensions
    {
        public static void BeginInvokeAction(this Dispatcher dispatcher,
                                     Action action)
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
