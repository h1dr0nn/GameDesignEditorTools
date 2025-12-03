using System.Diagnostics;
using System.Text;

namespace h1dr0n.EditorTools
{
    public static class RealESRGANEngine
    {
        public static ProcessResult Process(
            string inputPath,
            string outputPath,
            out string output,
            out string error,
            string model = "realesrgan-x4plus",
            int scale = 4,
            float? customOutscale = null,
            int tileSize = 0,
            bool faceEnhance = false,
            string format = "auto",
            bool fp32 = false)
        {
            string exe = PathResolver.GetRealESRGANPath();
            if (string.IsNullOrEmpty(exe))
            {
                output = "";
                error = "Real-ESRGAN executable not found";
                return ProcessResult.ExecutableNotFound;
            }

            // Build arguments
            var args = new StringBuilder();
            args.Append($"-i \"{inputPath}\" -o \"{outputPath}\"");
            
            // Model
            args.Append($" -n {model}");
            
            // Scale or outscale
            if (customOutscale.HasValue)
                args.Append($" --outscale {customOutscale.Value}");
            else
                args.Append($" -s {scale}");
            
            // Tile size
            if (tileSize > 0)
                args.Append($" -t {tileSize}");
            
            // Face enhancement
            if (faceEnhance)
                args.Append(" --face_enhance");
            
            // Format
            if (format != "auto")
                args.Append($" --ext {format}");
            
            // FP32
            if (fp32)
                args.Append(" --fp32");

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

    public enum ProcessResult
    {
        Success,
        ExecutableNotFound,
        ProcessError,
        Exception
    }
}
