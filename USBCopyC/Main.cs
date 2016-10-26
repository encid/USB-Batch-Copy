/*
 USB Batch Copy
 Written by R. Cavallaro
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using System.Linq;
using System.Drawing;

namespace WindowsFormsApplication1
{
    public partial class Main : Form
    {
        bool cancelled;
        Dictionary<string, string> dictRemovableDrives = new Dictionary<string, string>();
        List<string> listDrivesToCopy                  = new List<string>();
        string sourceDir;
        
        public Main()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Determines if an invoke is required to perform an action.
        /// </summary>
        /// <param name="a">Action to perform.</param>
        private void ExecuteSecure(Action a)
        // Usage example: ExecuteSecure(() => this.someLabel.Text = "foo");
        {
            if (InvokeRequired)
                BeginInvoke(a);
            else
                a();
        }
        
        /// <summary>
        /// Format a long number into a readable string; i.e. input: 15520 output: "15.52 KB"
        /// </summary>
        /// <param name="bytes">Number of bytes.</param>
        /// <returns></returns>
        private string FormatBytes(long bytes)
        // Format a long number into a readable string; 15520 -> "15.52 KB"
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max        = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }

        /// <summary>
        /// Set CheckState for all items in a CheckedListBox.
        /// </summary>
        /// <param name="list">CheckedListBox to set CheckState on.</param>
        /// <param name="choice">Set CheckState; Checked = True and Unchecked = False.</param>
        private void SetCheckState(CheckedListBox list, bool choice)
        // Check or uncheck all items in CheckedListBox, choice = false for uncheck, choice = true for check
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                list.SetItemChecked(i, choice);
            }
        }
                
        private void Form1_Load(object sender, EventArgs e)
        {
            // Populate listbox with removable drives
            RefreshDrives(lstDrives, dictRemovableDrives);
        }

        /// <summary>
        /// Detects removable drives, adds them to dictionary (dict), then binds dictionary to listbox (clb).
        /// </summary>
        /// <param name="clb">CheckedListBox object to display removable drives.</param>
        /// <param name="dict">Dictionary to bind to CheckedListBox.</param>
        private void RefreshDrives(CheckedListBox clb, Dictionary<string, string> dict)
        // Detects removable drives, adds them to dictionary, binds dictionary to listbox
        {
            dict.Clear();
            clb.DataSource = null;            

            // Get collection of connected drives and query for removable and ready drives
            IEnumerable<DriveInfo> drives = 
                from d in DriveInfo.GetDrives()
                where d.DriveType == DriveType.Removable &&
                      d.IsReady   == true                      
                select d;

            // If no removable drives detected, exit method
            if (!drives.Any()) return;

            // Iterate through collection, add drive name and drive information to dictionary
            foreach (var drive in drives)
            {
                var freeSpace = FormatBytes(drive.TotalFreeSpace);
                var totalSpace = FormatBytes(drive.TotalSize);
                var drvInfo = String.Format("{0} - ( Label: {1}, FileSystem: {2}, Size: {3}, Free: {4} )",
                    drive.Name, drive.VolumeLabel, drive.DriveFormat,
                    totalSpace, freeSpace);
                dict.Add(drive.Name, drvInfo);
            }

            // Bind dictionary as CheckedListBox DataSource
            clb.DataSource = new BindingSource(dictRemovableDrives, null);

            // Set CheckedListBox properties
            clb.DisplayMember = "Value";
            clb.ValueMember = "Key";
        }

        /// <summary>
        /// Add checked items in CheckedListBox to list collection.
        /// </summary>
        /// <param name="clb">CheckedListBox to parse.</param>
        /// <param name="list">List collection to add items to.</param>
        private void PopulateDriveList(CheckedListBox clb, List<string> list)
        {
            // Clear list collection.
            list.Clear();

            // Iterate through KeyValuePairs in CheckedListBox and add each Key to list collection                        
            foreach (KeyValuePair<string, string> pair in clb.CheckedItems)
            {
                list.Add(pair.Key);
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
         // Sets check state for all listed drives to true.
        {
            SetCheckState(lstDrives, true);
            updateDriveCount(sender, e);
        }

        private void btnSelectNone_Click(object sender, EventArgs e)
        // Sets check state for all listed drives to false.
        {
            SetCheckState(lstDrives, false);
            updateDriveCount(sender, e);
        }

         private void btnRefreshDrives_Click(object sender, EventArgs e)
        // Detects removable drives and displays in CheckedListBox
        {
            RefreshDrives(lstDrives, dictRemovableDrives);
        }

        private void btnStartCopy_Click(object sender, EventArgs e)
        // Does some error checking on user input, and starts the FileSystem.CopyDirectory method
        {
            // Set UI properties and other vars
            PictureBox1.Visible = false;
            cancelled = false;
                        
            // If source directory is not a valid directory, exit method
            if (Directory.Exists(txtSourceDir.Text) == false)
            {
                MessageBox.Show("Please select a valid source directory.", "USB Batch Copy", MessageBoxButtons.OK);
                return;
            }

            // If no drives are checked in CheckedListBox, exit method 
            if (lstDrives.CheckedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one destination drive.", "USB Batch Copy", MessageBoxButtons.OK);
                return;
            }

            // Add checked items in CheckedListBox to list object
            PopulateDriveList(lstDrives, listDrivesToCopy);

            // Set some properties and variables
            sourceDir = txtSourceDir.Text;
            btnStartCopy.Enabled = false;
            lblStatus.Text       = "Copying...";
            lblStatus.ForeColor  = Color.Black;

            /*
            try
            {
                // Check if drives are ready, exit method if any drives are not ready
                for (int i = 0; i < listDrivesToCopy.Count; i++)
                {
                    string drive = listDrivesToCopy[i];
                    IEnumerable<DriveInfo> drives =
                        from d in DriveInfo.GetDrives()
                        where d.Name == drive
                        select d;
                    
                    foreach (var drv in drives)
                    {
                        if (drv.IsReady == false)
                        {
                            MessageBox.Show("Drive " + drv + " is not ready, please check and try again.");
                            return;
                        }
                    }
                }                                           
            }
            catch (Exception)
            {
                return;
            }
            */

            backgroundWorker1.RunWorkerAsync();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var fbd = new FolderBrowserDialog();

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtSourceDir.Text = fbd.SelectedPath;                    
            }
        }

        private void tmrRefresh_Tick(object sender, EventArgs e)
        {
            lblSelectedDrives.Text = "Drives Selected: " + lstDrives.CheckedItems.Count;
            //GC.Collect();
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            // Start copy execution
            try
            {
                foreach (string t in listDrivesToCopy)
                {
                    FileSystem.CopyDirectory(sourceDir, t, UIOption.AllDialogs, UICancelOption.ThrowException);
                }
            }
            catch (OperationCanceledException)
            {
                // Updates object properties using safe method to access UI thread
                //ExecuteSecure(() => btnStartCopy.Enabled = true);
                //ExecuteSecure(() => lblStatus.ForeColor  = Color.Black);
                //ExecuteSecure(() => lblStatus.Text       = "Cancelled copy.");
                
                // Update UI object properties using safe method 
                BeginInvoke((Action)delegate
                {
                    btnStartCopy.Enabled = true;
                    lblStatus.ForeColor = Color.Black;
                    lblStatus.Text = "Cancelled copy.";
                });
                
                cancelled = true;
            }
            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            btnStartCopy.Enabled = true;

            if (cancelled == true)
            {
                return;
            }

            PictureBox1.Visible = true;

            if (lstDrives.CheckedItems.Count == 1)
            {
                lblStatus.Text = "Success! Copied to 1 drive.";
            }
            else
                lblStatus.Text = "Success! Copied to " + lstDrives.CheckedItems.Count + " drives.";

            lblStatus.ForeColor = Color.Green;
            SetCheckState(lstDrives, false);
        }

        private void txtSourceDir_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asynchronously so that it occurs after Click
            BeginInvoke((Action)delegate
            {
                txtSourceDir.SelectAll();
            });
        }

        private void updateDriveCount(object sender, EventArgs e)
        {
            lblSelectedDrives.Text = "Drives Selected: " + lstDrives.CheckedItems.Count;
        }
    }
}
