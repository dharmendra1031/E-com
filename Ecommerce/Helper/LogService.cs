using System;
using System.IO;
namespace EcommerceWebApplication.Helpers
{
    public static class LogService
    {
        // Log folder path
        private static readonly string logFolderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\Logs");

        // Constructor to ensure the folder exists
        static LogService()
        {
            // Check if log folder exists, if not create it
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }
        }
        // Method to generate log files daywise
        public static void WriteLog(string message)
        {
            try
            {
                // Get the current date for filename
                string fileName = $"{DateTime.Now:yyyy-MM-dd}.txt";
                string filePath = Path.Combine(logFolderPath, fileName);
                // Ensure file exists (create if missing)
                if (!File.Exists(filePath))
                {
                    using (File.Create(filePath)) { }
                }

                // Write the log message to the file
                string logMessage = $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} - {message}";

                // Append the log to the file
                File.AppendAllText(filePath, logMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Handle any errors that may occur during logging
                Console.WriteLine("Error writing log: " + ex.Message);
            }
        }
    }
}
