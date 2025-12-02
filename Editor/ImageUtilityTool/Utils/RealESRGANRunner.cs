using System.Diagnostics;

namespace h1dr0n.EditorTools
{
    public static class RealESRGANRunner
    {
        public static bool Run(string exePath, string input, string output, int scale = 4)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-i \"{input}\" -o \"{output}\" -s {scale}",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);
            proc.WaitForExit();
            return proc.ExitCode == 0;
        }
    }
}
