﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CraigsListExtract
{
    internal class Program
    {
        private static string errorLogFile = String.Concat(AppDomain.CurrentDomain.BaseDirectory, "CraigsListExtractErrorLog.txt");

        private static void Main(string[] args)
        {
            string message = "";
            try
            {
                if ((args.Length == 1) && (args[0] != "-sites"))
                {
                    message = "Valid command is -sites. README.md has more detail.";
                }

                if ((args.Length == 1) && (args[0] == "-sites") && (message == ""))
                {
                    GetListOfSites();
                    message = "Process complete...";
                }

                if (message == "")
                {
                    var appDirectory = AppDomain.CurrentDomain.BaseDirectory;

                    if (File.Exists(appDirectory + "files\\UrlsToExtract.txt"))
                    {
                        string[] urls = File.ReadAllLines(appDirectory + "files\\UrlsToExtract.txt");
                        foreach (string line in urls)
                        {
                            try
                            {
                                if ((line.Trim() != String.Empty) && (line.Substring(0, 3) != "REM"))
                                {
                                    string[] commands = line.Split(' ');

                                    if (commands.Length != 6)
                                    {
                                        Console.WriteLine(String.Concat("Invalid parameters: ", line));
                                    }
                                    else
                                    {
                                        GetRss(commands[1], commands[3], Convert.ToInt32(commands[5]), appDirectory);
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(String.Concat("Invalid extract url", line));
                                File.WriteAllText(errorLogFile, String.Concat("Invalid extract url", line, "\r\n", e.Message, "\r\n", e.StackTrace));
                            }
                        }

                        message = "Process complete...";
                    }
                    else
                    {
                        message = "UrlsToExtract.txt is missing. Please create...";
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                File.WriteAllText(errorLogFile, String.Concat(e.Message, " - ", e.StackTrace));
            }

            Console.WriteLine(message);
        }

        private static void GetListOfSites()
        {
            string url, webPage;
            File.Delete(AppDomain.CurrentDomain.BaseDirectory + "SiteExtract.txt");
            var sbUrls = new StringBuilder();

            try
            {
                url = "http://geo.craigslist.org/iso/us/";
                string pattern = @"<a.*?>.*?</a>";

                WebRequest webRequest;
                webRequest = WebRequest.Create(url);

                StreamReader responseReader = new StreamReader(webRequest.GetResponse().GetResponseStream());
                webPage = responseReader.ReadToEnd();
                responseReader.Close();

                if (webPage.Contains("choose the site"))
                {
                    Regex linksExpression = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                    MatchCollection matches = linksExpression.Matches(webPage);

                    foreach (Match match in matches)
                    {
                        string href;
                        string temp = match.Groups[0].Value;

                        Match m2 = Regex.Match(temp, @"href=\""(.*?)\""", RegexOptions.Singleline);

                        if (m2.Success)
                        {
                            href = m2.Groups[1].Value;
                            if (
                                (href.Contains("craigslist") &&
                                (href.Contains("www") == false) &&
                                (href.Contains("geo") == false) &&
                                (href.Contains("forums") == false))

                                )
                            {
                                if (href.LastIndexOf("/") > href.LastIndexOf(".org"))
                                {
                                    href = href.Substring(0, href.IndexOf(".org") + 4); //remove trailing slash and/or regional designation
                                }
                                sbUrls.AppendLine(href);
                                Console.WriteLine(href);
                            }
                        }
                    }
                    File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "SiteExtract.txt", sbUrls.ToString());
                }             
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                File.WriteAllText(errorLogFile, e.StackTrace);
            }
        }

        private static void GetRss(string queryString, string resultFile, int daysToSearch, string appDirectory)
        {
            string[] excludePhrases = new string[] { "" };
            List<Rss> rssItems = new List<Rss>();
            StringBuilder htmlItems = new StringBuilder();

            string title;
            string url;
            string domain;
            string dupePost;
            List<string> titles = new List<string>();
            string[] urlsToExclude = new string[] { "" };
            bool exclude = false;

            XNamespace rss = "http://purl.org/rss/1.0/";
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            try
            {
                if (!resultFile.Contains("\\"))
                {
                    resultFile = string.Concat(appDirectory, resultFile);
                }

                if (File.Exists(resultFile))
                {
                    File.Delete(resultFile);
                }

                StreamReader streamReader;

                if (File.Exists(appDirectory + "files\\UrlsToExclude.txt"))
                {
                    urlsToExclude = File.ReadAllLines(appDirectory + "files\\UrlsToExclude.txt");
                }

                if (File.Exists(appDirectory + "files\\PhrasesToExclude.txt"))
                {
                    excludePhrases = File.ReadAllLines(appDirectory + "files\\PhrasesToExclude.txt");
                }

                if (!File.Exists(appDirectory + "files\\CraigsListExtract.txt"))
                {
                    Console.WriteLine("CraigsListExtract.txt is missing. Please create.");
                    Console.ReadLine();
                    return;
                }

                streamReader = File.OpenText(appDirectory + "files\\CraigsListExtract.txt");
                domain = streamReader.ReadLine();

                while (domain != null)
                {
                    if (domain.IndexOf("REM") == -1)
                    {
                        domain = domain.Replace(" ", "");
                        Console.WriteLine(domain);
                        url = string.Concat(domain, queryString, "&format=rss");

                        try
                        {
                            XDocument feedXml = XDocument.Load(url);

                            var feeds = from feed in feedXml.Descendants(rss + "item")
                                        select new Rss
                                        {
                                            Title = feed.Element(rss + "title").Value,
                                            Link = feed.Element(rss + "link").Value,
                                            Description = feed.Element(rss + "description").Value,
                                            PubDate = feed.Element(dc + "date").Value
                                        };

                            foreach (Rss item in feeds)
                            {
                                exclude = urlsToExclude.Contains(item.Link.ToString());

                                if (exclude == false)
                                {
                                    foreach (string piece in excludePhrases)
                                    {
                                        if ((item.Title.ToLower().Contains(piece.ToLower())) || (item.Description.ToLower().Contains(piece.ToLower())))
                                        {
                                            exclude = true;
                                        }
                                    }
                                }

                                if (exclude == false)
                                {
                                    if (item.Title.IndexOf("(") == -1)
                                    {
                                        title = item.Title;
                                    }
                                    else
                                    {
                                        title = item.Title.Substring(0, item.Title.IndexOf("("));
                                    }

                                    dupePost = string.Concat(item.Title, item.Description.Substring(1, 30));

                                    if (!titles.Contains(dupePost))
                                    {
                                        if (item.PubDate == "") //rss output sometimes does not output date
                                        {
                                            titles.Add(dupePost);
                                            rssItems.Add(item);
                                        }
                                        else if ((Convert.ToDateTime(item.PubDate) >= DateTime.Now.AddDays(daysToSearch * -1)))
                                        {
                                            titles.Add(dupePost);
                                            rssItems.Add(item);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("No match for: {0}", url);
                        }
                    }

                    domain = streamReader.ReadLine();
                }
                streamReader.Close();

                var sortedList = from p in rssItems
                                 orderby p.PubDate descending
                                 select p;

                foreach (Rss item in sortedList)
                {
                    htmlItems.Append(string.Concat(
                             "<table>",
                             "<tr><td valign=\"top\">Title</td><td>", item.Title, "</td></tr>",
                             "<tr><td valign=\"top\">Date</td><td>", item.PubDate, "</td></tr>",
                             "<tr><td valign=\"top\">Link</td><td><a target=\"_blank\" href=\"", item.Link, "\">", item.Link, "</a></td></tr>",
                             "<tr><td valign=\"top\">Description</td><td>", item.Description, "</td></tr>",
                             "</table><hr>"));
                }

                File.WriteAllText(resultFile, htmlItems.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                File.WriteAllText(errorLogFile, e.StackTrace);
            }
        }
    }

    public class Rss
    {
        public string Link { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string PubDate { get; set; }
    }
}