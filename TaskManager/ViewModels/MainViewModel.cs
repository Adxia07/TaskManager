using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TaskManager.Models;
using TaskManager.Services;
using TaskManager.Utilities;
using TaskManager.ViewModels;

namespace TaskManager.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ProcessService _service = new ProcessService();
        private DispatcherTimer _timer;
        public ObservableCollection<bool> SelectedCores { get; }
    = new ObservableCollection<bool>();

        public int CoreCount => Environment.ProcessorCount;

        public ICommand ShowHelpCommand { get; }

        public ObservableCollection<ProcessInfo> Processes { get; set; } = new ObservableCollection<ProcessInfo>();

        private ProcessInfo _selectedProcess;
        public ProcessInfo SelectedProcess
        {
            get => _selectedProcess;
            set { _selectedProcess = value; OnPropertyChanged(); LoadThreads(); }
        }

        public ObservableCollection<string> Threads { get; set; } = new ObservableCollection<string>();

        public string SearchText { get; set; }

        public ObservableCollection<ProcessTreeNode> ProcessTree { get; set; }
    = new ObservableCollection<ProcessTreeNode>();

        public SeriesCollection CpuSeries { get; set; }
        public double CpuValue { get; set; }

        private TimeSpan _lastCpuTime;
        private DateTime _lastCpuCheck = DateTime.Now;

        public ICommand ApplyAffinityCommand { get; }

        public MainViewModel()
        {
            CpuSeries = new SeriesCollection
    {
        new LineSeries
        {
            Values = new ChartValues<double>(),
            PointGeometry = null
        }
    };
            ApplyAffinityCommand = new RelayCommand(_ => ApplyAffinity());
            ShowHelpCommand = new RelayCommand(_ => ShowHelp()); // Новая команда

            for (int i = 0; i < CoreCount; i++)
            {
                SelectedCores.Add(true);
            }
            Refresh();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RefreshInterval) };
            _timer.Tick += (s, e) => Refresh();
            _timer.Start();
        }




        private void UpdateTimerInterval()
        {
            if (_timer != null)
                _timer.Interval = TimeSpan.FromMilliseconds(RefreshInterval);
        }

        private int _refreshInterval = 20000; // 20 секунд по умолчанию
        public int RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                _refreshInterval = value;
                OnPropertyChanged();
                UpdateTimerInterval();
            }
        }

        public void LoadAffinityFromSelectedProcess()
        {
            if (SelectedProcess == null) return;

            try
            {
                var process = Process.GetProcessById(SelectedProcess.Id);
                var mask = process.ProcessorAffinity;

                for (int i = 0; i < CoreCount; i++)
                    SelectedCores[i] = AffinityHelper.IsCoreEnabled(mask, i);

                OnPropertyChanged(nameof(SelectedCores));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось получить affinity: " + ex.Message);
            }
        }

        public void ApplyAffinity()
        {
            if (SelectedProcess == null) return;

            try
            {
                var process = Process.GetProcessById(SelectedProcess.Id);
                var cores = SelectedCores.ToArray();

                if (!cores.Any(c => c))
                {
                    MessageBox.Show("Должно быть выбрано хотя бы одно ядро.");
                    return;
                }

                process.ProcessorAffinity = AffinityHelper.SetCoreMask(cores);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при установке CPU affinity: " + ex.Message);
            }
        }

        private void UpdateCpuChart()
        {
            if (CpuSeries == null || CpuSeries.Count == 0 || CpuSeries[0].Values == null)
                return;

            try
            {
                var proc = Process.GetCurrentProcess();
                var now = DateTime.Now;

                var deltaCpu = (proc.TotalProcessorTime - _lastCpuTime).TotalMilliseconds;
                var deltaTime = (now - _lastCpuCheck).TotalMilliseconds;

                var cpuUsage = deltaCpu / (deltaTime * Environment.ProcessorCount) * 100;

                CpuValue = Math.Round(cpuUsage, 2);
                OnPropertyChanged(nameof(CpuValue));

                var values = (ChartValues<double>)CpuSeries[0].Values;
                values.Add(CpuValue);
                if (values.Count > 30)
                    values.RemoveAt(0);

                _lastCpuTime = proc.TotalProcessorTime;
                _lastCpuCheck = now;
            }
            catch
            {
                // процесс мог завершиться — тихо игнорируем
            }
        }

        public async void Refresh()
        {
            try
            {
                var search = SearchText?.ToLower();

                // Тяжёлая часть — в фоне
                var result = await Task.Run(() =>
                {
                    var list = _service.GetAllProcesses();

                    if (!string.IsNullOrWhiteSpace(search))
                        list = list.Where(p => p.Name.ToLower().Contains(search)).ToList();

                    var tree = _service.BuildProcessTree();

                    return (list, tree);
                });

               
                Processes.Clear();
                foreach (var p in result.list)
                    Processes.Add(p);

                ProcessTree.Clear();
                foreach (var node in result.tree)
                    ProcessTree.Add(node);

                UpdateCpuChart();
            }
            catch
            {
                
            }
        }


        public void SetPriority(ProcessPriorityClass priority)
        {
            if (SelectedProcess == null) return;

            if (priority == ProcessPriorityClass.RealTime)
            {
                var r = MessageBox.Show(
                    "Realtime может нарушить работу системы. Продолжить?",
                    "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            if (!_service.SetPriority(SelectedProcess.Id, priority))
                MessageBox.Show("Недостаточно прав или процесс завершён");
        }

        public void SetAffinityToFirstCore()
        {
            if (SelectedProcess == null) return;

            bool[] cores = new bool[Environment.ProcessorCount];
            cores[0] = true;

            var mask = AffinityHelper.SetCoreMask(cores);
            if (!_service.SetAffinity(SelectedProcess.Id, mask))
                MessageBox.Show("Не удалось изменить CPU Affinity");
        }

        private void LoadThreads()
        {
            Threads.Clear();
            if (SelectedProcess == null) return;

            foreach (var t in _service.GetThreads(SelectedProcess.Id))
                Threads.Add($"ID: {t.Id}, Priority: {t.PriorityLevel}, State: {t.ThreadState}");
        }
        private void ShowHelp()
        {
            string helpText =
                "СПРАВКА ПО ПРОГРАММЕ\n" +
                "═══════════════════════\n\n" +
                "ПОИСК ПРОЦЕССОВ:\n" +
                "• Введите название процесса в поле поиска\n" +
                "• Поиск работает в реальном времени\n\n" +
                "ИНТЕРВАЛ ОБНОВЛЕНИЯ:\n" +
                "• Можно изменить частоту обновления списка\n" +
                "• Значение в миллисекундах (по умолчанию 20000)\n\n" +
                "СПИСОК ПРОЦЕССОВ:\n" +
                "• Кликните на процесс для выбора\n" +
                "• Отображается ID, имя, количество потоков и память\n" +
                "• Можно сортировать по любому столбцу\n\n" +
                "ДЕРЕВО ПРОЦЕССОВ:\n" +
                "• Показывает иерархию процессов (родитель-потомок)\n" +
                "• Наведите курсор для просмотра ID процесса\n\n" +
                "УПРАВЛЕНИЕ ПРИОРИТЕТОМ:\n" +
                "• High - высокий приоритет\n" +
                "• Normal - обычный приоритет\n" +
                "• RealTime - реального времени (осторожно!)\n\n" +
                "CPU AFFINITY:\n" +
                "• Выберите ядра для выполнения процесса\n" +
                "• Нажмите 'Применить affinity' для сохранения\n" +
                "• 'Одно ядро' - быстрое назначение первого ядра\n\n" +
                "ГРАФИК CPU:\n" +
                "• Показывает загрузку процессора в реальном времени\n\n" +
                "ПОТОКИ ПРОЦЕССА:\n" +
                "• Отображаются все потоки выбранного процесса\n\n" +
                "ДОПОЛНИТЕЛЬНО:\n" +
                "• Для закрытия программы нажмите ESC\n" +
                "• Все изменения требуют прав администратора\n\n" +
                "© 2026 Диспетчер задач";

            MessageBox.Show(helpText, "Справка", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}