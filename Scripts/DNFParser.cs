using System.Collections.Generic;
using UnityEngine;

public class DNFParser : MonoBehaviour
{
    // entry point
    public static List<List<string>> Parse(string expr)
    {
        expr = expr.Trim();
        return ParseExpression(expr);
    }

    // recursively parse expression
    private static List<List<string>> ParseExpression(string expr)
    {
        List<List<string>> result = new();

        // split top-level ORs
        var orGroups = SplitTopLevel(expr, "OR");
        foreach (var group in orGroups)
        {
            // split top-level ANDs
            var andParts = SplitTopLevel(group, "AND");

            // parse each AND part recursively
            List<List<string>> current = new() { new List<string>() };

            foreach (var part in andParts)
            {
                string trimmed = part.Trim();

                // parenthesis → recurse
                if (trimmed.StartsWith("(") && trimmed.EndsWith(")"))
                {
                    var inner = ParseExpression(trimmed.Substring(1, trimmed.Length - 2));
                    current = CartesianProduct(current, inner);
                }
                else
                {
                    // simple requirement
                    foreach (var list in current)
                        list.Add(trimmed);
                }
            }

            result.AddRange(current);
        }

        return result;
    }

    // splits a string by the operator but only at top-level (ignoring parentheses)
    private static List<string> SplitTopLevel(string input, string op)
    {
        List<string> result = new();
        int paren = 0;
        int lastSplit = 0;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '(') paren++;
            else if (c == ')') paren--;
            else
            {
                if (paren == 0)
                {
                    // check if operator matches
                    if (input.Substring(i).StartsWith(op, System.StringComparison.OrdinalIgnoreCase)) // works with both upper and lowecase
                    {
                        string part = input.Substring(lastSplit, i - lastSplit);
                        if (!string.IsNullOrWhiteSpace(part))
                            result.Add(part.Trim());
                        i += op.Length - 1;
                        lastSplit = i + 1;
                    }
                }
            }
        }

        string last = input.Substring(lastSplit);
        if (!string.IsNullOrWhiteSpace(last))
            result.Add(last.Trim());

        return result;
    }

    // cartesian product of outer ANDs
    private static List<List<string>> CartesianProduct(List<List<string>> a, List<List<string>> b)
    {
        List<List<string>> result = new();
        foreach (var listA in a)
        {
            foreach (var listB in b)
            {
                var combined = new List<string>(listA);
                combined.AddRange(listB);
                result.Add(combined);
            }
        }
        return result;
    }
}