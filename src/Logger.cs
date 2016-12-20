using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;
using System.Management;

namespace USBBatchCopy 
    {
    public class Logger {

        /// <summary>
        /// Write a message to the status log textbox.
        /// </summary>
        /// <param name="message">Message to write to the status log.</param>
        /// <param name="obj">Object to target.</param>
        public static void Log(string message, RichTextBox box)
        {
            box.AppendText(Environment.NewLine + message);
        }

        public static void Log(string message, RichTextBox box, Color color)
        {
            box.AppendText(Environment.NewLine + message, color);
        }
    }

    public static partial class RichTextBoxExtensions {
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
