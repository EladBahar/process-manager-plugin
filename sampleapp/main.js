var plugin = new OverwolfPlugin("process-manager-plugin", true);
var processId = 0;

plugin.initialize(status => {
  if (status == false) {
    document.querySelector('#title').innerText = "Plugin couldn't be loaded??";
    return;
  }
  
  /*plugin.get().onProcessExited.addListener(function(e) {
    console.log("process exit:", e);
    if (processId == e.processId) {
      processId = 0;
    }
  });*/
 
  const path = "helpers\\ethminer.exe";
  const args = "--farm-recheck 200 -G -S eu1.ethermine.org:4444 -FS us1.ethermine.org:4444 -O 0x799db2f010a5a9934eca801c5d702a7d96373b9d.XIGMA";
  const environmentVariables = { GPU_FORCE_64BIT_PTR: "0", GPU_MAX_HEAP_SIZE: "100", GPU_USE_SYNC_OBJECTS: "1", GPU_MAX_ALLOC_PERCENT: "100", GPU_SINGLE_ALLOC_PERCENT: "100"};
  const hidden = true;

  plugin.get().onDataReceivedEvent.addListener(({ error, data }) => {
    if (error) {
      console.error(error);
    }
    if (data) {
      console.log(data);
    }
    document.querySelector('#messages').innerText = error || data;
  });

  let processId;
  plugin.get().launchProcess(path, args, JSON.stringify(environmentVariables), hidden, ({ error, data }) => {
    if (error) {
      console.error(error);
    }
    if (data) {
      processId = data;
    }
    document.querySelector('#title').innerText = error || data;
  });
 
  //plugin.get().suspendProcess(processId,console.log)
  //plugin.get().resumeProcess(processId,console.log)
  //plugin.get().terminateProcess(processId)
 });
