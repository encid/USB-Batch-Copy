/*
 USB Batch Copy
 Written by R. Cavallaro
 Version 1.08
*/

using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;
using System.Management;

namespace WindowsFormsApplication1
{
    public partial class Main : Form {
        //const string SHELL = "shell32.dll";
        int currDriveCount;

        //[DllImport(SHELL, CharSet = CharSet.Unicode)]
        //private static extern uint SHParseDisplayName(string pszName, IntPtr zero, [Out] out IntPtr ppidl, uint sfgaoIn, [Out] out uint psfgaoOut);

        //[DllImport(SHELL, CharSet = CharSet.Unicode)]
        //private static extern uint SHGetNameFromIDList(IntPtr pidl, SIGDN sigdnName, [Out] out String ppszName);

        /*public enum SIGDN : uint
          {
              NORMALDISPLAY = 0x00000000,
              PARENTRELATIVEPARSING = 0x80018001,
              DESKTOPABSOLUTEPARSING = 0x80028000,
              PARENTRELATIVEEDITING = 0x80031001,
              DESKTOPABSOLUTEEDITING = 0x8004c000,
              FILESYSPATH = 0x80058000,
              URL = 0x80068000,
              PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
              PARENTRELATIVE = 0x80080001
          }*/

        private struct CopyParams {
            public readonly string SourceDir;
            public readonly List<string> DestDirs;
            public CopyParams(string source, List<string> destinations)
            {
                SourceDir = source;
                DestDirs = destinations;
            }
        }

        private struct ProgressParams {
            public int CurrentDrive;
            public readonly int TotalDrives;
            public ProgressParams(int currentDrive, int totalDrives)
            {
                CurrentDrive = currentDrive;
                TotalDrives = totalDrives;
            }
        }

        public Main()
        {
            InitializeComponent();
            PopulateListView(lvDrives);  // Add (destination) removable drives to ListView
            PopulateTreeView(dirsTreeView);  // Add (source) drives to TreeView

            // Keep selected node highlighted if TreeView control loses focus            
            dirsTreeView.BeforeExpand += new TreeViewCancelEventHandler(this.dirsTreeView_BeforeExpand);
            dirsTreeView.DrawNode += (o, e) => {
                if (!e.Node.TreeView.Focused && e.Node == e.Node.TreeView.SelectedNode) {
                    Font treeFont = e.Node.NodeFont ?? e.Node.TreeView.Font;
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                    ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, SystemColors.HighlightText, SystemColors.Highlight);
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, treeFont, e.Bounds, SystemColors.HighlightText, TextFormatFlags.GlyphOverhangPadding);
                }
                else
                    e.DrawDefault = true;
            };
            dirsTreeView.MouseDown += (o, e) => {
                TreeNode node = dirsTreeView.GetNodeAt(e.X, e.Y);
                if (node != null && node.Bounds.Contains(e.X, e.Y))
                    dirsTreeView.SelectedNode = node;
            };

            // Tick the checkbox if any part of the item line in ListView is clicked
            lvDrives.MouseClick += (o, e) => {
                ListViewItem lvi = lvDrives.GetItemAt(e.X, e.Y);
                if (e.X > 16) lvi.Checked = !lvi.Checked;
            };

