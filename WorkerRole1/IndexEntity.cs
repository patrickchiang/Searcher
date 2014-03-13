using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    public class IndexIdentity : TableEntity
    {
        public IndexIdentity()
        {
        }

        public IndexIdentity(string word) 
        {
            RowKey = word;
            PartitionKey = "indexingforsearch";
        }
    }
}
