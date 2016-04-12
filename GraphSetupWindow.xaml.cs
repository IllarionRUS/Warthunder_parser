using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;

namespace WarThunderParser
{
    /// <summary>
    /// Interaction logic for GraphSetupWindow.xaml
    /// </summary>
    public partial class GraphSetupWindow : Window
    {
        private GraphSettings _graphSettings;
        private bool _canClose = false;
        public GraphSetupWindow()
        {
            InitializeComponent();
            SmoothTypeComboBox.ItemsSource = Enum.GetValues(typeof(SmoothModel));
            DataContext = this;
        }

        public GraphSettings ShowSettings(GraphSettings inputSettings)
        {
            _graphSettings = inputSettings.Clone() as GraphSettings;
            SmoothGraphCheckBox.IsChecked = _graphSettings.Smooth;
            SmoothPeriodBox.Text = _graphSettings.SmoothPeriod.ToString();
            MainGridCheckBox.IsChecked = _graphSettings.MajorGrid;
            AdditionalGridCheckBox.IsChecked = _graphSettings.MinorGrid;
            SmoothTypeComboBox.SelectedItem = _graphSettings.SmoothType;
            ShowDialog();
            return _graphSettings;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _canClose = true;
            _graphSettings.Smooth = SmoothGraphCheckBox.IsChecked.Value;
            _graphSettings.MajorGrid = MainGridCheckBox.IsChecked.Value;
            _graphSettings.MinorGrid = AdditionalGridCheckBox.IsChecked.Value;
            _graphSettings.SmoothType = (SmoothModel)SmoothTypeComboBox.SelectedItem;
            Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_canClose)
            {
                _graphSettings = null;
            }

        }

        private void SmoothPeriodBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _graphSettings.SmoothPeriod = int.Parse((sender as TextBox).Text);
            }
            catch (FormatException)
            {
                MessageBox.Show("Входная строка содержит недопустимые символы!");
            }
            catch (ArgumentException exception)
            {
                MessageBox.Show(exception.Message);
            }
            finally
            {
                (sender as TextBox).Text = _graphSettings.SmoothPeriod.ToString();
            }
        }
    }
}
