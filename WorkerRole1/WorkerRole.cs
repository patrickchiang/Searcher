using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.WindowsAzure.Storage.Table;
using HtmlAgilityPack;
using System.Globalization;

namespace WorkerRole1
{
    public class WorkerRole : RoleEntryPoint
    {
        // urls that have already been acknowledged
        private static HashSet<string> xmls = new HashSet<string>();
        private static HashSet<string> urls = new HashSet<string>();
        private static HashSet<string> disallow = new HashSet<string>();

        static string rootUrl = "";

        // Connect to queue storage
        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
            CloudConfigurationManager.GetSetting("StorageConnectionString"));
        static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

        // Create the table client.
        static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        static CloudTable table;
        static CloudTable errorTable;

        static CloudQueue command = queueClient.GetQueueReference("commands");
        static CloudQueue answers = queueClient.GetQueueReference("answers");

        // Use local queue because cloud queue performance is terrible with 200k inserts.
        static Queue<string> crawlUrl = new Queue<string>();

        // State of crawler
        static Boolean crawlXml = true;
        static Boolean crawlHtml = false;

        static int siteEntries = 0;

        static string guid = Guid.NewGuid().ToString();

        public override void Run()
        {
            // Create answering machine thread to answer frontend queries
            Thread am = new Thread(new ThreadStart(AnsweringMachine));
            am.Start();

            // Build dictionary constantly
            Thread db = new Thread(new ThreadStart(DictionaryBuilder));
            db.Start();

            while (true)
            {
                Thread.Sleep(1000);

                // Check if command state changed
                CloudQueueMessage commandMsg = command.PeekMessage();
                if (commandMsg != null)
                {
                    if (commandMsg.AsString.StartsWith("Start Crawling: "))
                    {
                        // Dequeue, hide, and delete
                        command.DeleteMessage(command.GetMessage());

                        rootUrl = commandMsg.AsString.Split(new string[] { "Start Crawling: " }, StringSplitOptions.None)[1];
                        crawlXml = true;

                        // Create a request for the URL. 
                        WebRequest request = WebRequest.Create("http://" + rootUrl + "/robots.txt");
                        try
                        {
                            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                            // Successful response from robots.txt
                            if (response.StatusCode.ToString().Equals("OK"))
                            {
                                StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                                while (stream.Peek() >= 0)
                                {
                                    string line = stream.ReadLine();
                                    if (line.StartsWith("Sitemap: "))
                                    {
                                        xmls.Add(line.Replace("Sitemap: ", ""));

                                        crawlUrl.Enqueue(line.Replace("Sitemap: ", "").ToLower());
                                    }
                                    if (line.StartsWith("Disallow: "))
                                    {
                                        disallow.Add(line.Replace("Disallow: ", "").ToLower());
                                    }
                                }

                                response.Close();
                                stream.Close();
                            }
                            else
                            {
                                // Bad response
                                AddErrorToTable("robots.txt", "Cannot find site index");
                            }
                        }
                        catch (Exception ex)
                        {
                            AddErrorToTable("robots.txt", "Cannot find site index." + ex.Message);
                        }
                    }
                }

                //CloudQueueMessage urlMsg = queue.GetMessage();
                string urlMsg = "";
                if (crawlUrl.Count > 0)
                {
                    try
                    {
                        urlMsg = crawlUrl.Dequeue();
                    }
                    catch (Exception ex) { }
                }
                while (crawlHtml && crawlUrl.Count >= 1)
                {
                    // Crawl the page from URL
                    crawlPage(urlMsg);

                    try
                    {
                        // Delete message & get new one
                        urlMsg = crawlUrl.Dequeue();
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }

                while (crawlXml && crawlUrl.Count >= 1)
                {
                    crawlSitemap(urlMsg);

                    try
                    {
                        // Delete message & get new one
                        urlMsg = crawlUrl.Dequeue();
                    }
                    catch (Exception ex)
                    {
                        break;
                    }
                }

                AddErrorToTable("Done sitemapping", "now");

                // Done with sitemap crawling, start html crawling
                if (crawlXml)
                {
                    CloudQueueMessage doneMapping = new CloudQueueMessage("Done mapping bro");
                    answers.AddMessage(doneMapping);

                    crawlXml = false;
                    crawlHtml = true;
                    foreach (string tempurl in urls)
                    {
                        crawlUrl.Enqueue(tempurl.ToLower());
                    }
                }
            }
        }

        public void AddErrorToTable(string url, string message)
        {
            ErrorEntity error = new ErrorEntity();
            error.URL = Base64Encode(url);
            error.Message = Base64Encode(message);

            TableOperation insertOperation = TableOperation.Insert(error);
            errorTable.Execute(insertOperation);
        }

        public static string Base64Encode(string plainText)
        {
            try
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        public void DictionaryBuilder()
        {
            
            while(true)
            {
                // Rebuild dictionary and clean cache every 30 seconds
                Thread.Sleep(30000);

                try
                {
                    // Grab the guid
                    WebRequest dictionaryReq = WebRequest.Create("http://info344crawler.cloudapp.net/queries.asmx/GetAnswers");
                    dictionaryReq.Timeout = 10000; // quit after 10 seconds
                    HttpWebResponse response = (HttpWebResponse)dictionaryReq.GetResponse();
                }
                catch (Exception ex) { }

                try
                {
                    // Clear the cache
                    WebRequest cacheReq = WebRequest.Create("http://info344crawler.cloudapp.net/queries.asmx/ClearCache");
                    cacheReq.Timeout = 10000; // quit after 10 seconds
                    HttpWebResponse response2 = (HttpWebResponse)cacheReq.GetResponse();
                }
                catch (Exception ex) { }

                try
                {
                    // Rebuild the dictionary
                    WebRequest dictionaryReq = WebRequest.Create("http://info344crawler.cloudapp.net/queries.asmx/buildDictionary");
                    dictionaryReq.Timeout = 10000; // quit after 10 seconds
                    HttpWebResponse response = (HttpWebResponse)dictionaryReq.GetResponse();
                }
                catch (Exception ex) { }
            }
        }

        public void AnsweringMachine()
        {
            while (true)
            {
                Thread.Sleep(1000);

                CloudQueueMessage commandMsg = command.PeekMessage();
                if (commandMsg != null)
                {
                    if ((crawlHtml || crawlXml) && commandMsg.AsString.StartsWith("Start Crawling"))
                    {
                        command.DeleteMessage(command.GetMessage());
                    }

                    if (commandMsg.AsString.Equals("Stop Crawling"))
                    {
                        // Dequeue, hide, and delete
                        command.DeleteMessage(command.GetMessage());

                        // Gracefully stop crawling
                        crawlHtml = false;
                        crawlXml = false;

                        crawlUrl.Clear();
                        xmls.Clear();
                        urls.Clear();
                        disallow.Clear();

                        table.DeleteIfExists();
                        errorTable.DeleteIfExists();

                        guid = Guid.NewGuid().ToString().Substring(0, 5);

                        table = tableClient.GetTableReference("sites" + guid);
                        errorTable = tableClient.GetTableReference("errors" + guid);

                        table.CreateIfNotExists();
                        errorTable.CreateIfNotExists();

                        siteEntries = 0;
                    }
                }

                List<string> answerMsg = new List<string>();
                answerMsg.Add(crawlUrl.Count + "");
                answerMsg.Add(siteEntries + "");
                answerMsg.Add(urls.Count + "");
                answerMsg.Add(guid);
                string json = string.Join(",", answerMsg.ToArray());

                CloudQueueMessage cmAnswer = new CloudQueueMessage("M" + json);
                answers.AddMessage(cmAnswer, new TimeSpan(0, 0, 5));    // live for 5s
            }
        }

        /// <summary>
        ///     Crawl the sitemap from the worker role, which is better practice than from the webrole
        ///     because crawling is a background task.
        /// </summary>
        public void crawlSitemap(string xml)
        {
            // Create a request for the URL. 
            WebRequest request = WebRequest.Create(xml);
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // Successful response from robots.txt
                if (response.StatusCode.ToString().Equals("OK"))
                {
                    StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                    
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(stream.ReadToEnd());
                    XmlNodeList loc = xmlDoc.GetElementsByTagName("loc");

                    CultureInfo ci = CultureInfo.CreateSpecificCulture("en-US");

                    foreach (XmlNode n in loc)
                    {
                        string temp = n.InnerText;
                        XmlNode timenode = n.ParentNode.NextSibling;

                        if (timenode != null && timenode["lastmod"] != null)
                        {
                            try
                            {
                                var cnntime = DateTime.ParseExact(timenode["lastmod"].InnerText.Substring(0, 10), "yyyy-MM-dd", ci);

                                // More than 90 days old
                                if ((DateTime.Now - cnntime).Days > 90)
                                    continue;
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                        else
                        {
                            continue;
                        }
                        
                        if (temp.Contains(".xml"))
                        {
                            xmls.Add(temp);

                            crawlUrl.Enqueue(temp.ToLower());
                        }
                        else if (temp.Contains(".html"))
                        {
                            urls.Add(temp);
                        }
                    }

                    response.Close();
                    stream.Close();
                }
                else
                {
                    // Bad response
                    AddErrorToTable(xml, "Cannot find page.");
                }
            }
            catch (Exception ex)
            {
                AddErrorToTable(xml, "Page not responsive." + ex.Message);
            }
        }

        public void crawlPage(string url)
        {
            try
            {
                // Create a request for the URL. 
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 5000; // give up after 5 seconds

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                // Successful response from robots.txt
                if (response.StatusCode.ToString().Equals("OK"))
                {
                    StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8);

                    HtmlDocument doc = new HtmlDocument();
                    doc.LoadHtml(stream.ReadToEnd());

                    // Parse date
                    string date = "";
                    HtmlNodeCollection metas = doc.DocumentNode.SelectNodes("//meta");
                    foreach (HtmlNode node in metas)
                    {
                        if (node.GetAttributeValue("http-equiv", "").Equals("last-modified"))
                        {
                            date = node.GetAttributeValue("content", "");
                        }
                    }

                    // Title
                    HtmlNodeCollection titles = doc.DocumentNode.SelectNodes("//title");
                    string title = titles[0].InnerText;

                    // Headline
                    HtmlNodeCollection headlines = doc.DocumentNode.SelectNodes("//h1");
                    string headline = headlines[0].InnerText;

                    string[] words = title.Split(' ');

                    List<string> permutations = new List<string>();
                    for (int i = 0; i < words.Length; i++)
                    {
                        for (int j = i; j < words.Length; j++)
                        {
                            string temp = "";
                            for (int k = i; k <= j; k++)
                            {
                                temp += " " + words[k];
                            }
                            permutations.Add(temp);
                        }
                    }

                    // Insert into index
                    SiteIndex indexer = new SiteIndex();
                    indexer.URL = Base64Encode(url);
                    indexer.Date = Base64Encode(date);
                    indexer.Title = Base64Encode(title);
                    indexer.Headline = Base64Encode(headline);

                    TableOperation indexing = TableOperation.Insert(indexer);
                    table.Execute(indexing);

                    // Find all permutations of the words
                    foreach (string w in permutations)
                    {
                        // Insert into table
                        SiteEntity site = new SiteEntity(w);
                        site.URL = Base64Encode(url);
                        site.Date = Base64Encode(date);
                        site.Title = Base64Encode(title);
                        site.Headline = Base64Encode(headline);

                        TableOperation insertOperation = TableOperation.Insert(site);
                        table.Execute(insertOperation);
                        try
                        {
                            // Entry in index
                            TableOperation indexOperation = TableOperation.Insert(new IndexIdentity(w));
                            table.Execute(indexOperation);
                        }
                        catch (Exception ex)
                        {
                            // key already exists
                        }
                    }

                    // Find links on page
                    HtmlNodeCollection links = doc.DocumentNode.SelectNodes("//a");
                    foreach (HtmlNode node in links)
                    {
                        ParseLink(node.GetAttributeValue("href", ""));
                    }

                    siteEntries++;

                    response.Close();
                    stream.Close();
                }
                else
                {
                    // Bad response
                    AddErrorToTable(url, "Cannot find page.");
                }
            }
            catch (Exception ex)
            {
                AddErrorToTable(url, "Page not responsive" + ex.Message);
            }
        }

        public void ParseLink(string link)
        {
            // Not parsed before
            if (urls.Contains(link.ToLower()) || crawlUrl.Contains(link.ToLower()))
            {
                return;
            }

            // Not disallowed
            foreach (string s in disallow)
            {
                if (link.ToLower().Contains(s.ToLower()))
                {
                    return;
                }
            }

            // Matches link pattern
            Regex absolute = new Regex(@"http:\/\/.*\." + rootUrl + @".*\.html");
            if (absolute.IsMatch(link))
            {
                urls.Add(link.ToLower());
                crawlUrl.Enqueue(link.ToLower());
            }

            Regex relative = new Regex(@"^\/.*.html");
            if (relative.IsMatch(link))
            {
                urls.Add("http://" + rootUrl + link.ToLower());
                crawlUrl.Enqueue("http://" + rootUrl + link.ToLower());
            }
        }

        public override bool OnStart()
        {
            ServicePointManager.DefaultConnectionLimit = 5000;

            // Gracefully stop crawling
            crawlHtml = false;
            crawlXml = false;

            crawlUrl.Clear();
            xmls.Clear();
            urls.Clear();
            disallow.Clear();

            answers.CreateIfNotExists();
            command.CreateIfNotExists();

            answers.Clear();
            command.Clear();

            guid = Guid.NewGuid().ToString().Substring(0, 5);

            table = tableClient.GetTableReference("sites" + guid);
            errorTable = tableClient.GetTableReference("errors" + guid);

            table.CreateIfNotExists();
            errorTable.CreateIfNotExists();

            siteEntries = 0;

            return base.OnStart();
        }
    }
}
