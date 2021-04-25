﻿using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SerialMonitor.Business.Enums;
using SerialMonitor.Business.Helpers;

namespace SerialMonitor.Business
{
    public class PortManager : NotifyPropertyChanged, IConnectionStatusProvider, IDisposable
    {
        public PortManager(
            SettingsManager settingsManager,
            ConsoleManager consoleManager,
            IMainThreadRunner mainThreadRunner,
            IUsbNotification usbNotification)
        {
            _mainThreadRunner = mainThreadRunner;
            ConsoleManager = consoleManager;
            _usbNotification = usbNotification;
            SettingsManager = settingsManager;
            
            _serialPort = new SerialPort();
        }

        public void Initialize()
        {
            _dataManager = new DataManager(SettingsManager, ConsoleManager, this, _mainThreadRunner);

            foreach (var portName in SettingsManager.AppSettings.PortsSettingsMap.Keys)
            {
                CreatePortInfo(portName, false);
            }

            var selectedPortName = SettingsManager.AppSettings.SelectedPort;
            if (!string.IsNullOrWhiteSpace(selectedPortName))
            {
                SelectedPort = Ports.SingleOrDefault(p => p.Name == selectedPortName) ?? CreatePortInfo(selectedPortName, false);
            }

            _usbNotification.DeviceChanged += OnUsbDevicesChanged;
            UpdatePorts();
        }

        public SettingsManager SettingsManager { get; }

        public ConsoleManager ConsoleManager { get; }
        
        public ObservableCollection<PortInfo> Ports { get; set; } = new ObservableCollection<PortInfo>();

        public EConnectionStatus ConnectionStatus
        {
            get => _connectionStatus;
            set => SetNotifyingValueProperty(ref _connectionStatus, value, () =>
            {
                OnPropertyChanged(nameof(IsDisconnected));
                OnPropertyChanged(nameof(IsConnectionChanging));
                OnPropertyChanged(nameof(IsConnectingLong));
                OnPropertyChanged(nameof(IsConnected));
            });
        }

        public bool IsDisconnected => _connectionStatus == EConnectionStatus.Disconnected;
        public bool IsConnectionChanging =>
            _connectionStatus == EConnectionStatus.ConnectingShort ||
            _connectionStatus == EConnectionStatus.ConnectingLong ||
            _connectionStatus == EConnectionStatus.DisconnectingGracefully ||
            _connectionStatus == EConnectionStatus.DisconnectingByFailure;
        public bool IsConnectingLong => _connectionStatus == EConnectionStatus.ConnectingLong;
        public bool IsConnected => _connectionStatus == EConnectionStatus.Connected;

        public void Connect()
        {
            if (!IsDisconnected)
            {
                return;
            }

            ConnectionStatus = EConnectionStatus.ConnectingShort;

            _portTask = Task.Run(async () =>
            {
                using (var delayCts = new CancellationTokenSource())
                {
                    var delayTask = Task.Delay(750, delayCts.Token).ContinueWith(t => _mainThreadRunner.Run(() =>
                    {
                        if (ConnectionStatus == EConnectionStatus.ConnectingShort)
                        {
                            ConnectionStatus = EConnectionStatus.ConnectingLong;
                        }
                    }), delayCts.Token);

                    try
                    {
                        _serialPort.PortName = SelectedPort.Name;
                        _serialPort.BaudRate = SelectedPort.Settings.BaudRate;
                        _serialPort.DataBits = SelectedPort.Settings.DataBits;
                        _serialPort.Handshake = SelectedPort.Settings.Handshake;
                        _serialPort.Parity = SelectedPort.Settings.Parity;
                        _serialPort.StopBits = SelectedPort.Settings.StopBits;
                        _serialPort.ReadTimeout = SelectedPort.Settings.ReadTimeoutMs;
                        _serialPort.WriteTimeout = SelectedPort.Settings.WriteTimeoutMs;
                        _serialPort.Open();
                    }
                    catch (Exception e)
                    {
                        _mainThreadRunner.Run(() =>
                        {
                            ConnectionStatus = EConnectionStatus.Disconnected;
                            ConsoleManager.PrintWarningMessage(GetConnectExceptionMessage(e));
                        });
                        return;
                    }
                    finally
                    {
                        try
                        {
                            delayCts.Cancel();
                            await delayTask;
                        }
                        catch (OperationCanceledException)
                        { }
                    }
                }

                _mainThreadRunner.Run(() =>
                {
                    ConnectionStatus = EConnectionStatus.Connected;
                    ConsoleManager.PrintInfoMessage($"{SelectedPort.Name} connected!");
                });

                await ReadAsync();
            });

            string GetConnectExceptionMessage(Exception e) => $"{SelectedPort.Name} connecting failed!{Environment.NewLine}{e.GetType()}: {e.Message}";
        }

