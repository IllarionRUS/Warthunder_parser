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
    /// Interaction logic for AskGraphNameWindow.xaml
    /// </summary>
    public partial class AskGraphNameWindow : Window
    {
        public AskGraphNameWindow()
        {
            InitializeComponent();
        }
        private string resultName;
        public string GetName(string inputName)
        {
            resultName = inputName;
            GraphNameTextBox.Text = inputName;
            ShowDialog();
            return resultName;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GraphNameTextBox.Text))
            {
                MessageBox.Show("Имя не может быть пустым!");
            }
            else
            {
                resultName = GraphNameTextBox.Text;
            }
            Close();
        }
    }
}
