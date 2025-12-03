using System.Diagnostics;
using System.Text;

namespace h1dr0n.EditorTools
{
    public static class Waifu2xEngine
    {
        public static ProcessResult Process(
            string inputPath,
            string outputPath,
            out string output,
            out string error,
            string model = "cunet",
            int scale = 2,
            int noiseLevel = -1,
            int tileSize = 0,
            bool tta = false,
            int gpuId = 0,
            string format = "png")
        {
            string exe = PathResolver.GetWaifu2xPath();
            if (string.IsNullOrEmpty(exe))
            {
                output = "";
                error = "Waifu2x executable not found";
                return ProcessResult.ExecutableNotFound;
            }

            // Build arguments
            var args = new StringBuilder();
            args.Append($"-i \"{inputPath}\" -o \"{outputPath}\"");
            
            // Model
            args.Append($" -m models-{model}");
            
            // Scale
            args.Append($" -s {scale}");
            
            // Noise level
            args.Append($" -n {noiseLevel}");
            
            // Tile size
            if (tileSize > 0)
                args.Append($" -t {tileSize}");
            
            // TTA mode
            if (tta)
                args.Append(" -x");
            
            // GPU ID
            if (gpuId >= 0)
                args.Append($" -g {gpuId}");
            
            // Format
            args.Append($" -f {format}");

            // Execute process
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            try
            {
                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return process.ExitCode == 0 
                        ? ProcessResult.Success 
                        : ProcessResult.ProcessError;
                }
            }
            catch (System.Exception ex)
            {
                output = "";
                error = ex.Message;
                return ProcessResult.Exception;
            }
        }
    }
}
