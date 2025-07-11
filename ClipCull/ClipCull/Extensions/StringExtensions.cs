using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ClipCull.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }
        public static bool IsNotNullOrEmpty(this string str)
        {
            return !string.IsNullOrEmpty(str);
        }
        public static bool IsNullOrWhiteSpace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }
        public static string ToValidWindowsFileName(this string str)
        {
            string invalidChars = Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
            string invalidReStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
            return Regex.Replace(str, invalidReStr, "_").Replace(" ", "-");
        }
        /// <summary>
        /// Converts a string to a valid Windows directory name by replacing invalid characters
        /// </summary>
        /// <param name="input">The input string to sanitize</param>
        /// <param name="replacementChar">Character to replace invalid characters with (default: '_')</param>
        /// <returns>A valid Windows directory name</returns>
        public static string ToValidWindowsDirectoryName(this string input, char replacementChar = '_')
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // These characters are invalid in Windows file/directory names
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Replace invalid characters with the replacement character
            string result = input;
            foreach (char invalidChar in invalidChars)
            {
                result = result.Replace(invalidChar, replacementChar);
            }

            // Handle reserved Windows device names (CON, PRN, AUX, NUL, etc.)
            string[] reservedNames = { "CON", "PRN", "AUX", "NUL",
                                      "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                                      "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

            // Check if the name is a reserved name (case-insensitive)
            string filename = Path.GetFileNameWithoutExtension(result);
            foreach (string reservedName in reservedNames)
            {
                if (string.Equals(filename, reservedName, StringComparison.OrdinalIgnoreCase))
                {
                    // Prefix with an underscore to avoid reserved name
                    return "_" + result;
                }
            }

            // Remove trailing periods and spaces which are not allowed at the end
            result = result.TrimEnd('.', ' ');

            // Ensure it's not empty after all replacements
            if (string.IsNullOrEmpty(result))
                return "_";

            return result;
        }
        public static string RemoveNonNumeric(this string input)
        {
            // Use regular expression to remove non-numeric characters
            string pattern = "[^0-9]";
            string replacement = "";
            Regex regex = new Regex(pattern);
            string result = regex.Replace(input, replacement);

            return result;
        }
        public static string RemoveLeadingAndTrailingCommasAndDots(this string input)
        {
            char[] charsToTrim = { ',', '.' };
            return input.Trim().Trim(charsToTrim).Trim();
        }
        public static string RemoveAlternativeFromIconName(this string input)
        {
            input = input.Trim();
            while (input.Contains("$"))
            {
                input = input.Replace("$", "");
            }
            return input.Trim();
        }
        public static bool EqualsIgnoringCase(this string str, string other)
        {
            if (str == null || other == null)
                return str == other; // Return true if both are null

            return string.Equals(str, other, StringComparison.OrdinalIgnoreCase);
        }

        public static bool EqualsIgnoringCaseTrimmed(this string str, string other)
        {
            if (str == null || other == null)
                return str == other; // Return true if both are null

            return string.Equals(str.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsIgnoringCase(this IEnumerable<string> source, string value)
        {
            if (source == null || value == null)
                return false;

            return source.Any(s => s.EqualsIgnoringCase(value));
        }
        public static bool ContainsTrimmedIgnoringCase(this IEnumerable<string> source, string value)
        {
            if (source == null || value == null)
                return false;

            return source.Any(s => s?.Trim().EqualsIgnoringCase(value.Trim()) == true);
        }

    }

    public static class EnumerableExtensions
    {
        // Extension method to find index of a string (case-insensitive)
        public static int IndexOfIgnoringCase(this IEnumerable<string> source, string value)
        {
            int index = 0;

            foreach (var item in source)
            {
                if (string.Equals(item, value, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
                index++;
            }

            return -1; // Return -1 if the value is not found
        }

        public static string NormalizeSpecialCharacters(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Decompose characters into base + diacritics
            string normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            // Manual replacements for characters not decomposed by normalization
            var manualReplacements = new Dictionary<char, char>
        {
            {'Ø', 'O'}, {'ø', 'o'},  // Replace Ø/ø with O/o
            {'Æ', 'A'}, {'æ', 'a'},  // Replace Æ/æ with A/a
            {'Ð', 'D'}, {'ð', 'd'},  // Replace Ð/ð with D/d
            {'Þ', 'P'}, {'þ', 'p'},  // Replace Þ/þ with P/p
            // Add more mappings as needed
        };

            foreach (char c in normalized)
            {
                // Skip combining diacritics (e.g., ¨, ´)
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                // Check if the character is A-Z/a-z
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                    continue;
                }

                // Apply manual replacement if defined
                if (manualReplacements.TryGetValue(c, out char replacement))
                {
                    sb.Append(replacement);
                }
                else
                {
                    // Preserve all other characters (numbers, symbols, etc.)
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        public static string RemoveDiacritics(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // First use normalization to handle common diacritics
            string normalized = text.Normalize(NormalizationForm.FormD);
            StringBuilder result = new StringBuilder();

            foreach (char c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    result.Append(c);
                }
            }

            // Special character mappings not handled by normalization
            string withoutSpecialChars = result.ToString().Normalize(NormalizationForm.FormC);

            // Map specific characters that normalization doesn't handle well
            withoutSpecialChars = withoutSpecialChars
                .Replace('æ', 'a').Replace('Æ', 'A')
                .Replace('ø', 'o').Replace('Ø', 'O')
                .Replace('å', 'a').Replace('Å', 'A')
                .Replace('ð', 'd').Replace('Ð', 'D')
                .Replace("þ", "th").Replace("Þ", "Th")
                .Replace("ß", "ss")
                .Replace("œ", "oe").Replace("Œ", "OE")
                .Replace('ł', 'l').Replace('Ł', 'L');

            return withoutSpecialChars;
        }
    }
}
