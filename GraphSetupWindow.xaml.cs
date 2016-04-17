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
        private GraphSettings m_GraphSettings;
        private bool _canClose = false;
        public GraphSetupWindow()
        {
            InitializeComponent();
            cmb_SmoothType.ItemsSource = Enum.GetValues(typeof(SmoothModel));
            DataContext = this;
        }

        public GraphSettings ShowSettings(GraphSettings inputSettings)
        {
            m_GraphSettings = inputSettings.Clone() as GraphSettings;
            cb_Smooth.IsChecked = m_GraphSettings.Smooth;
            edt_SmoothPeriod.Text = m_GraphSettings.SmoothPeriod.ToString();
            cb_PrimaryGrid.IsChecked = m_GraphSettings.MajorGrid;
            cb_AdditionalGrid.IsChecked = m_GraphSettings.MinorGrid;
            cmb_SmoothType.SelectedItem = m_GraphSettings.SmoothType;
            cb_ShowLegend.IsChecked = m_GraphSettings.LegendVisible;
            cb_AxisLabelsVisibility.IsChecked = m_GraphSettings.AxisLabelVisible;
            cb_AxisCurveColor.IsChecked = m_GraphSettings.AxisColorAsCurve;
            edt_AxisFontSize.Text = m_GraphSettings.AxisFontSize.ToString();
            ShowDialog();
            return m_GraphSettings;
        }

        private void btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            _canClose = true;
            m_GraphSettings.Smooth = cb_Smooth.IsChecked.Value;
            m_GraphSettings.MajorGrid = cb_PrimaryGrid.IsChecked.Value;
            m_GraphSettings.MinorGrid = cb_AdditionalGrid.IsChecked.Value;
            m_GraphSettings.SmoothType = (SmoothModel)cmb_SmoothType.SelectedItem;
            m_GraphSettings.LegendVisible = cb_ShowLegend.IsChecked.Value;
            m_GraphSettings.AxisLabelVisible = cb_AxisLabelsVisibility.IsChecked.Value;
            m_GraphSettings.AxisColorAsCurve = cb_AxisCurveColor.IsChecked.Value;
            Close();
        }

        private void btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_canClose)
            {
                m_GraphSettings = null;
            }

        }

        private void edt_SmoothPeriod_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                m_GraphSettings.SmoothPeriod = int.Parse((sender as TextBox).Text);
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
                (sender as TextBox).Text = m_GraphSettings.SmoothPeriod.ToString();
            }
        }

        private void edt_AxisFontSize_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                m_GraphSettings.AxisFontSize = int.Parse((sender as TextBox).Text);
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
                (sender as TextBox).Text = m_GraphSettings.AxisFontSize.ToString();
            }
        }
    }
}
