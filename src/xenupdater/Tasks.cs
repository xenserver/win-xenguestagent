using System;
using System.ComponentModel;
using TaskScheduler;
using System.Diagnostics;

namespace XenUpdater
{
    class Tasks : IDisposable
    {
        #region Constants
        const string TASKNAME = "Citrix XenGuestAgent Auto-Updater";
        const string AUTHOR = "Citrix Systems, Inc.";
        const string DESCRIPTION = "Automatically checks and updates Xen Guest Agent";

        const string TaskPathSeparator = "\\";
        const string DateTimeFormatExpectedByCOM = "yyyy-MM-ddThh:mm:ss";
        #endregion

        #region Private Fields
        private ITaskService taskService = new TaskScheduler.TaskScheduler();
        private bool isDisposed = false;
        #endregion

        public Tasks()
        {
        }

        ~Tasks()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!this.isDisposed)
                {
                    ReleaseComObject(this.taskService);
                    if (disposing)
                    {
                        // dispose managed resources here 
                        this.taskService = null;
                    }
                    this.isDisposed = true;
                }
            }
        }
        private static void ReleaseComObject(object comObject)
        {
            System.Runtime.InteropServices.Marshal.ReleaseComObject(comObject);
        }

        #region Helpers for Creating/Removing Task
        public void AddTask()
        {
            ConnectTaskSchedulerService();

            ITaskDefinition task = taskService.NewTask(0);
            task.RegistrationInfo.Author = AUTHOR;
            task.RegistrationInfo.Description = DESCRIPTION;
            task.Settings.AllowDemandStart = true;
            task.Settings.Compatibility = _TASK_COMPATIBILITY.TASK_COMPATIBILITY_V2_1;
            task.Settings.Enabled = true;
            task.Settings.Hidden = false;
            task.Settings.MultipleInstances = _TASK_INSTANCES_POLICY.TASK_INSTANCES_IGNORE_NEW;
            task.Settings.RunOnlyIfNetworkAvailable = true;
            task.Settings.StartWhenAvailable = true;
            task.Settings.StopIfGoingOnBatteries = false;

            DateTime start = DateTime.Now;
            DateTime end = start.AddYears(10);

            IDailyTrigger trigger = (IDailyTrigger)task.Triggers.Create(_TASK_TRIGGER_TYPE2.TASK_TRIGGER_DAILY);
            trigger.Enabled = true;
            trigger.StartBoundary = start.ToString(DateTimeFormatExpectedByCOM);
            trigger.EndBoundary = end.ToString(DateTimeFormatExpectedByCOM);
            trigger.DaysInterval = 1; // every day
            trigger.RandomDelay = "P0DT4H0M0S"; // up-to-4 hours random

            IExecAction action = (IExecAction)task.Actions.Create(_TASK_ACTION_TYPE.TASK_ACTION_EXEC);
            action.Path = Process.GetCurrentProcess().MainModule.FileName;

            ITaskFolder root = taskService.GetFolder(TaskPathSeparator);
            root.RegisterTaskDefinition(TASKNAME, task, 6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_NONE, null);
        }

        public void RemoveTask()
        {
            ConnectTaskSchedulerService();
            ITaskFolder folder = taskService.GetFolder(TaskPathSeparator);
            folder.DeleteTask(TASKNAME, 0);
            ReleaseComObject(folder);
        }
        #endregion

        #region Private Helpers
        void ConnectTaskSchedulerService()
        {
            if (!this.taskService.Connected)
            {
                this.taskService.Connect();
            }
        }
        #endregion
    }
}
