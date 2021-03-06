﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Catan.Proxy;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

using StaticHelpers;

using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace CatanLogSpy
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    ///
    public sealed partial class MainPage : Page
    {
        #region Delegates + Fields + Events + Enums

        public static readonly DependencyProperty LogHeaderJsonProperty = DependencyProperty.Register("LogHeaderJson", typeof(string), typeof(MainPage), new PropertyMetadata(""));
        public static readonly DependencyProperty SelectedGameProperty = DependencyProperty.Register("SelectedGame", typeof(GameInfo), typeof(MainPage), new PropertyMetadata(null, SelectedGameChanged));
        public static readonly DependencyProperty SelectedMessageProperty = DependencyProperty.Register("SelectedMessage", typeof(CatanMessage), typeof(MainPage), new PropertyMetadata(null, SelectedMessageChanged));
        private ObservableCollection<CatanMessage> Messages = new ObservableCollection<CatanMessage>();
        public static MainPage Current { get; private set; }
        #endregion Delegates + Fields + Events + Enums

        #region Properties

        public string LogHeaderJson
        {
            get => (string)GetValue(LogHeaderJsonProperty);
            set => SetValue(LogHeaderJsonProperty, value);
        }

        public GameInfo SelectedGame
        {
            get => (GameInfo)GetValue(SelectedGameProperty);
            set => SetValue(SelectedGameProperty, value);
        }

        public CatanMessage SelectedMessage
        {
            get => (CatanMessage)GetValue(SelectedMessageProperty);
            set => SetValue(SelectedMessageProperty, value);
        }

        private ObservableCollection<GameInfo> Games { get; } = new ObservableCollection<GameInfo>();
        private HubConnection HubConnection { get; set; }

        #endregion Properties

        #region Constructors + Destructors

        public MainPage ()
        {
            this.InitializeComponent();
            Current = this;
            _ = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await ConnectToService();

                await RefreshGames();
            });
        }

        #endregion Constructors + Destructors

        #region Methods

        public static JsonSerializerOptions GetJsonOptions (bool indented = false)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = indented
            };
            options.Converters.Add(new JsonStringEnumConverter());
            // options.Converters.Add(new PlayerModelConverter());
            return options;
        }

        public async Task ConnectToService ()
        {
            string host = "catanhub.azurewebsites.net";
            string ServiceUrl;
            try
            {
                if (host.Contains("192"))
                {
                    ServiceUrl = "http://" + host + "/CatanHub";
                }
                else
                {
                    ServiceUrl = "https://" + host + "/CatanHub";
                }

                HubConnection = new HubConnectionBuilder().WithAutomaticReconnect().WithUrl(ServiceUrl).ConfigureLogging((logging) =>
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Trace);
                }).Build();

                HubConnection.ServerTimeout = TimeSpan.FromMinutes(5);
                HubConnection.HandshakeTimeout = TimeSpan.FromSeconds(10);
                HubConnection.KeepAliveInterval = TimeSpan.FromSeconds(19);

                HubConnection.Reconnecting += async error =>
                {
                    //   this.TraceMessage("Hub reconnecting!!");
                    Debug.Assert(HubConnection.State == HubConnectionState.Reconnecting);
                    if (SelectedGame != null) // this puts us back into the channel with the other players.
                    {
                        await JoinGame(SelectedGame);
                    }
                };
                //HubConnection.Reconnected += (connectionId) =>
                //{
                //    this.TraceMessage($"Reconnected.  new id: {connectionId}.");

                //};

                HubConnection.Closed += async (error) =>
                {
                    this.TraceMessage($"HubConnection closed!  Error={error}");
                    await Task.Delay(new Random().Next(0, 5) * 1000);
                    await HubConnection.StartAsync();
                };

                HubConnection.On("ToAllClients", async (CatanMessage message) =>
                {
                    //  Debug.WriteLine($"message received: {message}");
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });

                HubConnection.On("ToOneClient", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });
                HubConnection.On("OnAck", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });

                HubConnection.On("CreateGame", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });
                HubConnection.On("DeleteGame", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });
                HubConnection.On("JoinGame", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });
                HubConnection.On("LeaveGame", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Insert(0, message);
                    });
                });

                HubConnection.On("AllGames", async (List<GameInfo> games) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Games.Clear();
                        Games.AddRange(games);
                    });
                });

                HubConnection.On("AllPlayers", async (ICollection<string> playerNames) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                    });
                });

                HubConnection.On("AllMessages", async (List<CatanMessage> messages) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Clear();
                        AddMessages(messages);
                    });
                });

                await HubConnection.StartAsync();
            }
            catch
            {
            }
        }

        public async Task ShowErrorMessage (string message, string caption, string extended)
        {
            ContentDialog dlg = new ContentDialog()
            {
                Title = caption,
                Content = message,
                CloseButtonText = "Close",
            };
            await dlg.ShowAsync();
        }

        private static void SelectedGameChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as MainPage;
            var depPropValue = (GameInfo)e.NewValue;
            depPropClass?.SetSelectedGame(depPropValue);
        }

        private static void SelectedMessageChanged (DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as MainPage;
            var depPropValue = (CatanMessage)e.NewValue;
            depPropClass?.SetSelectedMessage(depPropValue);
        }

        private void AddMessages (List<CatanMessage> messages)
        {
            foreach (var message in messages)
            {
                Messages.Insert(0, message);
            }
        }
        private async Task EnsureConnection ()
        {
            if (HubConnection.State == HubConnectionState.Connected) return;

            TaskCompletionSource<object> connectionTCS = new TaskCompletionSource<object>(); ;
            HubConnection.Reconnected += Reconnected;
            Task Reconnected (string arg)
            {
                connectionTCS.TrySetResult(null);
                HubConnection.Reconnected -= Reconnected;
                return Task.CompletedTask;
            }

            int n = 0;
            //
            //  make sure we are connected to the service
            while (HubConnection.State != HubConnectionState.Connected)
            {
                n++;
                await MainPage.Current.ShowErrorMessage("Lost Connection to the Catan Service.  Click Ok and I'll try to connect.", "Catan", "");
                await connectionTCS.Task.TimeoutAfter(5000);
                connectionTCS = new TaskCompletionSource<object>();
            }
        }
        private string Format (string dataType, string json)
        {
            if (dataType.Contains("RandomBoardLog"))
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < json.Length; i++)
                {
                    sb.Append(json[i]);
                    if (json[i] == '[')
                    {
                        while (json[i] != ']')
                        {
                            i++;
                            if (json[i] == '\n' || json[i] == '\r' || json[i] == ' ' || json[i] == '\t') continue;
                            sb.Append(json[i]);
                        }
                    }
                }

                return sb.ToString();
            }

            return json;
        }

        private string FormatJson (string json)
        {
            StringBuilder sb = new StringBuilder();

            int tabs = 0;
            bool inArray = false;
            foreach (char c in json)
            {
                if (c == '{' || c == '[')
                {
                    inArray = (c == '[');
                    string soFar = sb.ToString();
                    if (soFar.Length > 0)
                    {
                        char lastChar  = soFar[soFar.Length - 1];

                        if (lastChar == '[' || lastChar == ']' || lastChar == ':')
                        {
                            sb.Append(Environment.NewLine);
                            sb.Append(new string('\t', tabs));
                        }
                    }
                    sb.Append(c);
                    sb.Append(Environment.NewLine);
                    tabs++;
                    sb.Append(new string('\t', tabs));
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    if (c == ']') inArray = false;
                    sb.Append(Environment.NewLine);
                    tabs--;
                    sb.Append(new string('\t', tabs));
                    sb.Append(c);
                    continue;
                }

                sb.Append(c);

                if (c == ',')
                {
                    if (!inArray)
                    {
                        sb.Append(Environment.NewLine);
                        sb.Append(new string('\t', tabs));
                    }
                }
            }

            return sb.ToString();
        }

        private async Task JoinGame (GameInfo gameInfo)
        {
            CatanMessage message = new CatanMessage()
            {
                MessageType = MessageType.JoinGame,
                ActionType = ActionType.Normal,
                Data = null,
                DataTypeName="",
                From = "Catan Spy",
                To="*",
                GameInfo = gameInfo,
            };
            await PostHubMessage(message);
        }

        private void OnGameDeleted (Guid id, string by)
        {
            for (int i = Games.Count() - 1; i >= 0; i--)
            {
                if (Games[i].Id == id)
                {
                    Games.RemoveAt(i);
                    return;
                }
            };
        }

        private async void OnRefresh (object sender, RoutedEventArgs e)
        {
            await RefreshGames();
        }

        private void OnSelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (e == null) return;
            if (e.AddedItems.Count == 0) return;
            if (((CatanMessage)e.AddedItems[0]) == null)
            {
                return;
            }
            if (((CatanMessage)e.AddedItems[0]).Data == null)
            {
                LogHeaderJson = JsonSerializer.Serialize(((CatanMessage)e.AddedItems[0]), GetJsonOptions(true));
                return;
            }
            if (((CatanMessage)e.AddedItems[0]).Data.ToString() == "null")
            {
                LogHeaderJson = JsonSerializer.Serialize(((CatanMessage)e.AddedItems[0]), GetJsonOptions(true));
                return;
            }
            SelectedMessage = e.AddedItems[0] as CatanMessage;
            string json = ((CatanMessage)e.AddedItems[0]).Data.ToString();
            LogHeaderJson = FormatJson(json);
        }

        private async Task PostHubMessage (CatanMessage message)
        {
            await EnsureConnection();
            //
            //  make it an object so we can get the whole message
            message.Data = JsonSerializer.Serialize<object>(message.Data, GetJsonOptions());

            await HubConnection.SendAsync("PostMessage", message);
        }
        private async Task RefreshGames ()
        {
            await HubConnection.InvokeAsync("GetAllGames");
        }

        private async void SetSelectedGame (GameInfo game)
        {
            this.TraceMessage($"Selected Game: {game}");
            await HubConnection.InvokeAsync("GetAllMessage", game);
            await JoinGame(game);
        }
        private void SetSelectedMessage (CatanMessage message)
        {
            //LogHeader logHeader = message.Data as LogHeader;
            //string json = CatanSignalRClient.Serialize<LogHeader>(logHeader, true);
        }

        #endregion Methods

        private async void OnOpenLogFile (object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".json");

            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file == null)
            {
                return;
            }

            string json =await FileIO.ReadTextAsync(file);
            json = json.Replace("\r\n", String.Empty);
            json = json.Replace("    ", String.Empty);
            List<CatanMessage> list  = JsonSerializer.Deserialize<List<CatanMessage>>(json, GetJsonOptions());
            Messages.Clear();
            foreach (var m in list)
            {
                Messages.Add(m);
            }
        }

        private async void OnResend(object sender, RoutedEventArgs e)
        {
            if (SelectedMessage == null) return;
            await HubConnection.SendAsync("PostMessage", SelectedMessage);
        }
    }
}