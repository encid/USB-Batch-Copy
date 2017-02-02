/*
 USB Batch Copy
 Written by R. Cavallaro
 Version 1.1.0
*/

using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;
using System.Runtime.InteropServices;

namespace USBBatchCopy
{
    public partial class Main : Form {
        bool startup = true;
        FolderBrowserDialog fbd;
        const int WM_DEVICECHANGE = 0x0219; //see msdn site
        const int DBT_DEVICEARRIVAL = 0x8000;
        const int DBT_DEVICEREMOVALCOMPLETE = 0x8004;
        const int DBT_DEVTYPVOLUME = 0x00000002;

        private struct CopyParams {
            public readonly string _sourceDir;
            public readonly List<string> _destDirs;
            public CopyParams(string source, List<string> destinations)
            {
                _sourceDir = source;
                _destDirs = destinations;
            }
        }

        private struct ProgressParams {
            public int _currentDrive;
            public readonly int _totalDrives;
            public ProgressParams(int currentDrive, int totalDrives)
            {
                _currentDrive = currentDrive;
                _totalDrives = totalDrives;
            }
        }

        [StructLayout(LayoutKind.Sequential)] //Same layout in mem
        public struct DEV_BROADCAST_VOLUME
        {
            public int dbcv_size;
            public int dbcv_devicetype;
            public int dbcv_reserved;
            public int dbcv_unitmask;
        }

        public Main()
        {
            InitializeComponent();

            PopulateListView(lvDrives);  // Add (destination) removable drives to ListView
            startup = false;

            // Tick the checkbox if any part of the item line in ListView is clicked
            lvDrives.MouseClick += (o, e) => {
                var lvi = lvDrives.GetItemAt(e.X, e.Y);
                if (e.X > 16) lvi.Checked = !lvi.Checked;
            };

            // Add columns to the ListView
            lvDrives.Columns.Add("Drive", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Volume name", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("File system", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Free space", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Capacity", -2, HorizontalAlignment.Left);

            fbd = new FolderBrowserDialog {
                ShowNewFolderButton = false,
                Description = "Select the source folder to copy files from.\nIf copying software, you should select the 'ECL' folder of the software p/n."                
            };

            //Make sure textbox stays at the most recent line(bottom most)
            rt.TextChanged += (sender, e) => {
                if (rt.Visible) 
                    rt.ScrollToCaret();
            };
        }

        /// <summary>
        /// Performs an action safely the appropriate thread.
        /// </summary>
        /// <param name="a">Action to perform.</param>
        private void ExecuteSecure(Action a)
        // Usage example: ExecuteSecure(() => this.someLabel.Text = "foo");
        {
            BeginInvoke(a);            
        }        

        private List<string> GetDestinationDirs(ListView lview)
        {
            var d = new List<string>();

            foreach (ListViewItem item in lview.CheckedItems) {
                d.Add(item.Text);
            }
            
            return d;
        }

        /// <summary>
        /// Format a long number into a readable string; i.e. input: 15520 output: "15.5 KB"
        /// </summary>
        /// <param name="byteCount">Number of bytes.</param>
        /// <returns></returns>
        private string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        /// <summary>
        /// Set CheckState for all items in a ListView.
        /// </summary>
        /// <param name="lview">ListView to set CheckState on.</param>
        /// <param name="choice">Set CheckState; Checked = True and Unchecked = False.</param>
        private void SetListViewCheckState(ListView lview, bool choice)
        // Check or uncheck all items in CheckedListBox, choice = false for uncheck, choice = true for check
        {
            foreach (ListViewItem item in lview.Items)
                item.Checked = choice;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1") {
                btnRefreshDrives.Font = new Font("Arial", 8.25F, FontStyle.Italic, GraphicsUnit.Point, ((byte)(0)));
                btnRefreshDrives.Text = "Auto-Detect On";
                btnRefreshDrives.Enabled = false;
            }
        }

        /// <summary>
        /// Retrieves a collection of removable drives that are ready.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<DriveInfo> GetRemovableDrives()
        {
            var drives = DriveInfo.GetDrives();

            return drives.Where(p => p.DriveType == DriveType.Removable && p.IsReady);            
        }

