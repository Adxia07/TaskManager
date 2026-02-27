using System.Collections.ObjectModel;
using TaskManager.Models;

namespace TaskManager.Models
{
    public class ProcessTreeNode
    {
        public ProcessInfo Process { get; set; }
        public ObservableCollection<ProcessTreeNode> Children { get; set; }
            = new ObservableCollection<ProcessTreeNode>();
    }
}