﻿using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Interop;
using TiqUtils.Wpf.Converters;
using TiqUtils.Wpf.Helpers;
using TiqUtils.Wpf.UIBuilders.Proxy;

namespace TiqUtils.Wpf.UIBuilders
{
    // ReSharper disable once InconsistentNaming
    public class SettingsAutoUI : Window
    {
        private readonly object _settings;
        private Grid _settingsGrid;

        protected internal SettingsAutoUI(object settingsClass)
        {
            _settings = settingsClass;
            InitializeComponent();
            GenerateBindingControllers();
            DataContext = settingsClass;
        }

        private void InitializeComponent()
        {
            InitWindowSettings();

            var baseGrid = new Grid
            {
                Margin = new Thickness(5)
            };
            baseGrid.RowDefinitions.Add(new RowDefinition());
            baseGrid.RowDefinitions.Add(new RowDefinition());

            _settingsGrid = new Grid();
            baseGrid.Children.Add(_settingsGrid);

            var closeButton = new Button
            {
                Content = "Close"
            };
            closeButton.Click += CloseButtonOnClick;
            Grid.SetRow(closeButton, 1);

            baseGrid.Children.Add(closeButton);

            Content = baseGrid;
        }

        private void CloseButtonOnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private Type GetObjectType()
        {
            if (_settings is NotifyPropertyChangedProxy proxy)
            {
                return proxy.GetWrappedType();
            }

            return _settings.GetType();
        }

        private void GenerateBindingControllers()
        {
            var properties = GetObjectType().GetProperties();
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var rowIdx = 0;
            foreach (var prop in properties.Where(x => x.GetCustomAttribute<PropertyMemberAttribute>() != null).OrderBy(x => x.GetCustomAttribute<PropertyOrderAttribute>()?.Order ?? 999))
            {
                grid.RowDefinitions.Add(new RowDefinition());
                var nameAttribute = prop.GetCustomAttribute<DisplayNameAttribute>();
                var name = nameAttribute?.DisplayName ?? GetBeautyPropName(prop);
                var text = new TextBlock { Text = name, Margin = new Thickness(5) };
                grid.Children.Add(text);
                Grid.SetColumn(text, 0);
                Grid.SetRow(text, rowIdx);

                var e = CreatePropertyUiElement(text, prop);

                Grid.SetColumn(e, 1);
                Grid.SetRow(e, rowIdx);
                grid.Children.Add(e);
                rowIdx++;
            }

            _settingsGrid.Children.Add(grid);
        }

        protected virtual UIElement CreatePropertyUiElement(TextBlock labelTextBlock, PropertyInfo prop)
        {
            UIElement e;

            switch (prop.PropertyType)
            {
                case Type _ when prop.PropertyType == typeof(bool):
                    e = CreateBooleanController(prop);
                    break;
                case Type _ when prop.PropertyType.IsEnum:
                    e = CreateEnumController(prop);
                    break;
                case Type _ when prop.PropertyType == typeof(double):
                case Type _ when prop.PropertyType == typeof(float):
                    e = CreateDoubleController(prop);
                    break;
                case Type _ when prop.PropertyType == typeof(int):
                    e = CreateIntController(prop);
                    break;
                case Type _ when prop.PropertyType == typeof(string):
                    e = CreateTextBlockController(prop);
                    break;
                case Type _ when prop.PropertyType.IsClass:
                    e = CreateSettingsClassController(GetPropertyValue(prop));
                    break;
                default:
                    throw new NotImplementedException();
            }

            return e;
        }

        protected virtual TextBox CreateTextBlockController(PropertyInfo prop)
        {
            var ret = new TextBox
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            var valueBinding = new Binding
            {
                Path = new PropertyPath(prop.Name, Array.Empty<object>())
            };
            ret.SetBinding(TextBox.TextProperty, valueBinding);
            return ret;
        }

        private object GetPropertyValue(PropertyInfo prop)
        {
            if (_settings is NotifyPropertyChangedProxy proxy)
            {
                return prop.GetValue(proxy.WrappedObject);
            }
            return prop.GetValue(_settings);
        }

