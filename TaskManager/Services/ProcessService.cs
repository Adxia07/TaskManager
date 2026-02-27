using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using TaskManager.Models;

namespace TaskManager.Services
{
    public class ProcessService
    {
        public List<ProcessInfo> GetAllProcesses()
        {
            var list = new List<ProcessInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    list.Add(new ProcessInfo
                    {
                        Id = p.Id,
                        Name = p.ProcessName,
                        Priority = p.PriorityClass,
                        MemoryUsage = p.WorkingSet64,
                        ThreadCount = p.Threads.Count,
                        CpuTime = p.TotalProcessorTime,
                        ParentId = GetParentProcessIdSafe(p.Id)
                    });
                }
                catch { }
            }
            return list.OrderBy(p => p.Name).ToList();
        }

        public bool SetPriority(int processId, ProcessPriorityClass priority)
        {
            try
            {
                var p = Process.GetProcessById(processId);
                p.PriorityClass = priority;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SetAffinity(int processId, IntPtr mask)
        {
            try
            {
                var p = Process.GetProcessById(processId);
                p.ProcessorAffinity = mask;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<ProcessThread> GetThreads(int processId)
        {
            var list = new List<ProcessThread>();
            try
            {
                var p = Process.GetProcessById(processId);
                foreach (ProcessThread t in p.Threads)
                    list.Add(t);
            }
            catch { }
            return list;
        }

        private int? GetParentProcessIdSafe(int pid)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}"))
                {
                    var obj = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                    if (obj != null)
                        return (int)(uint)obj["ParentProcessId"];
                }
            }
            catch { }
            return null;
        }

        public List<ProcessTreeNode> BuildProcessTree()
{
            var processes = GetAllProcesses();

            var dict = processes.ToDictionary(p => p.Id, p => new ProcessTreeNode { Process = p });
            var roots = new List<ProcessTreeNode>();

            foreach (var node in dict.Values)
            {
                if (node.Process.ParentId.HasValue && dict.ContainsKey(node.Process.ParentId.Value))
                {
                    dict[node.Process.ParentId.Value].Children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            return roots;
        }
    }
}