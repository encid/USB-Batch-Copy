/*
 USB Batch Copy
 Written by R. Cavallaro
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
    public partial class Main : Form
    {
      //const string SHELL = "shell32.dll";
        Dictionary<string, string> dictRemovableDrives = new Dictionary<string, string>();
        FolderBrowserDialog fbd = new FolderBrowserDialog();
        List<string> listDrivesToCopy = new List<string>();
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

        public Main()
        {
            InitializeComponent();
            PopulateListView(lvDrives);  // Add (destination) removable drives to ListView
            PopulateTreeView(dirsTreeView);  // Add (source) drives to TreeView

            // Keep selected node highlighted if TreeView control loses focus            
            dirsTreeView.BeforeExpand += new TreeViewCancelEventHandler(this.dirsTreeView_BeforeExpand);
            dirsTreeView.DrawNode += (o, e) =>
                {
                    if (!e.Node.TreeView.Focused && e.Node == e.Node.TreeView.SelectedNode)
                    {
                        Font treeFont = e.Node.NodeFont ?? e.Node.TreeView.Font;
                        e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                        ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, SystemColors.HighlightText, SystemColors.Highlight);
                        TextRenderer.DrawText(e.Graphics, e.Node.Text, treeFont, e.Bounds, SystemColors.HighlightText, TextFormatFlags.GlyphOverhangPadding);
                    }
                    else
                        e.DrawDefault = true;
                };
            dirsTreeView.MouseDown += (o, e) =>
                {
                    TreeNode node = dirsTreeView.GetNodeAt(e.X, e.Y);
                    if (node != null && node.Bounds.Contains(e.X, e.Y))
                        dirsTreeView.SelectedNode = node;
                };

            // Tick the checkbox if any part of the item line in ListView is clicked
            lvDrives.MouseClick += (o, e) =>
                {
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
        /// Determines if an invoke is required to perform an action.
        /// </summary>
        /// <param name="a">Action to perform.</param>
        private void ExecuteSecure(Action a)
        // Usage example: ExecuteSecure(() => this.someLabel.Text = "foo");
        {
            BeginInvoke((Action)delegate { a(); });
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
                list.SetItemChecked(i, choice);
        }

        /// <summary>
        /// Set CheckState for all items in a ListView.
        /// </summary>
        /// <param name="list">ListView to set CheckState on.</param>
        /// <param name="choice">Set CheckState; Checked = True and Unchecked = False.</param>
        private void SetListViewCheckState(ListView list, bool choice)
        // Check or uncheck all items in CheckedListBox, choice = false for uncheck, choice = true for check
        {
            foreach (ListViewItem item in list.Items)
                item.Checked = choice;
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1")
            {
                btnRefreshDrives.Font    = new Font("Arial", 8.25F, FontStyle.Italic, GraphicsUnit.Point, ((byte)(0)));
                btnRefreshDrives.Text    = "Auto-Detect On";
                btnRefreshDrives.Enabled = false;
            }

            //TreeNode treeNode = dirsTreeView.Nodes[0];
            //dirsTreeView.SelectedNode = treeNode;
        }

        /// <summary>
        /// Retrieves a collection of removable drives that are ready.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<DriveInfo> GetRemovableDrives()
        {
            // Get collection of connected drives and query for removable and ready drives
            IEnumerable<DriveInfo> retVal =
                from d in DriveInfo.GetDrives()
                where d.DriveType == DriveType.Removable &&
                      d.IsReady
                select d;

            return retVal;
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

            IEnumerable<DriveInfo> drives = GetRemovableDrives();

            // If no removable drives detected, exit method
            if (!drives.Any()) return;

            // Iterate through collection, add drive name and drive information to dictionary
            foreach (var drive in drives)
            {
                var freeSpace  = FormatBytes(drive.TotalFreeSpace);
                var totalSpace = FormatBytes(drive.TotalSize);
                var drvInfo    = String.Format("{0} - ( Label: {1}, FileSystem: {2}, Size: {3}, Free: {4} )",
                                    drive.Name, drive.VolumeLabel, drive.DriveFormat,
                                    totalSpace, freeSpace);
                dict.Add(drive.Name, drvInfo);
            }

            // Bind dictionary as CheckedListBox DataSource and set other properties
            clb.DataSource    = new BindingSource(dictRemovableDrives, null);
            clb.DisplayMember = "Value";
            clb.ValueMember   = "Key";
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

            // Iterate through collection, add drive name and drive information to dictionary
            foreach (var drive in drives)
            {
                var freeSpace = FormatBytes(drive.TotalFreeSpace);
                var totalSpace = FormatBytes(drive.TotalSize);

                ListViewItem oItem = new ListViewItem();
                
                oItem.Text = drive.Name;
                oItem.SubItems.Add(drive.VolumeLabel);
                oItem.SubItems.Add(drive.DriveFormat);
                oItem.SubItems.Add(freeSpace);
                oItem.SubItems.Add(totalSpace);

                lview.Items.Add(oItem);                
            }

            // Set column width
            for (int i = 0; i < lview.Columns.Count;  i++)
            {
                lview.Columns[i].Width = -2;
            }
        }

        /// <summary>
        /// Add checked items in ListView to list collection.
        /// </summary>
        /// <param name="lview">ListView to parse.</param>
        /// <param name="list">List collection to add items to.</param>
        private void AddDrivesToCopyList(ListView lview, List<string> list)
        {
            // Clear list collection.
            list.Clear();

            // Iterate through checked items in ListView and add them to list collection
            foreach (ListViewItem item in lview.CheckedItems)
            {
                list.Add(item.Text);
            }
        }

        /// <summary>
        /// Add checked items in CheckedListBox to list collection.
        /// </summary>
        /// <param name="clb">CheckedListBox to parse.</param>
        /// <param name="list">List collection to add items to.</param>
        private void old_AddDrivesToCopyList(CheckedListBox clb, List<string> list)
        {
            // Clear list collection.
            list.Clear();

            // Iterate through KeyValuePairs in CheckedListBox and add each Key to list collection                        
            foreach (KeyValuePair<string, string> pair in clb.CheckedItems)
                list.Add(pair.Key);            
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

        private void ValidateCopyParams(string srcDir, List<string> destList)
        {
            // Check to make sure user has selected a source folder.
            if (srcDir == "")
                throw new Exception("SourceDirNotFound");

            // Check if source drive is ready and exists
            DriveInfo dInfo = new DriveInfo(srcDir.Substring(0, 2));
            if (!dInfo.IsReady || !Directory.Exists(dInfo.Name))
                throw new Exception("DriveNotReady");

            // Check if source folder is empty.  Exit if true
            if (IsDirectoryEmpty(srcDir))
                throw new Exception("SourceDirEmpty");
            
            // If no drives are checked in CheckedListBox, exit method 
            if (destList.Count == 0)
                throw new Exception("NoDestDir");

            // Check to make sure source drive and destination drive are not the same, exit if true
            // Check to make sure user did not remove drive after refresh, but before copy
            foreach (var destDir in destList)
            {
                if (srcDir.Substring(0, 1) == destDir.Substring(0, 1))
                    throw new Exception("SourceAndDestSame");
                if (!Directory.Exists(destDir))
                    throw new Exception("DestDriveNotExist");
            }

            // Disable UI controls and set some variables    
            DisableUI();
            lblStatus.Text = "Copying...";
            lblStatus.ForeColor = Color.Black;

            // Begin the copy in BackgroundWorker
            backgroundWorker1.RunWorkerAsync(srcDir);
        }

        private void btnStartCopy_Click(object sender, EventArgs e)
        {
            string srcDir = "";
            TreeNode aNode = dirsTreeView.SelectedNode;

            AddDrivesToCopyList(lvDrives, listDrivesToCopy);

            if (aNode != null)
                srcDir = (string)aNode.Tag;            

            try
            {
                ValidateCopyParams(srcDir, listDrivesToCopy);
            }
            catch (Exception ex)
            {
                switch (ex.Message)
                {
                    case "SourceDirNotFound":
                        MessageBox.Show("Please select a valid source folder.", "USB Batch Copy", MessageBoxButtons.OK);
                        break;
                    case "DriveNotReady":
                        MessageBox.Show("Source drive is not ready.  Please try again.", "USB Batch Copy", MessageBoxButtons.OK);
                        PopulateTreeView(dirsTreeView);
                        break;
                    case "SourceDirEmpty":
                        MessageBox.Show("Source folder is empty; cannot copy an empty folder.  Please try again.", "USB Batch Copy", MessageBoxButtons.OK);
                        break;
                    case "NoDestDir":
                        MessageBox.Show("Please select at lease one destination drive.", "USB Batch Copy", MessageBoxButtons.OK);
                        break;
                    case "SourceAndDestSame":
                        MessageBox.Show("Source drive and destination drive cannot be the same.  Please try again.", "USB Batch Copy", MessageBoxButtons.OK);
                        break;
                    case "DestDriveNotExist":
                        MessageBox.Show("Target destination drive does not exist.  Please try again.");
                        PopulateListView(lvDrives);
                        break;
                    default:
                        MessageBox.Show(ex.Message);
                        break;
                }
            }
        }
                
        private void tmrRefresh_Tick(object sender, EventArgs e)
        {
            lblSelectedDrives.Text = string.Format("Drives Selected: {0}", lvDrives.CheckedItems.Count);

            // AUTOMATIC REFRESH of drive list -- Edit config file to enable/disable
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1")
            {
                IEnumerable<DriveInfo> drives = GetRemovableDrives();

                if (drives.Count() != currDriveCount)
                    PopulateListView(lvDrives);

                currDriveCount = drives.Count();
            }
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {            
            try
            {                
                PerformCopy((string)e.Argument, listDrivesToCopy);
            }
            catch (ArgumentException)  // Catch user removal of drive during copy for RunWorkerCompleted to process
            {
            }
            catch (OperationCanceledException)  // Catch user cancelling copy for RunWorkerCompleted to process
            {
                e.Cancel = true;
            }
        }

        private void PerformCopy(string srcDir, List<string> destList)
        {
            int totalItems = destList.Count();

            // Start copy execution
            for (int i = 0; i < totalItems; i++)
            {
                ExecuteSecure(() => lblStatus.Text = string.Format("Copying drive {0} of {1}..", (i + 1), totalItems));  // Update status label securely
                string destDir = destList[i];
                FileSystem.CopyDirectory(srcDir, destDir, UIOption.AllDialogs, UICancelOption.ThrowException);
            }            
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {                        
            if (e.Cancelled)
            {   
                // The user cancelled the operation.                
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text = "Ready";
                MessageBox.Show("Copying operation has been cancelled.", "USB Batch Copy", MessageBoxButtons.OK);
            }            
            else if (e.Error != null)
            {
                // There was an error during the operation.
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text = "Ready";
                PopulateListView(lvDrives);
                MessageBox.Show("An error has occured: " + e.Error.Message, "USB Batch Copy", MessageBoxButtons.OK);
            }
            else
            {
                // The operation completed normally.
                PictureBox1.Visible = true;
                lblStatus.ForeColor = Color.Green;
                if (lvDrives.CheckedItems.Count == 1)
                    lblStatus.Text = "Success! Copied to 1 drive.";
                else
                    lblStatus.Text = string.Format("Success! Copied to {0} drives.", listDrivesToCopy.Count());                
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
            foreach (string drive in drives)
            {
                DriveInfo di    = new DriveInfo(drive);
                string drvLabel = string.Empty;
                int driveImage;

                // Set drive's icon and label based on drive type
                switch (di.DriveType)
                {
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
                if (!string.IsNullOrEmpty(drvLabel)) { drvLabel = drvLabel + " "; }
                                              
                TreeNode node = new TreeNode(drvLabel + "(" + di.Name.Substring(0, 2) + ")", driveImage, driveImage);
                
                node.Tag = drive;

                if (di.IsReady)
                    node.Nodes.Add("...");

                treeViewName.Nodes.Add(node);
            }         
        }

        private void dirsTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count > 0)
            {
                if (e.Node.Nodes[0].Text == "..." && e.Node.Nodes[0].Tag == null)
                {
                    e.Node.Nodes.Clear();

                    //get the list of sub direcotires
                    //IEnumerable<string> dirs = Directory.EnumerateDirectories((string)e.Node.Tag);
                    List<string> dirs = new List<string>(Directory.GetDirectories((string)e.Node.Tag));

                    foreach (string dir in dirs)
                    {
                        var di = new DirectoryInfo(dir);
                        var node = new TreeNode(di.Name, 0, 1);

                        try
                        {
                            node.Tag = dir;  //keep the directory's full path in the tag for use later

                            //if the directory has any sub directories add the place holder
                            if (di.GetDirectories().Count() > 0)
                                node.Nodes.Add(null, "...", 0, 0);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            //if an unauthorized access exception occured display a locked folder
                            node.ImageIndex = 12;
                            node.SelectedImageIndex = 12;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "USB Batch Copy", MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                        finally
                        {
                            e.Node.Nodes.Add(node);
                        }
                    }
                }
            }
        }

        public static string GetUNCPath(string path)
        {
            if (path.StartsWith(@"\\")) return path;

            ManagementObject mo = new ManagementObject();
            mo.Path = new ManagementPath(string.Format("Win32_LogicalDisk='{0}'", path));
            return Convert.ToString(mo["ProviderName"]);
        }

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