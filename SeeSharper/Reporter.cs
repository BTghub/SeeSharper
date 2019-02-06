/*
 * File: Reporter.cs
 * Author: Brian Fehrman (fullmetalcache) & Bretton Tan
 * Date: 2019-01-28
 * Description:
 *      Contains the code that generates the report file. Uses
 *      template files to create base HTML code and then find and
 *      replace to dynamically generate each screenshots section.
 */

using System.IO;

namespace SeeSharper
{
    class Reporter
    {
        private static string _reportPath = "SeeSharpestReport.html"; //Default report name

        public Reporter(string report_path = "SeeSharpestReport.html")
        {
            _reportPath = report_path;
            
            //copy base html to report file
            var byteArray = Properties.Resources.report;
            File.WriteAllBytes(_reportPath, byteArray);
        }

        /// <summary>
        /// Function to take the inputted screenshot and 
        /// generate the HTML code to be added to the final report.
        /// Reads in tempate file as a string and uses String.Replace
        /// on special flags to inject the relevant information.
        /// </summary>
        /// <param name="filepath">File path to the screenshot taken by WebShot</param>
        /// <param name="url">URL of Site to Screenshot</param>
        /// <param name="rcode">Response code of the webrequest</param>
        public void CreateHTML(string filepath, string url, string rcode)
        {
            var byteArray = Properties.Resources.SSreport; //read-in embedded template file as array of bytes
            string fileContent = System.Text.Encoding.Default.GetString(byteArray); //convert bytes to string type
            fileContent = fileContent.Replace("^^SCF^^", filepath);
            fileContent = fileContent.Replace("^^URL^^", url);
            fileContent = fileContent.Replace("^^RC^^", rcode);
            File.WriteAllText("Screenshot.html", fileContent);
            AddToReport();
        }

        /// <summary>
        /// Function to append the created HTML code of a screenshot
        /// to the final report template.
        /// </summary>
        private void AddToReport()
        {
            if (File.Exists("Screenshot.html"))
            {
                using (StreamWriter fw = File.AppendText(_reportPath))
                {
                    string newContent = File.ReadAllText("Screenshot.html");
                    fw.WriteLine(newContent);
                }
                File.Delete("Screenshot.html");
            }
        }

        /// <summary>
        /// Function to append the closing body tag to
        /// the final report so it can be rendered in browser.
        /// Referenced as static so it can be called from Program.cs
        /// once all the screenshots have finished being taken.
        /// </summary>
        public static void FinalizeReport()
        {
            if (File.Exists(Reporter._reportPath))
            {
                using (StreamWriter fw = File.AppendText(Reporter._reportPath))
                {
                    fw.WriteLine("</body>");
                }
            }
        }
    }
}
