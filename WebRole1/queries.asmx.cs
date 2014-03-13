using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.Script.Services;
using System.Web.Services;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;
using CrawlerWorker;
using Microsoft.WindowsAzure.Storage.Queue;
using System.Threading;

namespace WebRole1
{
    [WebService(Namespace = "http://info344crawler.cloudapp.net/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class queries : System.Web.Services.WebService
    {
        static Trie trie;
        static Boolean buildLock = false;
        static Dictionary<string, string> cache = new Dictionary<string, string>(); // Dictionary is a HashTable

        static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
        // Connect to the table client.
        static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
        static CloudTable table = tableClient.GetTableReference("sites");

        static CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
        static CloudQueue answers = queueClient.GetQueueReference("answers");

        static int trieSize = 0;
        static string lastTitle = "";

        static Thread sender = new Thread(new ThreadStart(SendDictionaryStatus));

        static string guid = "";

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public void query(string data)
        {
            if (trie == null)
            {
                HttpContext.Current.Response.Write("Term not found.");
                if (!buildLock)
                {
                    buildDictionary();
                }
            }
            else
            {
                if (buildLock)
                {
                    HttpContext.Current.Response.Write("Term not found.");
                    return;
                }

                // Retrieve from cache if possible
                if (cache.ContainsKey(data))
                {
                    HttpContext.Current.Response.Write(cache[data]);
                    return;
                }

                List<string> results = trie.Search(data);
                if (results == null || results.Count == 0)
                {
                    HttpContext.Current.Response.Write("Term not found.");
                }
                else
                {
                    for (int i = 0; i < results.Count; i++)
                    {
                        results[i] = results[i].Substring(1);
                    }

                    Dictionary<string, string> title = new Dictionary<string, string>();

                    foreach (string keyword in results)
                    {
                        if (table.Exists())
                        {
                            TableQuery<SiteEntity> query = new TableQuery<SiteEntity>()
                               .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, keyword.ToLower()));

                            foreach (SiteEntity e in table.ExecuteQuery(query))
                            {
                                // Limit to 30 results
                                if (!title.ContainsKey(Base64Decode(e.URL)) && title.Count <= 30)
                                    title.Add(Base64Decode(e.URL), Base64Decode(e.Title));
                            }
                        }
                    }

                    // Add answer to cache
                    var result = new JavaScriptSerializer().Serialize(title);
                    cache.Add(data, result);

                    HttpContext.Current.Response.Write(result);
                }
            }
        }

        public static string Base64Decode(string base64EncodedData)
        {
            try
            {
                var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
                return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
            }
            catch (Exception ex)
            {
                return "";
            }
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public void buildDictionary()
        {
            buildLock = true;
            trie = new Trie();

            if (!sender.IsAlive)
            {
                sender.Start();
            }

            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            if (table.Exists())
            {
                TableQuery<IndexIdentity> query = new TableQuery<IndexIdentity>()
                   .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "indexingforsearch"));

                string title = "Done building dictionary";

                foreach (IndexIdentity e in table.ExecuteQuery(query))
                {
                    if (ramCounter.NextValue() >= 40)
                    {
                        string line = e.RowKey;
                        trie.Add(line);
                        trieSize++;
                        lastTitle = line;
                    }
                }

                HttpContext.Current.Response.Write(title);
            }

            buildLock = false;
        }

        public static void SendDictionaryStatus()
        {
            while(true)
            {
                Thread.Sleep(1000);

                List<string> answerMsg = new List<string>();
                answerMsg.Add(trieSize + "");
                answerMsg.Add(lastTitle + "");
                string json = string.Join(",", answerMsg.ToArray());

                CloudQueueMessage cqAnswer = new CloudQueueMessage("D" + json);
                answers.AddMessage(cqAnswer);
            }
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public void ClearCache()
        {
            cache.Clear();
        }

        [WebMethod]
        [ScriptMethod(UseHttpGet = true, ResponseFormat = ResponseFormat.Json)]
        public void GetAnswers()
        {
            // Insist on an answer
            while (answers.PeekMessage() == null)
            {
                Thread.Sleep(100);
            }

            // Get the answer
            CloudQueueMessage cloudAnswer = answers.GetMessage();
            string cloudAnswerString = "";
            if (cloudAnswer != null)
            {
                cloudAnswerString = cloudAnswer.AsString;
                answers.DeleteMessage(cloudAnswer);
            }

            if (cloudAnswerString != "")
            {
                if (cloudAnswerString.StartsWith("M"))
                {
                    string[] answerJson = cloudAnswerString.Substring(1).Split(new string[] { "," }, StringSplitOptions.None);

                    guid = answerJson[3];
                    table = tableClient.GetTableReference("sites" + guid);
                }
                else if (cloudAnswerString.StartsWith("Done mapping"))
                {
                    // restore the message if not relevant to current page
                    CloudQueueMessage doneMapping = new CloudQueueMessage("Done mapping bro");
                    answers.AddMessage(doneMapping);
                }
            }
        }
    }
}
