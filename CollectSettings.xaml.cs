using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WarThunderParser
{
    /// <summary>
    /// Interaction logic for CollectSettings.xaml
    /// </summary>
    public partial class CollectSetupWindow : Window
    {
        private bool _canClose = false;
        public CollectSetupWindow()
        {
            InitializeComponent();
        }

        private CollectSettings _collectSettings;
        public CollectSettings ShowSettings(CollectSettings inputSettings)
        {
            _collectSettings = inputSettings.Clone() as CollectSettings;
            InterpIntervalBox.Text = _collectSettings.InterpInterval.ToString();
            RequestIntervalBox.Text = _collectSettings.RequestInterval.ToString();
            FailureDelayBox.Text = _collectSettings.FailureDelay.ToString();
            OutlierCheckBox.IsChecked = _collectSettings.AllowOutlierFilter;
            AllowFiltersCheckBox.IsChecked = _collectSettings.AllowInputFilters;
            ExcelSelectionRangeCheckBox.IsChecked = _collectSettings.ExcelSelectedOnly;
            FiltersListBox.ItemsSource = _collectSettings.FilterList;
            ShowDialog();
            return _collectSettings;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_canClose) _collectSettings = null;
        }

        private void RequestIntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _collectSettings.RequestInterval = int.Parse((sender as TextBox).Text);
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
                (sender as TextBox).Text = _collectSettings.RequestInterval.ToString();
            }
        }

        private void InterpIntervalBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _collectSettings.InterpInterval = int.Parse((sender as TextBox).Text);
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
                (sender as TextBox).Text = _collectSettings.InterpInterval.ToString();
            }
        }
        private void FailureDelayBox_LostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                _collectSettings.FailureDelay = int.Parse((sender as TextBox).Text);
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
                (sender as TextBox).Text = _collectSettings.FailureDelay.ToString();
            }
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrWhiteSpace(FilterNameTextBox.Text))return;
            _collectSettings.FilterList.Add(FilterNameTextBox.Text);
            FiltersListBox.Items.Refresh();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DeleteSelectedFromFilterList();
        }

        void DeleteSelectedFromFilterList()
        {

            foreach (string item in FiltersListBox.SelectedItems)
            {
                _collectSettings.FilterList.Remove(item);
            }
            FiltersListBox.Items.Refresh();
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            _canClose = true;
            _collectSettings.AllowOutlierFilter = OutlierCheckBox.IsChecked.Value;
            _collectSettings.AllowInputFilters = AllowFiltersCheckBox.IsChecked.Value;
            _collectSettings.ExcelSelectedOnly = ExcelSelectionRangeCheckBox.IsChecked.Value;
            Close();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void FiltersListBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete) DeleteSelectedFromFilterList();
        }

        
    }

    public class CollectSettings:ICloneable
    {
        #region Constants
        private const int MinRequestInterval = 0;
        private const int MaxRequestInterval = 5000;
        private const int MinInterpInterval = 50;
        private const int MaxInterpInterval = 10000;
        private const int MinFailureDelay = 0;
        private const int MaxFailureDelay = 10000;
        #endregion

        public bool ExcelSelectedOnly = false;
        public bool AllowOutlierFilter = true;
        public bool AllowInputFilters = true;
        public List<string> FilterList = new List<string>();
        private int _requestInterval = 0;
        private int _interpInterval = 200;
        private int _failureDelay = 1000;
        public int FailureDelay
        {
            get { return _failureDelay; }
            set
            {
                if ((value < MinFailureDelay) || (value > MaxFailureDelay))
                    throw new ArgumentException("Интервал запросов должен лежать в пределах от " + MinFailureDelay + "мс до " + MaxFailureDelay + "мс.");
                _failureDelay = value;
            }
        }
        public int RequestInterval
        {
            get { return _requestInterval; }
            set
            {
                if ((value < MinRequestInterval) || (value > MaxRequestInterval))
                    throw new ArgumentException("Интервал запросов должен лежать в пределах от " + MinRequestInterval + "мс до " + MaxRequestInterval + "мс.");
                _requestInterval = value;
            }
        }

        public int InterpInterval
        {
            get { return _interpInterval; }
            set
            {
                if((value<MinInterpInterval)||(value>MaxInterpInterval)) 
                    throw new ArgumentException("Интервал интерполяции должен лежать в пределах от "+MinInterpInterval+"мс до "+MaxInterpInterval+"мс.");
                _interpInterval = value;
            }
        }

        public object Clone()
        {
            var resultSettings = new CollectSettings
            {
                ExcelSelectedOnly = this.ExcelSelectedOnly,
                AllowOutlierFilter = this.AllowOutlierFilter,
                AllowInputFilters = this.AllowInputFilters,
                FilterList = new List<string>(this.FilterList),
                _requestInterval = this._requestInterval,
                _interpInterval = this._interpInterval,
                _failureDelay = this._failureDelay
            };
            return resultSettings;
        }
    }
}
