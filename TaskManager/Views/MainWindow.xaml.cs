using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using TaskManager.ViewModels;

namespace TaskManager.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel VM => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            // Добавляем обработчик клавиши Esc для закрытия окна
            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    this.Close();
                }
            };
        }

        private void High_Click(object sender, RoutedEventArgs e)
        {
            VM?.SetPriority(ProcessPriorityClass.High);
        }

        private void Normal_Click(object sender, RoutedEventArgs e)
        {
            VM?.SetPriority(ProcessPriorityClass.Normal);
        }

        private void Realtime_Click(object sender, RoutedEventArgs e)
        {
            VM?.SetPriority(ProcessPriorityClass.RealTime);
        }

        private void Affinity_Click(object sender, RoutedEventArgs e)
        {
            VM?.SetAffinityToFirstCore();
        }
    }
}