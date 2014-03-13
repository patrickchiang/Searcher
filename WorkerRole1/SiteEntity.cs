﻿using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    public class SiteEntity : TableEntity
    {
        public SiteEntity()
        {
        }

        public SiteEntity(string word) 
        { 
            // store time info in row key for easy reverse retrieval
            string date = string.Format("{0:d19}", DateTime.MaxValue.Ticks - DateTime.UtcNow.Ticks);
            RowKey = date + Guid.NewGuid();
            PartitionKey = word.ToLower();
        }

        public string URL { get; set; }

        public string Title { get; set; }

        public string Date { get; set; }

        public string Headline { get; set; }
    }
}