        public void Disconnect() => Disconnect(false);

        private void Disconnect(bool isFailure)
        {
            if (!IsConnected)
            {
                return;
            }

            ConnectionStatus = isFailure ? EConnectionStatus.DisconnectingByFailure : EConnectionStatus.DisconnectingGracefully;

            Task.Run(async () =>
            {
                Exception exception = null;

                try
                {
                    _serialPort.Close();
                }
                catch (Exception e)
                {
                    exception = e;
                }

                try
                {
                    await _portTask;
                }
                catch (Exception)
                { }

                _mainThreadRunner.Run(() =>
                {
                    _portTask = Task.CompletedTask;
                    if (!isFailure && exception != null)
                    {
                        ConsoleManager.PrintWarningMessage($"{exception.GetType()}: {exception.Message}");
                    }
                    var text = isFailure ? "disconnected unexpectedly!" : "disconnected!";
                    ConsoleManager.PrintMessage($"{SelectedPort.Name} {text}", isFailure ? EMessageType.Error : EMessageType.Info);
                    ConnectionStatus = EConnectionStatus.Disconnected;
                });
            });
        }

        private async Task ReadAsync()
        {
            _dataManager.Clean();
            var buffer = new byte[10000];

            while (true)
            {
                int bytesCount;
                try
                {
                    bytesCount = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length);
                }
                catch (Exception e)
                {
                    _dataManager.Clean();
                    _mainThreadRunner.Run(() =>
                    {
                        if (ConnectionStatus != EConnectionStatus.DisconnectingGracefully)
                        {
                            ConsoleManager.PrintWarningMessage($"{SelectedPort.Name} reading failed!{Environment.NewLine}{e.GetType()}: {e.Message}");
                        }
                    });
                    return;
                }

                if (bytesCount > 0)
                {
                    _dataManager.ProcessReceivedData(buffer, bytesCount);
                }
            }
        }

        public void SendText(string text)
        {
            string newline;
            switch (SelectedPort.Settings.SendingNewline)
            {
                case ESendingNewline.None:
                    newline = string.Empty;
                    break;
                case ESendingNewline.Crlf:
                    newline = "\r\n";
                    break;
                case ESendingNewline.Lf:
                    newline = "\n";
                    break;
                default: throw new ArgumentOutOfRangeException();
            }

            var data = Encoding.Convert(Encoding.Default, SelectedPort.Settings.Encoding, Encoding.Default.GetBytes($"{text}{newline}"));

            try
            {
                _serialPort.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                ConsoleManager.PrintWarningMessage(e.Message);
            }
        }

        public void Dispose()
        {
            if (_serialPort == null)
            {
                return;
            }

            _usbNotification.DeviceChanged -= OnUsbDevicesChanged;

            try
            {
                _serialPort.Dispose();
            }
            catch (Exception)
            { }
            
            _serialPort = null;
        }

        private PortInfo SelectedPort
        {
            get => SettingsManager.SelectedPort;
            set => SettingsManager.SelectedPort = value;
        }

        private void OnUsbDevicesChanged(object sender, bool e) => UpdatePorts();

        private void UpdatePorts()
        {
            var wasSelectedPortAvailable = SelectedPort?.IsAvailable == true;
            var portNames = SerialPort.GetPortNames();
            
            foreach (var portName in portNames)
            {
                var portInfo = Ports.SingleOrDefault(p => p.Name == portName);
                if (portInfo == null)
                {
                    CreatePortInfo(portName, true);
                }
                else
                {
                    portInfo.IsAvailable = true;
                }
            }

            if (!Ports.Any())
            {
                return;
            }

            foreach (var portInfo in Ports)
            {
                portInfo.IsAvailable = portNames.Any(n => n == portInfo.Name);
            }

            if (SelectedPort == null)
            {
                SelectedPort = Ports.FirstOrDefault(p => p.IsAvailable) ?? Ports.First();
            }

            if (!SelectedPort.IsAvailable && IsConnected)
            {
                Disconnect(true);
                return;
            }

            if (SettingsManager.AppSettings.AutoconnectEnabled && IsDisconnected)
            {
                if (!wasSelectedPortAvailable && SelectedPort.IsAvailable)
                {
                    Connect();
                }
            }
        }

        private PortInfo CreatePortInfo(string portName, bool isAvailable)
        {
            var portInfo = new PortInfo
            {
                Name = portName,
                IsAvailable = isAvailable,
                Settings = SettingsManager.GetSettings(portName)
            };
            Ports.AddSorted(portInfo);
            return portInfo;
        }

        private readonly IMainThreadRunner _mainThreadRunner;
        private readonly IUsbNotification _usbNotification;
        private SerialPort _serialPort;
        private EConnectionStatus _connectionStatus;
        private DataManager _dataManager;
        private Task _portTask = Task.CompletedTask;
    }
}