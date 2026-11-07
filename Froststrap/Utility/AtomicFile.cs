// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

namespace Froststrap.Utility
{
    internal static class AtomicFile
    {
        /// Writes to a temp file next to `path`, then swaps it in, so a failed or interrupted
        /// write never leaves a half-written file. `ownerOnly` creates it as 0600 on Unix.
        public static void WriteAllText(string path, string contents, bool ownerOnly = false)
        {
            string temp = $"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (ownerOnly && !OperatingSystem.IsWindows())
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

                using (var stream = new FileStream(temp, options))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temp, path, overwrite: true);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