            // Add columns to ListView
            lvDrives.Columns.Add("Drive", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Volume name", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("File system", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Free space", -2, HorizontalAlignment.Left);
            lvDrives.Columns.Add("Capacity", -2, HorizontalAlignment.Left);
        }

        /// <summary>
        /// Performs an action safely the appropriate thread.
        /// </summary>
        /// <param name="a">Action to perform.</param>
        private void ExecuteSecure(Action a)
        // Usage example: ExecuteSecure(() => this.someLabel.Text = "foo");
        {
            BeginInvoke((Action)delegate {
                a();
            });
        }        

        private List<string> GetDestinationDirs(ListView lview)
        {
            List<string> d = new List<string>();

            foreach (ListViewItem item in lview.CheckedItems) {
                d.Add(item.Text);
            }
            
            return d;
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
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders) {
                if (bytes > max)
                    return string.Format("{0:##.#} {1}", decimal.Divide(bytes, max), order);
                max /= scale;
            }
            return "0 Bytes";
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
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        /// <summary>
        /// Set CheckState for all items in a ListView.
        /// </summary>
        /// <param name="list">ListView to set CheckState on.</param>
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
            var d = drives.Where(p => p.DriveType == DriveType.Removable && p.IsReady);

            return d;
        }

        /// <summary>
        /// Detects removable drives and adds them to ListView.
        /// </summary>
        /// <param name="lview">ListView to populate.</param>
        private void PopulateListView(ListView lview)
        {
            lview.Items.Clear();

            IEnumerable<DriveInfo> drives = GetRemovableDrives();

            // If no removable drives detected, exit method
            if (!drives.Any()) return;

            // Iterate through collection and add each removable drive as to ListView as items and subitems
            foreach (var drive in drives) {
                var freeSpace = BytesToString(drive.TotalFreeSpace);
                var totalSpace = BytesToString(drive.TotalSize);

                ListViewItem oItem = new ListViewItem();

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

        /// <summary>
        /// Validates the UI input parameters for copying, and throws exceptions based on parameters.
        /// </summary>
        /// <param name="srcDir">Source directory to copy from.</param>
        /// <param name="destDirs">Destination directories in list collection.</param>
        private void ValidateCopyParams(string srcDir, List<string> destDirs)
        {
            // Check to make sure user has selected a source folder.
            if (srcDir == "")
                throw new Exception("Please select a valid source folder.");
            
            // Check if source drive is ready and exists
            DriveInfo dInfo = new DriveInfo(srcDir.Substring(0, 2));
            if (!dInfo.IsReady || !Directory.Exists(dInfo.Name))
                throw new Exception("Source drive is not ready. Please try again.");

            // Check if source folder is empty.  Exit if true
            if (IsDirectoryEmpty(srcDir))
                throw new Exception("Source folder is empty; cannot copy an empty folder. Please try again.");

            // If no drives are checked in CheckedListBox, exit method 
            if (destDirs.Count == 0)
                throw new Exception("Please select at least one destination drive.");

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
            string srcDir = "";
            TreeNode aNode = dirsTreeView.SelectedNode;
            
            if (aNode != null)
                srcDir = (string)aNode.Tag;

            CopyParams cp = new CopyParams(srcDir, GetDestinationDirs(lvDrives));

            try {
                // Validate user input on UI
                ValidateCopyParams(cp.SourceDir, cp.DestDirs);

                // No exceptions, so continue....
                // Disable UI controls and set status
                DisableUI();
                lblStatus.Text = "Copying...";
                lblStatus.ForeColor = Color.Black;

                // Begin the copy in BackgroundWorker, pass CopyParams object into it
                bw.RunWorkerAsync(cp);
            }            
            catch (Exception ex) {
                MessageBox.Show("Error:  " + ex.Message, "USB Batch Copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (ex.Message.Contains("Please select a valid source folder"))
                    PopulateTreeView(dirsTreeView);                
                if (ex.Message.Contains("Target destination drive does not exist"))
                    PopulateListView(lvDrives);
            }
        }
        
        private void tmrRefresh_Tick(object sender, EventArgs e)
        {
            lblSelectedDrives.Text = string.Format("Drives Selected: {0}", lvDrives.CheckedItems.Count);

            // AUTOMATIC REFRESH of drive list -- Edit config file to enable/disable
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1") { 
                IEnumerable<DriveInfo> drives = GetRemovableDrives();

                if (drives.Count() != currDriveCount)
                    PopulateListView(lvDrives);

                currDriveCount = drives.Count();
            }            
        }

        private void bw_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            CopyParams cp = (CopyParams)e.Argument;

            try {                
                PerformCopy(cp.SourceDir, cp.DestDirs);
            }
            catch (ArgumentException) {  // Catch user removal of drive during copy for RunWorkerCompleted to process
            }
            catch (OperationCanceledException) {  // Catch user cancelling copy for RunWorkerCompleted to process
                e.Cancel = true;
            }
        }

        private void bw_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            ProgressParams pp = (ProgressParams)e.UserState;

            lblStatus.Text = String.Format("Copying drive {0} of {1}..", pp.CurrentDrive, pp.TotalDrives);
        }

        private void PerformCopy(string srcDir, List<string> destDirs)
        {
            ProgressParams pp = new ProgressParams(1, destDirs.Count);

            // Start copy execution
            for (int i = 0; i < destDirs.Count; i++) {
                pp.CurrentDrive++;
                bw.ReportProgress(i, pp);
                string destDir = destDirs[i];
                FileSystem.CopyDirectory(srcDir, destDir, UIOption.AllDialogs, UICancelOption.ThrowException);
            }            
        }

        private void bw_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {                        
            if (e.Cancelled) {   
                // The user cancelled the operation.                
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text = "Ready";
                MessageBox.Show("Copying operation has been cancelled.", "USB Batch Copy", MessageBoxButtons.OK);
                this.BringToFront();
                this.Focus();
            }            
            else if (e.Error != null) {
                // There was an error during the operation.
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text = "Ready";
                PopulateListView(lvDrives);
                MessageBox.Show("An error has occured: " + e.Error.Message, "USB Batch Copy", MessageBoxButtons.OK);
                this.BringToFront();
                this.Focus();
            }
            else {
                // The operation completed normally.
                PictureBox1.Visible = true;
                lblStatus.ForeColor = Color.Green;
                if (lvDrives.CheckedItems.Count == 1)
                    lblStatus.Text = "Success! Copied to 1 drive.";
                else
                    lblStatus.Text = string.Format("Success! Copied to {0} drives.", GetDestinationDirs(lvDrives).Count());                
            }

            // Enable UI controls            
            EnableUI();
            
            SetListViewCheckState(lvDrives, false);
        }

        private void EnableUI()
        {
            // Enable UI controls            
            if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = true; }
            btnStartCopy.Enabled = true;
            btnSelectAll.Enabled = true;
            btnSelectNone.Enabled = true;
            lvDrives.Enabled = true;
            dirsTreeView.Enabled = true;
            tmrRefresh.Enabled = true;
        }

        private void DisableUI()
        {
            // Enable UI controls            
            if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = false; }
            btnStartCopy.Enabled = false;
            btnSelectAll.Enabled = false;
            btnSelectNone.Enabled = false;
            lvDrives.Enabled = false;
            dirsTreeView.Enabled = false;
            tmrRefresh.Enabled = false;
        }

        /// <summary>
        /// Populates a TreeView control with all logical drives.
        /// </summary>
        /// <param name="treeView">Specifies the TreeView control to populate.</param>
        private void PopulateTreeView(TreeView treeViewName)
        {
            // Clear the TreeView of nodes
            treeViewName.Nodes.Clear();

            // Get a list of the drives
            string[] drives = Environment.GetLogicalDrives();
            
            // Iterate through drives to set icons and labels
            foreach (string drive in drives) {
                DriveInfo di    = new DriveInfo(drive);
                string drvLabel = string.Empty;
                int driveImage;

                // Set drive's icon and label based on drive type
                switch (di.DriveType) {
                    case DriveType.Fixed:
                        driveImage = 2;
                        if (di.VolumeLabel == string.Empty)
                            drvLabel = "Local Disk";
                        else drvLabel = di.VolumeLabel;
                        break;
                    case DriveType.CDRom:
                        driveImage = 3;
                        drvLabel   = "CD_ROM";
                        break;
                    case DriveType.Removable:
                        driveImage = 5;
                        drvLabel   = "Removable Disk";
                        break;
                    case DriveType.Network:
                        string fullUNC = GetUNCPath(di.Name.Substring(0, 2));
                        int lastSlash  = fullUNC.LastIndexOf(@"\") + 1;
                        string netPath = fullUNC.Substring(0, lastSlash - 1);
                        drvLabel       = fullUNC.Substring(lastSlash, fullUNC.Length - lastSlash);                        
                        drvLabel       = drvLabel + " (" + netPath + ")";
                        driveImage     = 6;
                        break;
                    case DriveType.NoRootDirectory:
                        driveImage = 8;
                        break;
                    case DriveType.Unknown:
                        driveImage = 8;
                        break;
                    default:
                        driveImage = 2;
                        break;
                }                

                // If drive label exists, add a space after it
                if (!string.IsNullOrEmpty(drvLabel))
                    drvLabel = drvLabel + " ";
                                              
                TreeNode node = new TreeNode(drvLabel + "(" + di.Name.Substring(0, 2) + ")", driveImage, driveImage);
                
                node.Tag = drive;

                if (di.IsReady)
                    node.Nodes.Add("...");

                treeViewName.Nodes.Add(node);
            }         
        }

        private void dirsTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count > 0) {
                if (e.Node.Nodes[0].Text == "..." && e.Node.Nodes[0].Tag == null) {
                    e.Node.Nodes.Clear();

                    //get the list of sub direcotires
                    //IEnumerable<string> dirs = Directory.EnumerateDirectories((string)e.Node.Tag);
                    List<string> dirs = new List<string>(Directory.GetDirectories((string)e.Node.Tag));

                    foreach (string dir in dirs) {
                        var di = new DirectoryInfo(dir);
                        var node = new TreeNode(di.Name, 0, 1);

                        try {
                            node.Tag = dir;  //keep the directory's full path in the tag for use later

                            //if the directory has any sub directories add the place holder
                            if (di.GetDirectories().Count() > 0)
                                node.Nodes.Add(null, "...", 0, 0);
                        }
                        catch (UnauthorizedAccessException) {
                            //if an unauthorized access exception occured display a locked folder
                            node.ImageIndex = 12;
                            node.SelectedImageIndex = 12;
                        }
                        catch (Exception ex) {
                            MessageBox.Show(ex.Message, "USB Batch Copy", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                        finally {
                            e.Node.Nodes.Add(node);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the network path for the specified drive.
        /// </summary>
        /// <param name="path">Drive to perform search on.</param>
        /// <returns></returns>
        public static string GetUNCPath(string path)
        {
            if (path.StartsWith(@"\\")) return path;

            ManagementObject mo = new ManagementObject();
            mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", path));
            return Convert.ToString(mo["ProviderName"]);
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

        /*public string GetDriveLabels(string driveNameAsLetterColonBackslash)
        {
            IntPtr pidl;
            uint dummy;
            string name;
            if (SHParseDisplayName(driveNameAsLetterColonBackslash, IntPtr.Zero, out pidl, 0, out dummy) == 0
                && SHGetNameFromIDList(pidl, SIGDN.PARENTRELATIVEEDITING, out name) == 0
                && name != null)
            {
                return name;
            }
            return null;
        }*/
    }
}