using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace USBBatchCopy 
    {
    public class Logger {

        /// <summary>
        /// Write a message to the status log textbox.
        /// </summary>
        /// <param name="message">Message to write to the status log.</param>
        /// <param name="obj">Object to target.</param>
        public static void Log(string message, RichTextBox obj)
        {
            string time = DateTime.Now.ToString("h:mm:sstt");

            string msg = string.Format("{0}: {1}\n", time, message);

            obj.AppendText(msg);
        }
    }
}
