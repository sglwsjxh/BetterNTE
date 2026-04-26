using System.Diagnostics;
using OpenCvSharp;

if (!Environment.IsPrivilegedProcess) {
    var psi = new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true };
    Process.Start(psi);
    return;
}

const string PROCESS_NAME = "HTGame";

var config = Config.Load();
StartGame.Run();

using var frame = new Mat();
while (Process.GetProcessesByName(PROCESS_NAME).Length > 0) {
    Capture.CaptureScreen(frame);

    if (config.Options.AutoTeleport)
        AutoTeleport.Run(frame);

    Thread.Sleep(100);
}

ImageMatch.ClearTemplateCache();