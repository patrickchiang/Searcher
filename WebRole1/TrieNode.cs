using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class TrieNode
    {
        char c;
        long rank = 0;
        Boolean endpoint = false;
        TrieNode parent;
        List<TrieNode> children;

        public TrieNode(char c, TrieNode parent)
        {
            this.c = c;
            this.parent = parent;
        }

        public long getRank()
        {
            return rank;
        }

        public void Collect(List<string> dict, string word, string check)
        {
            if (dict.Count >= 10)
            {
                return;
            }

            // Traverse trie to find current word
            string full = "";

            TrieNode temp = this;
            while (temp != null)
            {
                full = temp.c + full;
                temp = temp.parent;
            }
            Console.WriteLine(full);

            if (word.Length >= 1 && word[0] != c)
            {
                return;
            }

            if (endpoint)
            {
                if (full.Length >= check.Length)
                    dict.Add(full);
                if (children == null)
                    return;
            }

            if (word == "")
            {
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Collect(dict, "", check);
                }
                return;
            }

            try
            {
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].Collect(dict, word.Substring(1), check);
                }
            }
            catch (Exception ex) { }
        }

        public Boolean ExistsPath(string word)
        {
            if (word.Length == 1)
            {
                return true;
            }

            if (children == null)
            {
                return false;
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].c == word[1])
                {
                    return children[i].ExistsPath(word.Substring(1));
                }
            }

            return false;
        }

        private Boolean HasChildren()
        {
            return children != null;
        }

        public void AddWord(String s)
        {
            // One char string passed
            if (s.Length == 1)
            {
                endpoint = true;
                rank = 1;
                return;
            }

            // Empty child list
            if (!HasChildren())
            {
                children = new List<TrieNode>();
            }

            TrieNode temp = null;

            // Find correct child
            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].c == s[1])
                {
                    temp = children[i];
                }
            }

            // Can't find the child
            if (temp == null)
            {
                temp = new TrieNode(s[1], this);
                children.Add(temp);
            }

            // Recurse
            temp.AddWord(s.Substring(1));
            rank++;
        }
    }
}