using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace CompanionRoom.UI
{
    /// <summary>
    /// Line breaking for Chinese text, which uGUI's wrapping handles poorly: closing punctuation
    /// (，。、！？」』…) never starts a line — it hangs at the end of the previous one — opening
    /// brackets never end a line, and runs of Latin letters/digits stay together. The result has
    /// explicit line breaks, so typing it out never makes a word jump to the next line.
    /// </summary>
    public static class TextWrap
    {
        const string NoLineStart = "，。、．,.!?！？：；:;）)」』】〉》…‥～~ー・%％";
        const string NoLineEnd = "（(「『【〈《";

        /// <summary>Sets the text, pre-wrapped to <paramref name="width"/> (default: the rect's width).</summary>
        public static void SetWrapped(this Text text, string value, float width = -1f)
        {
            if (width <= 0f)
                width = text.rectTransform.rect.width;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = Wrap(value, text.font, text.fontSize, text.fontStyle, width);
        }

        public static string Wrap(string value, Font font, int size, FontStyle style, float width)
        {
            if (string.IsNullOrEmpty(value) || font == null || width <= 0f)
                return value ?? "";
            font.RequestCharactersInTexture(value, size, style);
            var output = new StringBuilder(value.Length + 8);
            var paragraphs = value.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    output.Append('\n');
                WrapParagraph(paragraphs[p], font, size, style, width, output);
            }
            return output.ToString();
        }

        static float Advance(Font font, char c, int size, FontStyle style) =>
            font.GetCharacterInfo(c, out var info, size, style) ? info.advance : (c < 128 ? size * 0.5f : size);

        static bool IsWordChar(char c) => c < 128 && char.IsLetterOrDigit(c);

        static void WrapParagraph(string text, Font font, int size, FontStyle style, float width, StringBuilder output)
        {
            var line = new StringBuilder();
            float lineWidth = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                float advance = Advance(font, c, size, style);
                bool overflows = lineWidth + advance > width && line.Length > 0;
                // Closing punctuation hangs past the edge instead of opening a new line.
                if (overflows && NoLineStart.IndexOf(c) < 0)
                {
                    int carry = 0;
                    // Keep Latin words whole.
                    if (IsWordChar(c))
                        while (carry < line.Length - 1 && IsWordChar(line[line.Length - 1 - carry]))
                            carry++;
                    // Don't leave an opening bracket at the end of the line.
                    while (carry < line.Length - 1 && NoLineEnd.IndexOf(line[line.Length - 1 - carry]) >= 0)
                        carry++;
                    var moved = line.ToString(line.Length - carry, carry);
                    line.Length -= carry;
                    output.Append(line.ToString().TrimEnd(' ')).Append('\n');
                    line.Clear();
                    line.Append(moved.TrimStart(' '));
                    lineWidth = 0f;
                    foreach (var m in line.ToString())
                        lineWidth += Advance(font, m, size, style);
                    if (c == ' ' && line.Length == 0)
                        continue;
                }
                line.Append(c);
                lineWidth += advance;
            }
            output.Append(line);
        }
    }
}
