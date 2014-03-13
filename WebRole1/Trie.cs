using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Trie
    {
        TrieNode root;
        public Trie()
        {
            root = new TrieNode('*', null);
        }

        public void Add(string word)
        {
            root.AddWord('*' + word);
        }

        public List<string> Search(string query)
        {
            List<string> dict = null;
            if (root.ExistsPath('*' + query))
            {
                dict = new List<string>();
                root.Collect(dict, '*' + query, '*' + query);
            }
            return dict;
        }
    }
}