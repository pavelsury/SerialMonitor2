﻿using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SerialMonitor.Business.Data;
using SerialMonitor.Business.Enums;
using SerialMonitor.Business.Helpers;

namespace SerialMonitor.Business
{
    public class SettingsManager : NotifyPropertyChanged, IEndiannessProvider
    {
        public bool IsLittleEndian
        {
            get
            {
                switch (SelectedPort?.Settings.Endianness)
                {
                    case EPortEndianness.Default: return IsDefaultLittleEndian;
                    case EPortEndianness.Little: return true;
                    case EPortEndianness.Big: return false;
                    case null: return true;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        public PortInfo SelectedPort
        {
            get => _selectedPort;
            set
            {
                AppSettings.SelectedPort = value?.Name;
                SetNotifyingProperty(ref _selectedPort, value);
            }
        }

        public bool AutoswitchEnabled
        {
            get => _autoswitchEnabled;
            set
            {
                AppSettings.AutoswitchEnabled = value;
                SetNotifyingValueProperty(ref _autoswitchEnabled, value);
            }
        }

        public EViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                AppSettings.ViewMode = value;
                SetNotifyingValueProperty(ref _viewMode, value);
            }
        }

        public bool WriteMessageToConsole
        {
            get => _writeMessageToConsole;
            set
            {
                AppSettings.WriteMessageToConsole = value;
                SetNotifyingValueProperty(ref _writeMessageToConsole, value);
            }
        }

        public bool WriteCommandToConsole
        {
            get => _writeCommandToConsole;
            set
            {
                AppSettings.WriteCommandToConsole = value;
                SetNotifyingValueProperty(ref _writeCommandToConsole, value);
            }
        }

        public bool WriteResolvedCommandToConsole
        {
            get => _writeResolvedCommandToConsole;
            set
            {
                AppSettings.WriteResolvedCommandToConsole = value;
                SetNotifyingValueProperty(ref _writeResolvedCommandToConsole, value);
            }
        }

        public bool WriteSentBytesToConsole
        {
            get => _writeSentBytesToConsole;
            set
            {
                AppSettings.WriteSentBytesToConsole = value;
                SetNotifyingValueProperty(ref _writeSentBytesToConsole, value);
            }
        }

        public bool HexPrefixEnabled
        {
            get => _hexPrefixEnabled;
            set
            {
                AppSettings.HexPrefixEnabled = value;
                SetNotifyingValueProperty(ref _hexPrefixEnabled, value);
            }
        }

        public string HexSeparator
        {
            get => _hexSeparator;
            set
            {
                AppSettings.HexSeparator = value;
                SetNotifyingProperty(ref _hexSeparator, value);
            }
        }

        public bool PipeEnabled
        {
            get => _pipeEnabled;
            set
            {
                AppSettings.PipeEnabled = value;
                SetNotifyingValueProperty(ref _pipeEnabled, value);
            }
        }

        public bool ShowDotForNonPrintableAscii
        {
            get => _showDotForNonPrintableAscii;
            set
            {
                AppSettings.ShowDotForNonPrintableAscii = value;
                SetNotifyingValueProperty(ref _showDotForNonPrintableAscii, value);
            }
        }

        public int HexFixedColumns
        {
            get => _hexFixedColumns;
            set
            {
                AppSettings.HexFixedColumns = value;
                SetNotifyingValueProperty(ref _hexFixedColumns, value);
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                AppSettings.FontSize = value;
                SetNotifyingValueProperty(ref _fontSize, value);
            }
        }

        public EDefaultEndianness DefaultEndianness
        {
            get => _defaultEndianness;
            set
            {
                AppSettings.DefaultEndianness = value;
                SetNotifyingValueProperty(ref _defaultEndianness, value);
            }
        }

        public bool ShowButtonsTab
        {
            get => _showButtonsTab;
            set
            {
                AppSettings.ShowButtonsTab = value;
                SetNotifyingValueProperty(ref _showButtonsTab, value);
            }
        }

        public bool ShowCommandsTab
        {
            get => _showCommandsTab;
            set
            {
                AppSettings.ShowCommandsTab = value;
                SetNotifyingValueProperty(ref _showCommandsTab, value);
            }
        }

        public ObservableCollection<CustomButton> CustomButtons { get; private set; }

        public ObservableCollection<CustomCommandVariable> CustomCommandVariables { get; private set; }

        public ObservableCollection<string> CommandHistory { get; private set; }

        public AppSettings AppSettings { get; private set; } = new AppSettings();

        public PortSettings GetSettings(string portName) => AppSettings.PortsSettingsMap.GetOrCreate(portName);

        public void AddSentCommand(string command)
        {
            var index = CommandHistory.IndexOf(command);
            if (index == 0)
            {
                return;
            }
            
            if (index > 0)
            {
                CommandHistory.Move(index, 0);
                return;
            }

            if (CommandHistory.Count == MaxCommandHistoryCount)
            {
                CommandHistory.RemoveAt(MaxCommandHistoryCount - 1);
            }

            CommandHistory.Insert(0, command);
        }
        public void ClearCommandHistory() => CommandHistory.Clear();

        public void ResetSelectedPortSettings()
        {
            var portSettings = new PortSettings();
            AppSettings.PortsSettingsMap[SelectedPort.Name] = portSettings;
            SelectedPort.Settings = portSettings;
        }

        public void Save()
        {
            AppSettings.CustomButtons = CustomButtons.Select(b => new CustomButtonSetting
            {
                Label = b.Label,
                Command = b.Command
            }).ToList();

            AppSettings.CustomCommandVariables = CustomCommandVariables.Select(c => new CustomCommandVariable
            {
                CommandVariable = c.CommandVariable,
                Content = c.Content
            }).ToList();

            AppSettings.CommandHistory = CommandHistory.ToList();

            FileHelper.WriteAllTextNoShare(_settingsFilename, JsonSerializer.Serialize(AppSettings, _jsonSerializerOptions));
        }

        public Task LoadAsync(string settingsFilename, string selectedPort)
        {
            return Task.Run(() =>
            {
                try
                {
                    _settingsFilename = settingsFilename;
                    if (_settingsFilename == null)
                    {
                        var settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SerialMonitor2");
                        _settingsFilename = Path.Combine(settingsFolder, "Settings.json");
                        Directory.CreateDirectory(settingsFolder);
                    }

                    if (!File.Exists(_settingsFilename))
                    {
                        return;
                    }

                    var appSettings = JsonSerializer.Deserialize<AppSettings>(FileHelper.ReadAllText(_settingsFilename), _jsonSerializerOptions);
                    if (appSettings != null)
                    {
                        AppSettings = appSettings;
                    }
                }
                catch (JsonException)
                { }
                finally
                {
                    if (!string.IsNullOrEmpty(selectedPort))
                    {
                        AppSettings.SelectedPort = selectedPort;
                    }
                    OnSettingsLoaded();
                }
            });
        }

        public bool IsDefaultLittleEndian
        {
            get
            {
                switch (DefaultEndianness)
                {
                    case EDefaultEndianness.System: return BitConverter.IsLittleEndian;
                    case EDefaultEndianness.Little: return true;
                    case EDefaultEndianness.Big: return false;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        protected virtual void OnSettingsLoaded()
        {
            AppSettings.Validate();
            AutoswitchEnabled = AppSettings.AutoswitchEnabled;
            ViewMode = AppSettings.ViewMode;
            WriteMessageToConsole = AppSettings.WriteMessageToConsole;
            WriteCommandToConsole = AppSettings.WriteCommandToConsole;
            WriteResolvedCommandToConsole = AppSettings.WriteResolvedCommandToConsole;
            WriteSentBytesToConsole = AppSettings.WriteSentBytesToConsole;
            HexPrefixEnabled = AppSettings.HexPrefixEnabled;
            HexSeparator = AppSettings.HexSeparator;
            HexFixedColumns = AppSettings.HexFixedColumns;
            PipeEnabled = AppSettings.PipeEnabled;
            ShowDotForNonPrintableAscii = AppSettings.ShowDotForNonPrintableAscii;
            FontSize = AppSettings.FontSize;
            DefaultEndianness = AppSettings.DefaultEndianness;
            ShowButtonsTab = AppSettings.ShowButtonsTab;
            ShowCommandsTab = AppSettings.ShowCommandsTab;

            CustomButtons = new ObservableCollection<CustomButton>(AppSettings.CustomButtons.Select(b => new CustomButton
            {
                Label = b.Label,
                Command = b.Command
            }));

            CustomCommandVariables = new ObservableCollection<CustomCommandVariable>(AppSettings.CustomCommandVariables.Select(c => new CustomCommandVariable
            {
                CommandVariable = c.CommandVariable,
                Content = c.Content
            }));

            CommandHistory = new ObservableCollection<string>(AppSettings.CommandHistory.Distinct().Take(MaxCommandHistoryCount));
        }

        private const int MaxCommandHistoryCount = 20;
        private string _settingsFilename;
        private PortInfo _selectedPort;
        private bool _autoswitchEnabled;
        private EViewMode _viewMode = EViewMode.Text;
        private bool _writeMessageToConsole;
        private bool _writeCommandToConsole;
        private bool _writeResolvedCommandToConsole;
        private bool _writeSentBytesToConsole;
        private bool _hexPrefixEnabled;
        private string _hexSeparator;
        private int _hexFixedColumns;
        private bool _pipeEnabled;
        private bool _showDotForNonPrintableAscii;
        private int _fontSize;
        private EDefaultEndianness _defaultEndianness;
        private bool _showButtonsTab;
        private bool _showCommandsTab;
        private JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new EncodingJsonConverter() }
        };
    }
}
