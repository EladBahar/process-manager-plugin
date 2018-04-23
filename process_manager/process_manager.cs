using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace com.overwolf.net {
  public class ProcessManager : IDisposable {

    #region pInvoke Helpers
    [Flags]
    public enum ThreadAccess : int {
      TERMINATE = (0x0001),
      SUSPEND_RESUME = (0x0002),
      GET_CONTEXT = (0x0008),
      SET_CONTEXT = (0x0010),
      SET_INFORMATION = (0x0020),
      QUERY_INFORMATION = (0x0040),
      SET_THREAD_TOKEN = (0x0080),
      IMPERSONATE = (0x0100),
      DIRECT_IMPERSONATION = (0x0200)
    }

    [DllImport("kernel32.dll")]
    static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
    [DllImport("kernel32.dll")]
    static extern uint SuspendThread(IntPtr hThread);
    [DllImport("kernel32.dll")]
    static extern int ResumeThread(IntPtr hThread);
    [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);
    #endregion

    private readonly string _dll_location;

    private Dictionary<int, Process> _running_process_ =
      new Dictionary<int, Process>();

    private bool _disposing = false;

    #region Events
    public event Action<object> onProcessExited;
    #endregion Events

    public ProcessManager() {
      _dll_location =
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    } 

    // a global event that triggers with two parameters:
    //
    // plugin.get().onGlobalEvent.addListener(function(first, second) {
    //  ...
    // });
    public event Action<object> onDataReceivedEvent;

    // path can be relative to the dll location or absolute
    public void launchProcess(string path, string arguments, object environmentVariables,
        bool hidden, Action<object> callback) {
      Task.Run(() => {
        try {
          string fullPath = string.Empty;
          if (!IsPathExists(path, out fullPath)) {
            callback(new { error = "can't find path" });
            return;
          }

          Process process = new Process();

          process.StartInfo.UseShellExecute = false;
          process.StartInfo.FileName = fullPath;
          process.StartInfo.Arguments = arguments;
          process.StartInfo.CreateNoWindow = hidden;
          process.StartInfo.WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
          process.StartInfo.RedirectStandardOutput = true;
          process.StartInfo.RedirectStandardError = true;
          process.StartInfo.RedirectStandardInput = true;
          process.EnableRaisingEvents = true;

          if (environmentVariables != null) {
            try {
             var jsonObject = JObject.Parse(environmentVariables.ToString());
             foreach (var item in jsonObject) {
               process.StartInfo.EnvironmentVariables[item.Key] = (string)item.Value;
             }
            } catch {
              callback(new { error = "can't set environment variables" });
              return;
            }
          }
           
          process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => {
            Console.WriteLine(e.Data);
            if (!String.IsNullOrEmpty(e.Data))
            {
              if (onDataReceivedEvent != null) {
                onDataReceivedEvent(new { error = e.Data });
              }
            }
          };
          process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
          {
            Console.WriteLine(e.Data);
            if (!String.IsNullOrEmpty(e.Data))
            {
              if (onDataReceivedEvent != null) { 
                onDataReceivedEvent(new { data = e.Data });
              }
            }
          };

          process.Start();
          ChildProcessTracker.AddProcess(process);

          process.BeginOutputReadLine();
          process.BeginErrorReadLine();

          lock (this) {
            _running_process_.Add(process.Id, process);
          }

          //process.WaitForExit();

          process.EnableRaisingEvents = true;
          process.Exited += ProcessExited;
          callback(new { data = process.Id });
        } catch (Exception ex) {
          callback(new { error = "unknown exception: " + ex.ToString() });
        }
      });
    }

    public void sendTextToProcess(int processId, string text)
    {
      Task.Run(() => {
        try
        {
          Process process = null;
          lock (this)
          {
            if (!_running_process_.TryGetValue(processId, out process))
              return;
          }
          StreamWriter streamWriter = process.StandardInput;
          streamWriter.WriteLine(text);
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.ToString());
        }
      });
    }

    public void terminateProcess(int processId) {
      Task.Run(() => {
        try {
          Process process = null;
          lock (this) {
            if (!_running_process_.TryGetValue(processId, out process))
              return;
          }
          Console.WriteLine("Terminate {0}", processId);
          process.Kill();
        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.ToString());
        }
      });
    }

    public void suspendProcess(int processId, Action<object> callback) {
      Task.Run(() => {
        try {
          Process process = null;
          lock (this) {
            if (!_running_process_.TryGetValue(processId, out process)) {
              callback(new { error = "can't find process: " + processId });
              return;
            }
          }


          foreach (ProcessThread pT in process.Threads) {
            IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

            if (pOpenThread == IntPtr.Zero) {
              continue;
            }

            SuspendThread(pOpenThread);

            CloseHandle(pOpenThread);
          }

          callback(new { data = "success" });
        } catch (Exception ex) {
          callback(new { error = "unknown exception: " + ex.ToString() });
        }
      });
    }

    public void resumeProcess(int processId, Action<object> callback) {
      Task.Run(() => {
        try {
          Process process = null;
          lock (this) {
            if (!_running_process_.TryGetValue(processId, out process)) {
              callback(new { error = "can't find process: " + processId });
              return;
            }
          }

          foreach (ProcessThread pT in process.Threads) {
            IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

            if (pOpenThread == IntPtr.Zero) {
              continue;
            }

            var suspendCount = 0;
            do {
              suspendCount = ResumeThread(pOpenThread);
            } while (suspendCount > 0);

            CloseHandle(pOpenThread);
          }
          callback(new { data = "success" });
        } catch (Exception ex) {
          callback(new { error = "unknown exception: " + ex.ToString() });
        }
      });
    }

    #region privates Func
    private void ProcessExited(object sender, EventArgs e) {
      if (_disposing)
        return;

      try {
        var process = sender as Process;
        if (process == null)
          return;

        int exitCode = 0;
        try { exitCode = process.ExitCode; } catch { }

        lock (this) {
          _running_process_.Remove(process.Id);
        }

        if (onProcessExited == null)
          return;

        onProcessExited(new { processId = process.Id, exitCode = exitCode });

      } catch {

      }
    }

    private bool IsPathExists(string path, out string fullPath) {
      fullPath = path;
      if (!File.Exists(fullPath)) {
        fullPath = Path.Combine(_dll_location, path);

        if (!File.Exists(fullPath)) {
          return false;
        }
      }
      return true;
    }
    #endregion Private func

    #region IDisposable
    public void Dispose() {
      _disposing = true;

      // clear and kill all process
      try {
        lock (this) {
          foreach (var entry in _running_process_) {
            try {
              entry.Value.Exited -= ProcessExited;
              entry.Value.Kill();
            } catch { }
          }

          _running_process_.Clear();
        }
      } catch {

      }
    }
    #endregion IDisposable
  }
}
