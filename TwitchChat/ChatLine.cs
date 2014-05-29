using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TwitchChat.Properties;
using Winter;

namespace TwitchChat
{

    public class ChatLine : TextBlock
    {
        static BitmapImage s_sub, s_timeout, s_ban, s_eight, s_logs, s_check, s_mod;
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(
                "Value",
                typeof(ChatItem),
                typeof(ChatLine),
                new PropertyMetadata(default(ChatItem), OnItemsPropertyChanged));

        static Dictionary<TwitchEmoticon, BitmapImage> s_emotes = new Dictionary<TwitchEmoticon, BitmapImage>();

        Run m_name;
        List<Run> m_messages = new List<Run>();
        InlineUIContainer m_mod;
        TimeOutIcon m_timeout, m_eight, m_ban;
        Winter.TwitchUser m_user;

        MainWindow Controller { get { return Value != null ? Value.Controller : null; } }

        static ChatLine()
        {
            s_sub = GetBitmapImage(TwitchChat.Properties.Resources.star);
            s_timeout = GetBitmapImage(TwitchChat.Properties.Resources.timeout);
            s_ban = GetBitmapImage(TwitchChat.Properties.Resources.ban);
            s_eight = GetBitmapImage(TwitchChat.Properties.Resources.eight);
            s_logs = GetBitmapImage(TwitchChat.Properties.Resources.logs);
            s_check = GetBitmapImage(TwitchChat.Properties.Resources.check);
            s_mod = GetBitmapImage(TwitchChat.Properties.Resources.mod);
        }

        private static BitmapImage GetBitmapImage(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        private static void OnItemsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
        }

        public ChatItem Value
        {
            get
            {
                return (ChatItem)GetValue(ItemsProperty);
            }
            set
            {
                SetValue(ItemsProperty, value);
            }
        }

        static int s_curr;

        public ChatLine()
        {
            this.Initialized += ChatLine_Initialized;
            ContextMenu = new ContextMenu();
            ContextMenu.Opened += ContextMenu_Opened;
        }

        void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (ContextMenu.Items.Count > 0)
                return;

            if (m_user == null)
            {
                AddMenuItem("Copy", null, copy_line);
                e.Handled = true;
                return;
            }

            AddMenuItem("Copy", null, copy_line);
            ContextMenu.Items.Add(new Separator());
            AddMenuItem("Chat Logs", s_logs, showlogs_Click);
            AddMenuItem("Profile", null, profile_Click);
            ContextMenu.Items.Add(new Separator());
            AddMenuItem("Unban", null, profile_Unban);
            ContextMenu.Items.Add(new Separator());
            AddMenuItem("Purge", null, purge_click);
            AddMenuItem("Timeout", s_timeout, timeout_click);
            AddMenuItem("8 Hour Timeout", s_eight, eight_click);
            AddMenuItem("Ban", s_ban, ban_click);
        }