        protected virtual string GetBeautyPropName(PropertyInfo prop)
        {
            var name = prop.Name;
            var sb = new StringBuilder();
            
            foreach (var c in name)
            {
                var curChar = c;
                if (char.IsUpper(curChar))
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(' ');
                        curChar = char.ToLower(c);
                    }
                }
                sb.Append(curChar);
            }
            return sb.ToString();
        }

        protected virtual void InitWindowSettings()
        {
            var type = GetObjectType();
            var formName = type.GetCustomAttribute<DisplayNameAttribute>();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            Title = formName?.DisplayName ?? type.Name;
            MinWidth = 300.0;
            MinHeight = 100.0;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.ToolWindow;
            Loaded += SettingsAutoUI_Loaded;
        }

        private void SettingsAutoUI_Loaded(object sender, RoutedEventArgs e)
        {
            var hWnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hWnd, GWL_STYLE, GetWindowLong(hWnd, GWL_STYLE) & WS_SYSMENU);
        }

        public static Button CreateSettingsClassController(object obj, string buttonText = "Open")
        {
            var button = new Button
            {
                Content = buttonText,
                Margin = new Thickness(5)
            };

            button.Click += (sender, args) =>
            {
                if (!(obj is INotifyPropertyChanged))
                {
                    obj = new NotifyPropertyChangedProxy(obj);
                }
                var settingsPage = new SettingsAutoUI(obj)
                {
                    Topmost = true,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                settingsPage.ShowDialog();

            };

            return button;
        }

        protected virtual ComboBox CreateEnumController(PropertyInfo prop)
        {
            var cb = new ComboBox
            {
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                DisplayMemberPath = nameof(ValueDescription.Description),
                SelectedValuePath = nameof(ValueDescription.Value)
            };
            var collectionBinding = new Binding
            {
                Path = new PropertyPath(prop.Name),
                Converter = EnumToCollectionConverter.Instance
            };
            cb.SetBinding(ItemsControl.ItemsSourceProperty, collectionBinding);
            var valueBinding = new Binding
            {
                Path = new PropertyPath(prop.Name),
                Converter = EnumValueConverter.Instance
            };
            cb.SetBinding(Selector.SelectedItemProperty, valueBinding);
            return cb;
        }

        protected virtual CheckBox CreateBooleanController(PropertyInfo prop)
        {
            var cb = new CheckBox
            {
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var valueBinding = new Binding
            {
                Path = new PropertyPath(prop.Name)
            };
            cb.SetBinding(ToggleButton.IsCheckedProperty, valueBinding);
            return cb;
        }

        protected virtual Slider CreateDoubleController(PropertyInfo prop)
        {
            var limits = prop.GetCustomAttribute<SliderLimitsAttribute>();
            var ret = new Slider
            {
                AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
                Maximum = limits?.Max ?? 10,
                Minimum = limits?.Min ?? 1,
                TickFrequency = 0.05,
                AutoToolTipPrecision = 2,
                VerticalAlignment = VerticalAlignment.Center
            };
            var valueBinding = new Binding
            {
                Path = new PropertyPath(prop.Name)
            };
            ret.SetBinding(RangeBase.ValueProperty, valueBinding);
            return ret;
        }

        protected virtual UIElement CreateIntController(PropertyInfo prop)
        {
            var limits = prop.GetCustomAttribute<SliderLimitsAttribute>();
            UIElement result;
            if (limits != null)
            {
                var slider = new Slider
                {
                    AutoToolTipPlacement = AutoToolTipPlacement.TopLeft,
                    Maximum = limits.Max,
                    Minimum = limits.Min,
                    TickFrequency = (int)limits.TickFrequency,
                    LargeChange = (int)limits.LargeChange,
                    AutoToolTipPrecision = 0,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var valueBinding = new Binding
                {
                    Path = new PropertyPath(prop.Name)
                };
                slider.SetBinding(RangeBase.ValueProperty, valueBinding);
                result = slider;
            }
            else
            {
                result = CreateTextBlockController(prop);
            }
            return result;
        }

        #region PInvoke
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_STYLE = -16;

        private const int WS_SYSMENU = -524288;
        #endregion
    }
}