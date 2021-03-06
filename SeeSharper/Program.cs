﻿/*
 * File: Program.cs
 * Author: Brian Fehrman (fullmetalcache)
 * Date: 2018-12-27
 * Description:
 *      This is the main entry point for the SeeSharper program. 
 */

using CommandLine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;

namespace SeeSharper
{
    class Program
    {
        enum FileType { Nessus, HostFile };
        public static int taken = 0; //Tally up total of screenshots taken

        static async Task Main(string[] args)
        {
            int threads = 1;
            int timeout = 30;
            string fileName = "";
            FileType fileType = FileType.HostFile;
            bool appendPorts = false;
            bool prependHttps = false;
            List<string> hostList = null;
             
            //Parse command line arguments
            ParserResult<Options> argResults = Parser.Default.ParseArguments<Options>(args);
            ParserResult<Options> res = argResults.WithParsed<Options>(o =>
                {
                    appendPorts = o.AppendPorts;
                    prependHttps = o.PrependHTTPS;
                    threads = o.NumThreads;
                    timeout = o.Timeout;

                    if( o.FileName == "" )
                    {
                        System.Console.WriteLine("Please provide the name of a host file or a Nessus file");
                        System.Environment.Exit(1);
                    }

                    fileName = o.FileName;

                    //Automatically determine file type
                    try
                    {
                        StreamReader file = new StreamReader(fileName);
                        string line = file.ReadLine();

                        if (line != null)
                        {
                            if (line.Contains("xml version"))
                            {
                                fileType = FileType.Nessus;
                            }
                            else
                            {
                                fileType = FileType.HostFile;
                            }
                        }

                        file.Close();
                    }
                    catch
                    {
                        System.Console.WriteLine(string.Format("Could not open file: {0}", fileName));
                        System.Environment.Exit(1);
                    }

                });

            //This takes care of if the --help option was used
            if( argResults.Tag == ParserResultType.NotParsed )
            {
                System.Environment.Exit(1);
            }

            //Get hosts
            if(fileType == FileType.HostFile)
            {
                FileParser fParser = new FileParser();
                hostList = fParser.Parse(fileName, prependHttps, appendPorts);
            }
            else if(fileType == FileType.Nessus)
            {
                NessusParser nParser = new NessusParser();
                hostList = nParser.Parse(fileName);
            }

            if (hostList == null)
            {
                System.Environment.Exit(1);
            }

            int total = 0; //completion method condition
            foreach (string host in hostList)
            {
                System.Console.WriteLine(host);
                total += 1;
            }

            //Remove previous report if it exist
            if (File.Exists("SeeSharpestReport.html"))
            {
                File.Delete("SeeSharpestReport.html");
            }

            //Create a new WebShot object for screenshotting.
            WebShot webshot = new WebShot(threads, timeout);

            System.Console.WriteLine("Beginning to take screenshots...");
            //Take a screenshot of each host.
            string safeHost; //Used to replace host missing http 
            foreach (string host in hostList)
            {
                safeHost = host;
                if (!safeHost.StartsWith("http"))
                {
                    safeHost = "http://" + safeHost;
                }
                await webshot.ScreenShot(safeHost);
            }

            while (Program.taken < total) // wait for all screenshots to be taken
            {
                System.Threading.Thread.Sleep(500);
            }

            Reporter.FinalizeReport();
            webshot._webClient.Dispose(); // clean up for HTTPclient object

            System.Console.WriteLine("Screenshots completed! Total taken: {0}", Program.taken);
        }
    }

    //Class for command line arguments
    public class Options
    {
        [Option("appendports",
            Default = false,
            HelpText = "Appends ports specified in PortList.txt to each host that is provided; handles if port is already present")]
        public bool AppendPorts { get; set; }

        [Option('f', "file",
            Default = "",
            HelpText = "Path/Filename of hosts to screenshot")]
        public string FileName { get; set; }

        [Option("prependhttps",
            Default = false,
            HelpText = "Prepends HTTP and HTTPS to all addresses; handles if either prefix is already present")]
        public bool PrependHTTPS { get; set; }

        [Option("threads",
            Default = 1,
            HelpText = "Specify number of threads to use")]
        public int NumThreads { get; set; }

        [Option("timeout",
            Default = 30,
            HelpText = "Specify the timeout for web requests, in seconds")]
        public int Timeout { get; set; }
    }
}
