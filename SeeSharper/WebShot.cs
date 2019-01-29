/***
 * File: WebShot.cs
 * Author: Brian Fehrman (fullmetalcache)
 * Date: 2018-12-27
 * Description:
 *      Contains the code for taking screenshots of websites.
 *      Is slightly round-about to allow for ignoring certificate errors
 *      and not needing any third-party dependencies. Basic explanation follows.
 *      
 *      WebRequest class respects the settings of ServicePointManager.ServerCertificateValidationCallback,
 *      which allows for ignoring certificate errors. The WebRequest class, however,
 *      does not have a way to take a screenshot.
 *      
 *      The WebBrowser class allows for taking screenshots but does not allow for ignoring certficate errors.
 *      
 *      The solution here is to combine both WebRequest and WebBrowser to be able to take screenshots
 *      of websites and ignore certificate errors. WebRequest is used to make the initial request to get
 *      the HTML from the site. The HTML that was retrieved by WebRequest is then saved to a local .html
 *      file. The WebBrowser class is then used to load the local .html file, render the code, and 
 *      then take a screenshot of the rendered code. The local .html file is deleted after the rendered
 *      code has been transformed into an image.
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SeeSharper
{
    class WebShot
    {
        //private static Object _lockObject = new Object();
        private int _maxThreads;
        private int _threadsActive = 0;
        private HttpClient _webClient;
        private Reporter reporter;

        public WebShot( int maxThreads, int timeout)
        {
            _maxThreads = maxThreads;
            _webClient = new HttpClient();
            _webClient.Timeout = new TimeSpan(0,0,timeout); //sets request timeout to `timeout` seconds
            _webClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:64.0) Gecko/20100101 Firefox/64.0");
            _webClient.DefaultRequestHeaders.Add("Accept", "text/html"); //needed or you get flagged by godaddy dos protection
            //Create a new Reporter object for generating the final report
            reporter = new Reporter();
        }

        /// <summary>
        /// Function to create a screenshot of a given website.
        /// SSL Certificate errors are ignored.
        /// </summary>
        /// <param name="url">URL of Site to Screenshot</param>
        public async Task ScreenShot(string url)
        {
            //This line is needed to ignore cert errors
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true; 
            string responseFromServer = "";

            try
            {
                HttpResponseMessage resp = await _webClient.GetAsync(url);
                byte[] rfs = await resp.Content.ReadAsByteArrayAsync();
                responseFromServer = System.Text.Encoding.UTF8.GetString(rfs, 0, rfs.Length);

                if (resp.IsSuccessStatusCode)
                {
                    //Write HTML text to a file
                    string tempFile = url.Replace("://", "_");
                    tempFile = tempFile.Replace(".", "_");
                    tempFile = tempFile.Replace(":", "_"); // illegal character in file names
                    File.WriteAllText(tempFile + ".html", responseFromServer);

                    //Generate HTML for report
                    string filename = String.Format("{0}.jpeg", tempFile);
                    string rcode = resp.StatusCode.ToString();
                    reporter.CreateHTML(filename, url, rcode);

                    //return tempFile;
                    ScreenshotFile(tempFile, 1920, 1080);
                } else
                {
                    Console.WriteLine("No HTML File produced.");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Timeout or Error reaching:{0}", url));
                Console.WriteLine("Error: {0}", ex);
            }

        }

        /// <summary>
        /// Loads HTML from the given file, renders the HTML, and then calls
        /// a callback function to screenshot the rendered HTML and save the
        /// resulting image to a file. Note that the HTML file is deleted
        /// after the image file is created.
        /// </summary>
        /// <param name="fileName">Name of file to load, render, and screenshot</param>
        /// <param name="width">Width of screenshot</param>
        /// <param name="height">Height of screenshot</param>
        private void ScreenshotFile( string fileName, int width, int height )
        {
            //Create a thread to load and render the saved HTML file.
            //Actual screenshotting takes place the OnDocumentCompleted callback function
            //that is added in this thread
            var th = new Thread(() =>
            {
                //Set resolution and ignore JavaScript errors in the page
                WebBrowser browser = new WebBrowser();
                browser.ScriptErrorsSuppressed = true;
                browser.AllowNavigation = true;
                browser.ScrollBarsEnabled = false;
                browser.Width = width;
                browser.Height = height;
                browser.Name = fileName;

                //Open the saved HTML file and render it
                string curDir = Directory.GetCurrentDirectory();
                Uri uri = new Uri(String.Format("file:///{0}/{1}.html", curDir, fileName));
                browser.Navigate(uri);

                //Add a callback function for when the site has been fully rendered
                //This callback function is where the actual screenshotting takes place
                browser.DocumentCompleted += OnDocumentCompleted;

                //Forces thread to wait until Application.ExitThread() is called in the 
                //OnDocumentCompleted callback function. This ensures that the WebBrowser
                //object is not destroyed until after it is consumed by the OnDocumentCompleted
                //callback function.
                Application.Run();
            });

            //Wait if we have reached the maximum number of active threads
            while (_threadsActive >= _maxThreads)
            {
                Thread.Sleep(500);
            }
            
            //Updated the number of active threads
            /*lock (_lockObject)
            {
                _threadsActive += 1;
            }*/
            Interlocked.Increment(ref _threadsActive); //equivalent to above but more efficient, according to MSDOCS

            //Set to Single Threaded Application (STA) to all for the threading to work correctly
            th.SetApartmentState(ApartmentState.STA);

            //Start the latest thread
            th.Start();
        }

        /// <summary>
        /// Callback function for WebBrowser DocumentCompleted event. Once the local HTML file has been
        /// loaded and rendered, this function will be called to take and save a screenshot of the rendered
        /// site. Note that the HTML file is deleted after the image file is created.
        /// </summary>
        /// <param name="sender">Object that generated the event that called this callback function</param>
        /// <param name="e">Additional arguments passed to the callback function</param>
        void OnDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            //Get WebBrowser object, create a new bitmap object, set resolution,
            //set filetype (JPEG), and save the file.
            WebBrowser browser = (WebBrowser)sender;
            using (Graphics graphics = browser.CreateGraphics())
            using (Bitmap bitmap = new Bitmap(browser.Width, browser.Height, graphics))
            {
                Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                browser.DrawToBitmap(bitmap, bounds);

                /*Bitmap resized = new Bitmap(bitmap, new Size(bitmap.Width, bitmap.Height));
                String filename = String.Format("{0}.jpeg", browser.Name);
                resized.Save(filename, ImageFormat.Jpeg);*/
                Image img = (Image)bitmap.Clone();
                String filename = String.Format("{0}.jpeg", browser.Name);
                using (var stream = new MemoryStream())
                {
                    img.Save(stream, ImageFormat.Jpeg);
                    var bytes = stream.ToArray();
                    File.WriteAllBytes(filename, bytes);
                }

            }

            //Delete the temporary HTML file that was created in the ScreenShot method
            string curDir = Directory.GetCurrentDirectory();
            File.Delete(String.Format("{0}/{1}.html", curDir, browser.Name));

            //Decrement the number of active threads
            /*lock (_lockObject)
            {
                _threadsActive -= 1;
            }*/
            Interlocked.Decrement(ref _threadsActive); //equivalent to above but more efficient, according to MSDOCS
            //Allow the thread to exit and the objects to be destroyed
            Application.ExitThread();
        }
 
    }
}
