using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrawlerWorker
{
    public class ErrorEntity : TableEntity
    {
        public ErrorEntity() 
        {
            // store time info in row key for easy reverse retrieval
            string date = string.Format("{0:d19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
            RowKey = date + Guid.NewGuid();
            PartitionKey = "Errors";
        }

        public string URL { get; set; }

        public string Message { get; set; }
    }
}
