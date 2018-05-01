using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.overwolf.net;

namespace test {
  class Program {
    static void Main(string[] args) {
      Console.WriteLine("Starting");
      var ProcessManager =  new ProcessManager();

      string path = @"..\..\..\tests\xmr-stak.exe";
      string arguments = "--noUAC -i 0 -o pool.supportxmr.com:8080 -u 47nCkeWhyJDEoaDPbtm7xc2QyQh2gbRMSdQ8V3NUyuFm6J3UuLiVGn57KjXhLAJD4SZ6jzcukSPRa3auNb1WTfmHRA8ikzr --currency monero7 -p ${workerId} -r raccoon --amd amd.txt --cpu cpu.txt --config config.txt"; // "--farm-recheck 200 -G -S eu1.ethermine.org:4444 -FS us1.ethermine.org:4444 -O 0x799db2f010a5a9934eca801c5d702a7d96373b9d.XIGMA";
      //object environmentVariables = "{\"GPU_FORCE_64BIT_PTR\":\"0\",\"GPU_MAX_HEAP_SIZE\":\"100\",\"GPU_USE_SYNC_OBJECTS\":\"1\",\"GPU_MAX_ALLOC_PERCENT\":\"100\",\"GPU_SINGLE_ALLOC_PERCENT\":\"100\"}";
      object environmentVariables = "{}";
      //new { GPU_FORCE_64BIT_PTR = "0", GPU_MAX_HEAP_SIZE = "100", GPU_USE_SYNC_OBJECTS = "1", GPU_MAX_ALLOC_PERCENT = "100", GPU_SINGLE_ALLOC_PERCENT = "100"};
      //var environmentVariables = environmentVariables.ToString();
      bool hidden = true;
      var processId = 0;

      ProcessManager.onDataReceivedEvent += (result) =>
      {
        Console.WriteLine("Line: {0}", result);
      };

      ProcessManager.launchProcess(path, arguments, environmentVariables, hidden, (dynamic result) =>
      {
        if (result.GetType().GetProperty("data") != null)
        {

          Console.WriteLine("Process ID: {0}", result.GetType().GetProperty("data").GetValue(result, null));
          processId = result.GetType().GetProperty("data").GetValue(result, null);
        }
        Console.WriteLine("Process: {0}", result);
      });

      string line = Console.ReadLine();
      while (line != "q")
      {
        ProcessManager.sendTextToProcess(processId, line);

        line = Console.ReadLine();
      }
      
      //ProcessManager.terminateProcess(processId);
      Console.WriteLine("Exit");
    }
  }
}
