using System.Diagnostics;

if (!Environment.IsPrivilegedProcess) {
    var psi = new ProcessStartInfo(Environment.ProcessPath!) { Verb = "runas", UseShellExecute = true };
    Process.Start(psi);
    return;
}
StartGame.Run();