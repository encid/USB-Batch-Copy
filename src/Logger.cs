using System;
using System.Drawing;
using System.Windows.Forms;

namespace USBBatchCopy 
    {
    public static class Logger {

        /// <summary>
        /// Write a message to the status log textbox.
        /// </summary>
        /// <param name="message">Message to write to the status log.</param>
        /// <param name="box">Object to target.</param>
        public static void Log(string message, RichTextBox box)
        {
            if (string.IsNullOrEmpty(box.Text)) {
                box.AppendText(message);
            }
            else
                box.AppendText(Environment.NewLine + message);
        }

        public static void Log(string message, RichTextBox box, Color color)
        {
            if (string.IsNullOrEmpty(box.Text)) {
                box.AppendText(message, color);
            }
            else
                box.AppendText(Environment.NewLine + message, color);
        }
    }

    public static class RichTextBoxExtensions {
        public static void AppendText(this RichTextBox box, string text, Color color)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
        }
    }
}