        /// <summary>
        /// Detects removable drives and adds them to ListView.
        /// </summary>
        /// <param name="lview">ListView to populate.</param>
        private void PopulateListView(ListView lview)
        {
            if (startup == true) Logger.Log("Scanning for USB devices..", rt);

            lview.Items.Clear();

            var drives = GetRemovableDrives();

            // If no removable drives detected, exit method
            if (!drives.Any()) {
                if (startup == true) Logger.Log("No USB devices detected", rt);
                return;
            }
            
            string driveNames = "";
            // Iterate through collection and add each removable drive as to ListView as items and subitems
            foreach (var drive in drives) {
                driveNames += drive.Name + ", "; 
                var freeSpace = BytesToString(drive.TotalFreeSpace);
                var totalSpace = BytesToString(drive.TotalSize);
                var oItem = new ListViewItem();

                oItem.Text = drive.Name;
                oItem.SubItems.Add(drive.VolumeLabel);
                oItem.SubItems.Add(drive.DriveFormat);
                oItem.SubItems.Add(freeSpace);
                oItem.SubItems.Add(totalSpace);

                lview.Items.Add(oItem);
            }            

            // Set column width
            for (int i = 0; i < lview.Columns.Count; i++) {
                lview.Columns[i].Width = -2;
            }            

            if (startup == true) {
                // Get count and names of drives found and log it to status
                driveNames = driveNames.Substring(0, driveNames.Length - 2);
                string logStr;
                if (drives.Count() == 1)
                    logStr = string.Format("Detected {0} USB device: {1}", drives.Count(), driveNames);
                else
                    logStr = string.Format("Detected {0} USB devices: {1}", drives.Count(), driveNames);
                Logger.Log(logStr, rt); 
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        // Sets check state for all listed drives to true.
        {
            SetListViewCheckState(lvDrives, true);
        }

        private void btnSelectNone_Click(object sender, EventArgs e)
        // Sets check state for all listed drives to false.
        {
            SetListViewCheckState(lvDrives, false);
        }

        private void btnRefreshDrives_Click(object sender, EventArgs e)
        // Detects removable drives and displays in CheckedListBox
        {
            PopulateListView(lvDrives);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            if (fbd.SelectedPath == "") { fbd.SelectedPath = @"V:\Released_Part_Information\"; }

            if (fbd.ShowDialog() == DialogResult.OK && fbd.SelectedPath != txtSourceDir.Text) {
                txtSourceDir.Text = fbd.SelectedPath;
                var logStr = string.Format("Source folder set to: '{0}'", fbd.SelectedPath);                
                Logger.Log(logStr, rt);                
            }
        }

        /// <summary>
        /// Validates the UI input parameters for copying, and throws exceptions based on parameters.
        /// </summary>
        /// <param name="srcDir">Source directory to copy from.</param>
        /// <param name="destDirs">Destination directories in list collection.</param>
        private void ValidateCopyParams(string srcDir, List<string> destDirs)
        {
            // Check to make sure user has selected a source folder.
            if (!Directory.Exists(srcDir))
                throw new Exception("Please select a valid source folder and try again.");
            
            // Check if source drive is ready and exists
            var dInfo = new DriveInfo(srcDir.Substring(0, 2));
            if (!dInfo.IsReady)
                throw new Exception("Source drive is not ready. Please try again.");

            // Check if source folder is empty.  Exit if true
            if (IsDirectoryEmpty(srcDir))
                throw new Exception("Source folder is empty; cannot copy an empty folder. Please try again.");

            // If no drives are checked in CheckedListBox, exit method 
            if (destDirs.Count == 0)
                throw new Exception("Please select at least one destination drive and try again.");

            // Check to make sure source drive and destination drive are not the same, exit if true
            // Check to make sure user did not remove drive after refresh, but before copy
            foreach (var destDir in destDirs) {
                if (srcDir.Substring(0, 1) == destDir.Substring(0, 1))
                    throw new Exception("Source drive and destination drive cannot be the same. Please try again.");
                if (!Directory.Exists(destDir))
                    throw new Exception("Target destination drive does not exist. Please try again.");                
            }
        }

        private void btnStartCopy_Click(object sender, EventArgs e)
        {
            PictureBox1.Visible = false;

            var cp = new CopyParams(txtSourceDir.Text, GetDestinationDirs(lvDrives));

            try {
                // Validate user input on UI
                ValidateCopyParams(cp._sourceDir, cp._destDirs);

                // No exceptions, so continue....
                // Disable UI controls and set status
                DisableUI();
                lblStatus.Text = "Copying...";
                var logStr = string.Format("Starting to copy [{0}] to {1} drive(s)...", cp._sourceDir, cp._destDirs.Count);
                Logger.Log(logStr, rt);

                // Begin the copy in BackgroundWorker, pass CopyParams object into it
                bw.RunWorkerAsync(cp);
            }
            catch (Exception ex) {
                var logStr = string.Format("Error: {0}", ex.Message);
                Logger.Log(logStr, rt, Color.Red);
                //MessageBox.Show("Error:  " + ex.Message, "USB Batch Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (ex.Message.Contains("Target destination drive does not exist"))
                    PopulateListView(lvDrives);
            }
        }

        private void bw_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var cp = (CopyParams)e.Argument;

            try {                
                PerformCopy(cp._sourceDir, cp._destDirs);
            }
            catch (ArgumentException) {  // Catch user removal of drive during copy for RunWorkerCompleted to process
            }
            catch (OperationCanceledException) {  // Catch user cancelling copy for RunWorkerCompleted to process
                e.Cancel = true;
            }
        }

        private void bw_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            var pp = (ProgressParams)e.UserState;

            var logStr = string.Format("Copying drive {0} of {1}..", pp._currentDrive, pp._totalDrives);
            Logger.Log(logStr, rt);

        }

        private void PerformCopy(string srcDir, List<string> destDirs)
        {
            var pp = new ProgressParams(0, destDirs.Count);

            // Start copy execution
            for (int i = 0; i < destDirs.Count; i++) {
                pp._currentDrive++;
                bw.ReportProgress(i, pp);
                string destDir = destDirs[i];
                FileSystem.CopyDirectory(srcDir, destDir, UIOption.AllDialogs, UICancelOption.ThrowException);
            }            
        }

        private void bw_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {                        
            if (e.Cancelled) {   
                // The user cancelled the operation.
                lblStatus.Text = "Ready";
                Logger.Log("Copy operation has been cancelled", rt, Color.Red);
                this.BringToFront();
                this.Focus();
            }            
            else if (e.Error != null) {
                // There was an error during the operation.
                lblStatus.Text = "Ready";
                PopulateListView(lvDrives);
                var logStr = string.Format("An error has occured: {0}", e.Error.Message);
                Logger.Log(logStr, rt, Color.Red);
                MessageBox.Show("An error has occured: " + e.Error.Message, "USB Batch Copy", MessageBoxButtons.OK);
                this.BringToFront();
                this.Focus();
            }
            else {
                // The operation completed normally.
                PictureBox1.Visible = true;
                lblStatus.Text = "Ready";
                var logStr = string.Format("Copy operation successful -- copied to {0} drive(s)", GetDestinationDirs(lvDrives).Count());
                Logger.Log(logStr, rt, Color.Green);             
            }

            // Enable UI controls            
            EnableUI();
            
            SetListViewCheckState(lvDrives, false);
        }

        private void EnableUI()
        {
            // Enable UI controls            
            //if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = true; }
            btnRefreshDrives.Enabled |= ConfigurationManager.AppSettings["autoRefresh"] == "0";
            btnStartCopy.Enabled = true;
            btnSelectAll.Enabled = true;
            btnSelectNone.Enabled = true;
            lvDrives.Enabled = true;
            btnBrowse.Enabled = true;
            txtSourceDir.Enabled = true;
        }

        private void DisableUI()
        {
            // Enable UI controls            
            //if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = false; }
            btnRefreshDrives.Enabled &= ConfigurationManager.AppSettings["autoRefresh"] != "0";
            btnStartCopy.Enabled = false;
            btnSelectAll.Enabled = false;
            btnSelectNone.Enabled = false;
            lvDrives.Enabled = false;
            btnBrowse.Enabled = false;
            txtSourceDir.Enabled = false;
        }

        /// <summary>
        /// Check if specified directory is empty.
        /// </summary>
        /// <param name="path">Path to check.</param>
        /// <returns></returns>
        public bool IsDirectoryEmpty(string path)
        {
            return !Directory.EnumerateFileSystemEntries(path).Any();
        }

        protected override void WndProc(ref Message m)
        {
            try {
                if (m.Msg == WM_DEVICECHANGE) {
                    var vol = (DEV_BROADCAST_VOLUME)Marshal.PtrToStructure(m.LParam, typeof(DEV_BROADCAST_VOLUME));
                    if ((m.WParam.ToInt32() == DBT_DEVICEARRIVAL) && (vol.dbcv_devicetype == DBT_DEVTYPVOLUME)) {
                        var driveStr = string.Format(@"{0}:\", DriveMaskToLetter(vol.dbcv_unitmask).ToString());
                        PopulateListView(lvDrives);
                        var logStr = string.Format("Detected new USB device: {0}", driveStr);
                        Logger.Log(logStr, rt);
                        
                    }
                    if ((m.WParam.ToInt32() == DBT_DEVICEREMOVALCOMPLETE) && (vol.dbcv_devicetype == DBT_DEVTYPVOLUME)) {
                        var driveStr = string.Format(@"{0}:\", DriveMaskToLetter(vol.dbcv_unitmask).ToString());
                        PopulateListView(lvDrives);
                        var logStr = string.Format("Detected USB device removal: {0}", driveStr);
                        Logger.Log(logStr, rt);
                    }
                }
                base.WndProc(ref m);
            }
            catch (NullReferenceException) {
            }
            catch (Exception) {
            }            
        }        

        private static char DriveMaskToLetter(int mask)
        {
            char[] units ={ 'A', 'B', 'C', 'D', 'E', 'F', 'G',
                        'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
                        'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
            int i = 0;
            //Convert the mask in an array, and search
            //the index for the first occurrence (the unit's name)
            var ba = new System.Collections.BitArray(BitConverter.GetBytes(mask));
            foreach (bool var in ba) {
                if (var == true)
                    break;
                i++;
            }
            return units[i];
        }
    }
}