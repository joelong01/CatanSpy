﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Catan.Proxy;

using CatanLogSpy.Models;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

using StaticHelpers;

using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
        public static MainPage Current { get; private set; }
        public static readonly DependencyProperty LogHeaderJsonProperty = DependencyProperty.Register("LogHeaderJson", typeof(string), typeof(MainPage), new PropertyMetadata(""));
        public static readonly DependencyProperty SelectedGameProperty = DependencyProperty.Register("SelectedGame", typeof(GameInfo), typeof(MainPage), new PropertyMetadata(null, SelectedGameChanged));
        public static readonly DependencyProperty SelectedMessageProperty = DependencyProperty.Register("SelectedMessage", typeof(CatanMessage), typeof(MainPage), new PropertyMetadata(null, SelectedMessageChanged));
        private ObservableCollection<CatanMessage> Messages = new ObservableCollection<CatanMessage>();

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
                        Messages.Add(message);
                    });
                });

                HubConnection.On("ToOneClient", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Add(message);
                    });
                });
                HubConnection.On("OnAck", async (CatanMessage message) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Messages.Add(message);
                    });
                });

                HubConnection.On("CreateGame", async (GameInfo gameInfo, string by) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var message = CreateGameModel.CreateMessage(gameInfo);
                        message.From = by;
                        Messages.Add(message);
                    });
                });
                HubConnection.On("DeleteGame", async (Guid id, string by) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        OnGameDeleted(id, by);
                    });
                });
                HubConnection.On("JoinGame", async (GameInfo gameInfo, string playerName) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                    });
                });
                HubConnection.On("LeaveGame", async (GameInfo gameInfo, string playerName) =>
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
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
                        Messages.AddRange(messages);
                    });
                });

                await HubConnection.StartAsync();
            }
            catch
            {
            }
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

        private void SetSelectedMessage (CatanMessage message)
        {
            //LogHeader logHeader = message.Data as LogHeader;
            //string json = CatanSignalRClient.Serialize<LogHeader>(logHeader, true);
        }

        #endregion Methods
    }
}