﻿/*
 * File: NessusParser.cs
 * Author: Brian Fehrman (fullmetalcache)
 * Date: 2018-12-27
 * Description:
 *      This class can be used to parse .nessus files for hosts where
 *      Nessus found the presence of a web service. The C# XmlDocument class made
 *      light work of this.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace SeeSharper
{
    class NessusParser
    {
        /// <summary>
        /// Parses a .nessus file and returns a list of endpoints where Nessus
        /// found a web service to be present. This method will return a list of
        /// hosts in the form of http://hostname:port or https://hostname:port,
        /// depending on if Nessus reported the service to be cleartext or
        /// SSL/TLS
        /// </summary>
        /// <param name="fileName">Absolute or Relative Path of Nessus File to Parse</param>
        /// <returns>List of Endpoints with a Web Service Present</returns>
        public List<string> Parse(string fileName)
        {
            List<string> hostList = new List<string>();
            XmlDocument xmlDoc = null;
            XmlNodeList hosts = null;

            //Load the .nessus file
            try
            {
                xmlDoc = new XmlDocument();
                xmlDoc.Load(fileName);
            }
            catch
            {
                Console.WriteLine(String.Format("Error Opening Nessus File: {0}", fileName));
                return null;
            }

            //Get all of the host nodes
            try
            {
                hosts = xmlDoc.GetElementsByTagName("ReportHost");
            }
            catch
            {
                Console.WriteLine(String.Format("File does not appear to be a valid .nessus File: {0}", fileName));
            } 

            //Parse each host node
            foreach (XmlNode node in hosts)
            {
                //Extract the host address
                string hostAddr = node.Attributes["name"].Value;

                //Extract and Iterate through each ReportItem node for the current host
                XmlNodeList reportItems = node.SelectNodes("ReportItem");

                foreach (XmlNode item in reportItems)
                {
                    //See if this ReportItem was generated by the Nessus "Service Detection" plugin
                    if( item.InnerText.Contains("Service Detection"))
                    {
                        //Extract the port on which the web service was running
                        string port = item.Attributes["port"].Value;

                        //Form the endpoint as hostAddr:port
                        string fullHost = String.Format("{0}:{1}", hostAddr, port);

                        //Determine if the service was running under HTTP or HTTPS and prepend
                        //the appropriate string to the endpoint
                        if( item.InnerText.Contains("A web server is running on this port."))
                        {
                            fullHost = String.Format("{0}{1}", "http://", fullHost);
                        }
                        else
                        {
                            fullHost = String.Format("{0}{1}", "https://", fullHost);
                        }

                        //Check if the endpoint is already in the list. If not, add the endpoint
                        var match = hostList.FirstOrDefault(stringToCheck => stringToCheck.Contains(fullHost));
                        if( match == null)
                        {
                            hostList.Add(fullHost);
                        }
                    }
                }
            }

            return hostList;
        }
    }
}
