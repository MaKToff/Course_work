/*
 * Graph-based data structure.
 * 
 * Author: Mikhail Kita, 371
 */

namespace Course_work
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Realization of graph-based data structure.
    /// </summary>
    internal class Groum
    {
        private List<GroumNode> Nodes { get; } = new List<GroumNode>();
        private Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        private List<string> Keywords { get; } = new List<string>();
        private Queue<string> ControlQueue { get; } = new Queue<string>();

        private List<List<string[]>> Dataset { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="sequence">Input API usage sequence.</param>
        /// <param name="dataset"></param>
        public Groum(List<string> sequence, List<List<string[]>> dataset)
        {
            using (var reader = new StreamReader("../../keywords.txt", true))
            {
                while (!reader.EndOfStream)
                {
                    Keywords.Add(reader.ReadLine());
                }
            }

            Dataset = dataset;
            SortElements(sequence);
        }

        /// <summary>
        /// Generates code snippet.
        /// </summary>
        /// <returns>String with source code.</returns>
        public string GenerateCode()
        {
            string result = string.Empty;

            foreach (GroumNode node in Nodes)
            {
                result += CreateLinesForNode(node);
            }

            while (ControlQueue.Count > 0)
            {
                ControlQueue.Dequeue();
                result += GenerateTabs(ControlQueue.Count) + "}\n\n";
            }

            result = result.Trim('\n');

            return result;
        }

        /// <summary>
        /// Sorts given sequence.
        /// </summary>
        /// <param name="sequence">Input API usage sequence.</param>
        private void SortElements(List<string> sequence)
        {
            List<string> list = new List<string>();

            foreach (string item in sequence)
            {
                list.Add(RemoveType(item));
            }

            while (sequence.Count > 0)
            {
                var result = FindBestElement(list);
                int pos = list.IndexOf(result.Key);

                Nodes.Add(new GroumNode(sequence[pos], result.Value));
                sequence.RemoveAt(pos);
                list.RemoveAt(pos);
            }
        }

        /// <summary>
        /// Removes type of API usage.
        /// </summary>
        /// <param name="usage">API usage.</param>
        /// <returns>Body of usage.</returns>
        private string RemoveType(string usage)
        {
            if (usage.Contains("<") && usage.Contains(">"))
            {
                int index = usage.IndexOf('<');
                usage = usage.Remove(index, usage.LastIndexOf('>') - index + 1);
            }

            return usage;
        }

        /// <summary>
        /// Finds best element of API sequence to add into Groum.
        /// </summary>
        /// <param name="list">List of API usages.</param>
        /// <returns>Found element and its arguments.</returns>
        private KeyValuePair<string, string> FindBestElement(List<string> list)
        {
            Dictionary<string, float> scores = new Dictionary<string, float>();
            Dictionary<string, int> argScores = new Dictionary<string, int>();
            List<List<string[]>> subset = new List<List<string[]>>();

            foreach (string item in list)
            {
                if (!scores.ContainsKey(item))
                {
                    scores.Add(item, 0f);
                }
            }

            string[] arguments = { string.Empty, "#if", "#while", "#for", "#foreach" };

            foreach (var arg in arguments)
            {
                argScores.Add(arg, 0);
            }

            foreach (var sequence in Dataset)
            {
                float score = 0f;
                string firstElement = string.Empty;

                foreach (string[] element in sequence)
                {
                    if (list.Contains(element[element.Length - 1]))
                    {
                        ++score;

                        if (firstElement == string.Empty)
                        {
                            firstElement = element[element.Length - 1];
                        }
                    }
                }

                score /= sequence.Count;

                if (firstElement != string.Empty)
                {
                    scores[firstElement] += score;
                    subset.Add(sequence);
                }
            }

            float bestScore = -1f;
            string result = string.Empty;

            foreach (var score in scores)
            {
                if (score.Value > bestScore)
                {
                    bestScore = score.Value;
                    result = score.Key;
                }
            }

            Dataset = subset;

            int counter = 0;
            int argsScore = 0;

            foreach (var sequence in Dataset)
            {
                foreach (string[] element in sequence)
                {
                    if (element[element.Length - 1] == result)
                    {
                        ++counter;

                        foreach (var item in element)
                        {
                            if (item == "#args")
                            {
                                ++argsScore;
                            }
                            else if (item.StartsWith("#"))
                            {
                                ++argScores[item];
                            }
                        }
                    }
                }
            }

            string args = argsScore > counter / 2 ? "#args " : string.Empty;
            bestScore = 0f;
            string value = string.Empty;

            foreach (var pair in argScores)
            {
                if (pair.Value > bestScore)
                {
                    bestScore = pair.Value;
                    value = pair.Key;
                }
            }

            args += value;
            args = args.Trim();

            return new KeyValuePair<string, string>(result, args);
        }

        /// <summary>
        /// Generates string with specified number of tabs.
        /// </summary>
        /// <param name="number">Number of tabs.</param>
        /// <returns>Output string.</returns>
        private string GenerateTabs(int number)
        {
            string result = string.Empty;

            for (int i = 0; i < number; ++i)
            {
                result += "\t";
            }

            return result;
        }

        /// <summary>
        /// Removes incorrect characters in given string.
        /// </summary>
        /// <param name="str">Input string.</param>
        /// <returns>Correct string.</returns>
        private string RemoveIncorrectCharacters(string str)
        {
            string incorrect = "<>{}()[]\"\'?!";
            string result = str;

            foreach (char symbol in incorrect)
            {
                if (result.Contains(symbol))
                {
                    result = result.Remove(result.IndexOf(symbol));
                }
            }

            return result;
        }

        /// <summary>
        /// Creates lines of code for given node.
        /// </summary>
        /// <param name="node">Current node.</param>
        /// <returns>String with generated lines of source code.</returns>
        private string CreateLinesForNode(GroumNode node)
        {
            string result = string.Empty;
            string args = node.Args.Contains("#args") ? "(...)" : "()";

            string[] splitted = node.Args.Split(' ');
            string control = splitted[splitted.Length - 1];

            if (ControlQueue.Count > 0 && ControlQueue.Contains(control))
            {
                while (ControlQueue.Peek() != control)
                {
                    ControlQueue.Dequeue();
                    result += GenerateTabs(ControlQueue.Count) + "}\n\n";
                }
            }
            else
            {
                if (control.StartsWith("#") && control != "#args")
                {
                    string tabs = GenerateTabs(ControlQueue.Count);
                    string condition = string.Empty;

                    switch (control)
                    {
                        case "#if":
                            condition = " (...)";
                            break;
                        case "#while":
                            condition = " (true)";
                            break;
                        case "#for":
                            condition = " (;;)";
                            break;
                        case "#foreach":
                            condition = " (var item in COLLECTION)";
                            break;
                    }

                    result += "\n" + tabs + control.Remove(0, 1) + condition + "\n" + tabs + "{\n";
                    ControlQueue.Enqueue(control);
                }
                else
                {
                    while (ControlQueue.Count > 0)
                    {
                        ControlQueue.Dequeue();
                        result += GenerateTabs(ControlQueue.Count) + "}\n";
                    }

                    if (result != string.Empty)
                    {
                        result += "\n";
                    }
                }
            }

            result += GenerateTabs(ControlQueue.Count);

            if (node.Value.Contains("."))
            {
                string[] array = node.Value.Split('.');
                int n = array.Length;
                string name = array[n - 2];

                if (array[n - 1] == "new")
                {
                    name = RemoveIncorrectCharacters(name);
                    name = char.ToLower(name[0]) + name.Substring(1);

                    if (Keywords.Contains(name))
                    {
                        name = "var" + char.ToUpper(name[0]) + name.Substring(1);
                    }

                    if (Variables.ContainsKey(name))
                    {
                        int i = 1;

                        while (Variables.ContainsKey(name + i))
                        {
                            ++i;
                        }

                        name = name + i;
                    }

                    result += array[n - 2] + " " + name + " = new " + array[n - 2] + args + ";\n";
                    Variables.Add(name, array[n - 2]);
                }
                else
                {
                    foreach (var variable in Variables)
                    {
                        if (variable.Value == array[n - 2])
                        {
                            name = variable.Key;
                        }
                    }

                    result += name + "." + array[n - 1] + args + ";\n";
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Realization of groum node.
    /// </summary>
    internal class GroumNode
    {
        public GroumNode(string value, string args)
        {
            Value = value;
            Args = args;
        }

        public string Value { get; }
        public string Args { get; }
    }
}