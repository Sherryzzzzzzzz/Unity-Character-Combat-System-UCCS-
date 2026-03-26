using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Lightweight formula evaluator supporting references like "Caster.AttackPower" or "Target.Defense",
/// variables like "$Var", and simple arithmetic (+ - * /, parentheses).
/// Safe: does not compile or execute code; uses a simple recursive-descent parser for math.
/// </summary>
public static class FormulaEvaluator
{
    private static readonly Regex AttributePattern = new Regex(@"(Caster|Target)\.(\w+)", RegexOptions.Compiled);
    private static readonly Regex VariablePattern = new Regex(@"\$(\w+)", RegexOptions.Compiled);

    public static float Evaluate(string formula, FormulaContext context)
    {
        if (string.IsNullOrWhiteSpace(formula)) return 0f;

        if (float.TryParse(formula, out var v)) return v;

        try
        {
            // Replace attribute refs
            string expr = AttributePattern.Replace(formula, match =>
            {
                string source = match.Groups[1].Value;
                string attrName = match.Groups[2].Value;

                if (!Enum.TryParse<GameplayAttribute>(attrName, true, out var attr))
                    return "0";

                float? val = null;
                if (source == "Caster" && context?.CasterAttributes != null)
                {
                    var av = context.CasterAttributes.GetAttributeValue(attr);
                    if (av != null) val = av.GetCurrentValue();
                }
                else if (source == "Target" && context?.TargetAttributes != null)
                {
                    var av = context.TargetAttributes.GetAttributeValue(attr);
                    if (av != null) val = av.GetCurrentValue();
                }

                return (val ?? 0f).ToString(System.Globalization.CultureInfo.InvariantCulture);
            });

            // Replace variables
            expr = VariablePattern.Replace(expr, match =>
            {
                string name = match.Groups[1].Value;
                if (context?.Variables != null && context.Variables.TryGetValue(name, out var vv))
                    return vv.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return "0";
            });

            // Special tokens
            if (context != null)
            {
                expr = expr.Replace("StackCount", context.StackCount.ToString());
                expr = expr.Replace("Level", context.Level.ToString());
            }

            return EvaluateExpression(expr);
        }
        catch
        {
            return 0f;
        }
    }

    private static float EvaluateExpression(string expr)
    {
        expr = expr.Replace(" ", "");
        int pos = 0;
        return ParseAddSub(expr, ref pos);
    }

    private static float ParseAddSub(string s, ref int pos)
    {
        float left = ParseMulDiv(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '+' && op != '-') break;
            pos++;
            float right = ParseMulDiv(s, ref pos);
            left = op == '+' ? left + right : left - right;
        }
        return left;
    }

    private static float ParseMulDiv(string s, ref int pos)
    {
        float left = ParseUnary(s, ref pos);
        while (pos < s.Length)
        {
            char op = s[pos];
            if (op != '*' && op != '/') break;
            pos++;
            float right = ParseUnary(s, ref pos);
            if (op == '*') left *= right; else if (right != 0) left /= right;
        }
        return left;
    }

    private static float ParseUnary(string s, ref int pos)
    {
        if (pos < s.Length && s[pos] == '-') { pos++; return -ParsePrimary(s, ref pos); }
        return ParsePrimary(s, ref pos);
    }

    private static float ParsePrimary(string s, ref int pos)
    {
        if (pos < s.Length && s[pos] == '(')
        {
            pos++;
            float v = ParseAddSub(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;
            return v;
        }
        int start = pos;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
        if (start == pos) return 0f;
        var numStr = s.Substring(start, pos - start);
        if (float.TryParse(numStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var num)) return num;
        return 0f;
    }
}

public class FormulaContext
{
    public AttributeSet CasterAttributes { get; set; }
    public AttributeSet TargetAttributes { get; set; }
    public Dictionary<string, float> Variables { get; set; }
    public int StackCount { get; set; } = 1;
    public int Level { get; set; } = 1;
}
