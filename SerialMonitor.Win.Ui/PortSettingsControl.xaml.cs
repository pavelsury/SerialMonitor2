﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using SerialMonitor.Business.Enums;
using SerialMonitor.Win.Business;
using SerialMonitor.Win.Ui.Helpers;

namespace SerialMonitor.Win.Ui
{
    public partial class PortSettingsControl : UserControl
    {
        public PortSettingsControl()
        {
            InitializeComponent();
            BaudRateComboBox.Focus();
        }

        public List<ComboPair> Encodings { get; } = Encoding.GetEncodings()
            .Select(e => new ComboPair
            {
                Value = e.GetEncoding(),
                Text = e.CodePage + " " + e.Name + " - " + e.DisplayName
            })
            .ToList();

        private WinSettingsManager WinSettingsManager => (WinSettingsManager)DataContext;

        private void OnOutputFilenameTextBoxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dialog = new OpenFileDialog { Multiselect = false };
            if (dialog.ShowDialog() == true)
            {
                OutputFilenameTextBox.SetCurrentValue(TextBox.TextProperty, dialog.FileName);
            }
        }

        private void OnComboBoxLostFocus(object sender, RoutedEventArgs e)
        {
            UpdateComboBoxTextProperty((ComboBox)sender);
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                switch (Keyboard.FocusedElement)
                {
                    case ComboBox c:
                        UpdateComboBoxTextProperty(c);
                        break;

                    case TextBox t:
                        t.SelectAll();
                        break;
                }
            }
        }

        private void UpdateComboBoxTextProperty(ComboBox comboBox)
        {
            comboBox.SetCurrentValue(ComboBox.TextProperty, null);
            comboBox.GetBindingExpression(ComboBox.TextProperty)?.UpdateTarget();
        }

        private void OnResetButtonClick(object sender, RoutedEventArgs e) => WinSettingsManager.ResetSelectedPortSettings();
    }
}