        private void profile_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(string.Format("www.twitch.tv/{0}/profile", m_user.Name));
        }

        private void profile_Unban(object sender, RoutedEventArgs e)
        {
            Controller.Unban(m_user);
        }

        private void copy_line(object sender, RoutedEventArgs e)
        {
            string text = null;
            if (Value is ChatMessage)
                text = ((ChatMessage)Value).Message;
            else if (Value is StatusMessage)
                text = ((StatusMessage)Value).Message;

            if (text != null)
                Clipboard.SetText(text);
        }

        private void showlogs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(string.Format("www.darkautumn.net/winter/chat.php?CHANNEL={0}&USER={1}", Controller.Channel.Text.ToLower(), m_user.Name));
        }

        private void timeout_click(object sender, RoutedEventArgs e)
        {
            if (Controller.Timeout(m_user, 600) && m_timeout != null)
                m_timeout.ShowAlternate();
        }

        private void eight_click(object sender, RoutedEventArgs e)
        {
            if (Controller.Timeout(m_user, 28800) && m_eight != null)
                m_eight.ShowAlternate();
        }

        private void ban_click(object sender, RoutedEventArgs e)
        {
            if (Controller.Ban(m_user) && m_ban != null)
                m_ban.ShowAlternate();
        }

        private void purge_click(object sender, RoutedEventArgs e)
        {
            Controller.Timeout(m_user, 1);
        }

        private void AddMenuItem(string title, BitmapImage image, RoutedEventHandler handler)
        {
            MenuItem item = new MenuItem();
            item.Header = title;
            item.Click += handler;
            if (image != null)
                item.Icon = GetImage(image);
            ContextMenu.Items.Add(item);
        }

        void ChatLine_Initialized(object sender, EventArgs e)
        {
            if (Controller != null)
                FontSize = Controller.DynamicFontSize;

            var value = Value;
            if (value == null)
                return;

            value.Clear += value_Clear;

            switch (value.Type)
            {
                case ItemType.Action:
                case ItemType.Question:
                case ItemType.Message:
                    SetMessage((ChatMessage)value);
                    break;

                case ItemType.Status:
                    SetStatus((StatusMessage)value);
                    break;

                case ItemType.Subscriber:
                    SetSubscriber((Subscriber)value);
                    break;

                default:
                    Debug.Assert(false);
                    break;
            }

            // ...and the right background color.
            int curr = ++s_curr;
            if ((curr % 2) == 0)
                Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#C8C8C8"));
            else
                Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#E4E4E4"));
        }

        void value_Clear()
        {
            m_name.TextDecorations.Add(System.Windows.TextDecorations.Strikethrough);
            m_name.Foreground = Brushes.Gray;

            foreach (var msg in m_messages)
            {
                msg.TextDecorations.Add(System.Windows.TextDecorations.Strikethrough);
                msg.Foreground = Brushes.Gray;
            }
        }

        private void SetSubscriber(Subscriber subscriber)
        {
            Inlines.Add(new Run(string.Format("{0} just subscribed!", subscriber.User.Name)) { BaselineAlignment = BaselineAlignment.Center, Foreground = Brushes.Red });
        }

        private void SetStatus(StatusMessage statusMessage)
        {
            Inlines.Add(new Run(statusMessage.Message) { BaselineAlignment = BaselineAlignment.Center, Foreground = Brushes.Green });
        }

        private void SetMessage(ChatMessage msg)
        {
            m_user = msg.User;

            m_ban = new TimeOutIcon(GetImage(s_ban), GetImage(s_check));
            m_ban.BaselineAlignment = BaselineAlignment.Center;
            m_ban.Clicked += m_ban_Clicked;
            m_ban.Restore += Unban;

            m_eight = new TimeOutIcon(GetImage(s_eight), GetImage(s_check));
            m_eight.BaselineAlignment = BaselineAlignment.Center;
            m_eight.Clicked += m_eight_Clicked;
            m_eight.Restore += Unban;

            m_timeout = new TimeOutIcon(GetImage(s_timeout), GetImage(s_check));
            m_timeout.Clicked += m_timeout_Clicked;
            m_timeout.BaselineAlignment = BaselineAlignment.Center;
            m_timeout.Restore += Unban;

            if (Controller.ShowTimestamps)
                Inlines.Add(new Run(DateTime.Now.ToString("hh:mm ")) { Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#707070")), BaselineAlignment = BaselineAlignment.Center });

            if (Controller.ShowIcons && Controller.CurrentUser.IsModerator)
            {
                Inlines.Add(m_ban);
                Inlines.Add(m_eight);
                Inlines.Add(m_timeout);
            }

            if (m_user.IsModerator)
            {
                m_mod = new InlineUIContainer(GetImage(s_mod));
                m_mod.BaselineAlignment = BaselineAlignment.Center;
                Inlines.Add(m_mod);
            }

            var user = msg.User;
            if (user.IsSubscriber)
            {
                var sub = new InlineUIContainer(GetImage(s_sub));
                sub.BaselineAlignment = BaselineAlignment.Center;
                Inlines.Add(sub);
            }

            Brush userColor = Brushes.Black;
            if (user.Color != null)
            {
                try
                {
                    userColor = (SolidColorBrush)(new BrushConverter().ConvertFrom(msg.User.Color));
                }
                catch (NotSupportedException)
                {
                }
            }

            m_name = new Run(user.Name) { FontWeight = FontWeights.Bold, Foreground = userColor, BaselineAlignment = BaselineAlignment.Center };
            Inlines.Add(m_name);

            if (msg.Type != ItemType.Action)
                Inlines.Add(new Run(": ") { BaselineAlignment = BaselineAlignment.Center });

            BuildText(msg);
        }

        private void BuildText(ChatMessage msg)
        {
            var text = msg.Message;
            var weight = (msg.Type == ItemType.Question) ? FontWeights.Bold : FontWeights.Normal;
            var color = (msg.Type == ItemType.Question) ? Brushes.Red : Brushes.Black;

            var set = TwitchHttp.Instance.ImageSet;
            if (set == null)   
            {
                var run = new Run(text) { Foreground = color, FontWeight = weight, BaselineAlignment = BaselineAlignment.Center };
                m_messages.Add(run);
                Inlines.Add(run);
                return;
            }

            int curr = 0;
            var emoticons = from item in set.Find(text, m_user.IconSet)
                            orderby item.Item2, item.Item3 descending, item.Item1.Default
                            select item;

            foreach (var item in emoticons)
            {
                var emote = item.Item1;
                var start = item.Item2;
                var len = item.Item3;

                if (start < curr)
                    continue;

                Image img = GetImage(emote);
                if (img != null)
                {
                    if (curr < start)
                    {
                        var run = new Run(text.Slice(curr, start)) { Foreground = color, FontWeight = weight, BaselineAlignment = BaselineAlignment.Center };
                        m_messages.Add(run);
                        Inlines.Add(run);
                    }

                    InlineUIContainer cont = new InlineUIContainer(img);
                    cont.ToolTip = emote.Name;
                    cont.BaselineAlignment = BaselineAlignment.Center;
                    Inlines.Add(cont);
                }
                else
                {
                    var run = new Run(text.Slice(curr, start + len)) { Foreground = color, FontWeight = weight, BaselineAlignment = BaselineAlignment.Center };
                    m_messages.Add(run);
                    Inlines.Add(run);
                }

                curr = start + len;
            }

            if (curr < text.Length)
            {
                var run = new Run(text.Substring(curr)) { Foreground = color, FontWeight = weight, BaselineAlignment = BaselineAlignment.Center };
                m_messages.Add(run);
                Inlines.Add(run);
            }
        }

        private static Image GetImage(TwitchEmoticon emote)
        {
            BitmapImage src;
            if (!s_emotes.TryGetValue(emote, out src))
            {
                if (string.IsNullOrWhiteSpace(emote.LocalFile) || !File.Exists(emote.LocalFile))
                    return null;

                try
                {
                    src = new BitmapImage();
                    src.BeginInit();
                    src.UriSource = new Uri(emote.LocalFile, UriKind.RelativeOrAbsolute);
                    src.EndInit();
                }
                catch
                {
                    try
                    {
                        File.Delete(emote.LocalFile);
                    }
                    catch
                    {
                    }

                    return null;
                }
            }

            Image img = new Image();
            img.Source = src;
            if (emote.Height != null)
                img.Height = (int)emote.Height;

            if (emote.Width != null)
                img.Width = (int)emote.Width;

            img.Stretch = Stretch.Uniform;
            return img;
        }

        void m_timeout_Clicked(TimeOutIcon obj)
        {
            if (m_user != null)
                if (Controller.Timeout(m_user, 600))
                    m_timeout.ShowAlternate();
        }

        void m_eight_Clicked(TimeOutIcon obj)
        {
            if (m_user != null)
                if (Controller.Timeout(m_user, 28800))
                    m_eight.ShowAlternate();
        }

        void Unban(TimeOutIcon icon)
        {
            Controller.Unban(m_user);
            icon.ShowIcon();
        }

        void m_ban_Clicked(TimeOutIcon icon)
        {
            if (m_user != null)
                if (Controller.Ban(m_user))
                    m_ban.ShowAlternate();
        }



        private static Image GetImage(BitmapImage bitmap)
        {
            Image img = new Image();
            img.Source = bitmap;
            img.Width = 18;
            img.Height = 18;
            img.VerticalAlignment = VerticalAlignment.Bottom;
            return img;
        }
    }

    class TimeOutIcon : InlineUIContainer
    {
        UIElement m_icon, m_check;

        public event Action<TimeOutIcon> Clicked;
        public event Action<TimeOutIcon> Restore;
        
        public TimeOutIcon(UIElement icon, UIElement check)
            : base(icon)
        {
            m_icon = icon;
            m_check = check;

            this.MouseUp += TimeOutIcon_MouseUp;
        }

        void TimeOutIcon_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Click();
        }

        public void Click()
        {
            if (Child == m_icon)
            {
                var evt = Clicked;
                if (evt != null)
                    evt(this);
            }
            else
            {
                var evt = Restore;
                if (evt != null)
                    evt(this);
            }
        }

        public void ShowAlternate()
        {
            Child = m_check;
        }
        public void ShowIcon()
        {
            Child = m_icon;
        }
    }
}
