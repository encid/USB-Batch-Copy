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
using Microsoft.Win32;
using System.Text;
using System.Runtime.InteropServices;

namespace WindowsFormsApplication1
{
    public partial class Main : Form
    {
        public const string SHELL = "shell32.dll";

        [DllImport(SHELL, CharSet = CharSet.Unicode)]
        public static extern uint SHParseDisplayName(string pszName, IntPtr zero, [Out] out IntPtr ppidl, uint sfgaoIn, [Out] out uint psfgaoOut);

        [DllImport(SHELL, CharSet = CharSet.Unicode)]
        public static extern uint SHGetNameFromIDList(IntPtr pidl, SIGDN sigdnName, [Out] out String ppszName);

        public enum SIGDN : uint
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
        }

        Dictionary<string, string> dictRemovableDrives = new Dictionary<string, string>();
        FolderBrowserDialog fbd                        = new FolderBrowserDialog();
        List<string> listDrivesToCopy                  = new List<string>();
        bool argExceptionError                         = false;
        string sourceDir;
        int currDriveCount;
        

        public Main()
        {
            InitializeComponent();
            PopulateTreeView();
        }

        /// <summary>
        /// Determines if an invoke is required to perform an action.
        /// </summary>
        /// <param name="a">Action to perform.</param>
        private void ExecuteSecure(Action a)
        // Usage example: ExecuteSecure(() => this.someLabel.Text = "foo");
        {
            BeginInvoke((Action)delegate
            {
                a();
            });
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
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1")
            {
                btnRefreshDrives.Font    = new Font("Arial", 8.25F, FontStyle.Italic, GraphicsUnit.Point, ((byte)(0)));
                btnRefreshDrives.Text    = "Auto-Detect On";
                btnRefreshDrives.Enabled = false;
            }

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
        /// Add checked items in CheckedListBox to list collection.
        /// </summary>
        /// <param name="clb">CheckedListBox to parse.</param>
        /// <param name="list">List collection to add items to.</param>
        private void PopulateListOfDrives(CheckedListBox clb, List<string> list)
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
        }

        private void btnSelectNone_Click(object sender, EventArgs e)
        // Sets check state for all listed drives to false.
        {
            SetCheckState(lstDrives, false);
        }

        private void btnRefreshDrives_Click(object sender, EventArgs e)
        // Detects removable drives and displays in CheckedListBox
        {
            RefreshDrives(lstDrives, dictRemovableDrives);
        }

        private void btnStartCopy_Click(object sender, EventArgs e)
        // Does some error checking on user input, and kicks off the BackgroundWorker
        {
            // Set UI properties and other vars
            PictureBox1.Visible = false;
            argExceptionError   = false;
            TreeNode aNode = dirsTreeView.SelectedNode;
            //sourceDir           = txtSourceDir.Text;

            /*
            // If source directory is not a valid directory, exit method
            if (!Directory.Exists(txtSourceDir.Text))
            {
                MessageBox.Show("Please select a valid source directory.", "USB Batch Copy", MessageBoxButtons.OK);
                return;
            }
            */

            // Check to make sure user has selected a source folder, if not, exit method
            if (aNode != null)
            {
                sourceDir = (string)aNode.Tag;
            }
            else
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
            PopulateListOfDrives(lstDrives, listDrivesToCopy);

            // Check to make sure user did not remove drive after refresh, but before copy
            foreach (var item in listDrivesToCopy)
            {
                if (!Directory.Exists(item))
                {
                    MessageBox.Show("Target destination drive does not exist.  Please try again.");
                    RefreshDrives(lstDrives, dictRemovableDrives);
                    return;
                }
            }

            // Disable UI controls and set some variables    
            if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = false; }
            //btnBrowse    .Enabled   = false;
            btnSelectAll .Enabled   = false;
            btnSelectNone.Enabled   = false;
            btnStartCopy .Enabled   = false;
            lstDrives    .Enabled   = false;
            dirsTreeView. Enabled   = false;
            lblStatus    .Text      = "Copying...";
            lblStatus    .ForeColor = Color.Black;

            // Begin the copy in BackgroundWorker
            backgroundWorker1.RunWorkerAsync();
        }

        /*private void btnBrowse_Click(object sender, EventArgs e)
        {            
            if (fbd.SelectedPath == "") { fbd.SelectedPath = @"V:\Released_Part_Information\"; }            
            
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtSourceDir.Text = fbd.SelectedPath;
            }
        }
        */

        private void tmrRefresh_Tick(object sender, EventArgs e)
        {
            lblSelectedDrives.Text = string.Format("Drives Selected: {0}", lstDrives.CheckedItems.Count);

            // AUTOMATIC REFRESH of drive list -- Edit config file to enable/disable
            if (ConfigurationManager.AppSettings["autoRefresh"] == "1")
            {
                IEnumerable<DriveInfo> drives =
                from d in DriveInfo.GetDrives()
                where d.DriveType == DriveType.Removable &&
                        d.IsReady == true
                select d;

                if (drives.Count() != currDriveCount)
                {
                    RefreshDrives(lstDrives, dictRemovableDrives);
                }

                currDriveCount = drives.Count();
            }

            //GC.Collect();
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            int totalItems = listDrivesToCopy.Count();

            // Start copy execution
            try
            {
                for (int i = 0; i < listDrivesToCopy.Count(); i++)
                {
                    ExecuteSecure(() => lblStatus.Text = string.Format("Copying drive {0} of {1}..", (i + 1), totalItems));  // Update status label securely
                    string item = listDrivesToCopy[i];
                    FileSystem.CopyDirectory(sourceDir, item, UIOption.AllDialogs, UICancelOption.ThrowException);
                }
            }
            catch (ArgumentException)  // Catch user removal of drive during copy and set bool flag for RunWorkerCompleted to process
            {
                argExceptionError = true;
            }
            catch (OperationCanceledException)  // Catch user cancelling copy and set bool flag for RunWorkerCompleted to process
            {
                e.Cancel = true;
            }
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            // Enable UI controls            
            if (ConfigurationManager.AppSettings["autoRefresh"] == "0") { btnRefreshDrives.Enabled = true; }
            btnStartCopy .Enabled  = true;
            //btnBrowse    .Enabled  = true;
            btnSelectAll .Enabled  = true;
            btnSelectNone.Enabled  = true;
            btnStartCopy .Enabled  = true;
            lstDrives    .Enabled  = true;
            dirsTreeView. Enabled  = true;


            // Checks for cancelled flag from BackgroundWorker1_DoWork and raises events / sets UI control properties appropriately
            if (e.Cancelled)
            {
                MessageBox.Show(this, "Copying operation has been cancelled.", "USB Batch Copy");
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text      = "Ready";
                return;
            }

            // Checks for argExceptionError flag from BackgroundWorker1_DoWork and raises events / sets UI control properties appropriately
            if (argExceptionError)
            {
                MessageBox.Show("An error has occured.  Please try again.");
                lblStatus.ForeColor = Color.Black;
                lblStatus.Text      = "Ready";
                RefreshDrives(lstDrives, dictRemovableDrives);
                return;
            }

            // Copy completed successfully with no flagged bools, so continue
            PictureBox1.Visible = true;

            // Check how many drives were copied and set status accordingly
            if (lstDrives.CheckedItems.Count == 1)
            {
                lblStatus.Text = "Success! Copied to 1 drive.";
            }
            else
                lblStatus.Text = string.Format("Success! Copied to {0} drives.", listDrivesToCopy.Count());

            lblStatus.ForeColor = Color.Green;
            SetCheckState(lstDrives, false);
        }

        private void txtSourceDir_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asynchronously so that it occurs after Click
            ExecuteSecure(() => txtSourceDir.SelectAll());            
        }

        private void PopulateTreeView()
        {
            //get a list of the drives
            string[] drives = Environment.GetLogicalDrives();
            

            foreach (string drive in drives)
            {
                DriveInfo di = new DriveInfo(drive);
                string drvName = "";
                int driveImage;
                string spc = "";

                switch (di.DriveType)    //set the drive's icon
                {
                    case DriveType.CDRom:
                        driveImage = 3;
                        drvName = "CD_ROM";
                        break;
                    case DriveType.Removable:
                        driveImage = 5;
                        drvName = "Removable Disk";
                        break;
                    case DriveType.Network:
                        driveImage = 6;
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

                switch (di.Name.Substring(0, 1))
                {
                    case "C":
                        drvName = "Local Disk";
                        break;
                    case "S":
                        drvName = "shared";
                        break;
                    case "V":
                        drvName = "vault";
                        break;
                    case "H":
                        drvName = "user";
                        break;
                    default:
                        if (drvName == "")
                        {
                            drvName = di.VolumeLabel;
                        }                        
                        break;
                }

                if (drvName != "")
                {
                    spc = " ";
                }
                else
                {
                    spc = "";
                }
                                              
                TreeNode node = new TreeNode(drvName + spc + "(" + di.Name + ")", driveImage, driveImage);
                
                //TreeNode node = new TreeNode(GetDriveLabel(di), driveImage, driveImage);
                //TreeNode node = new TreeNode(GetDriveLabels(di.Name) + " (" + di.Name + ")", driveImage, driveImage);

                node.Tag = drive;

                if (di.IsReady == true)
                    node.Nodes.Add("...");

                dirsTreeView.Nodes.Add(node);
            }

        }            

        private void treeView1_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void dirsTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count > 0)
            {
                if (e.Node.Nodes[0].Text == "..." && e.Node.Nodes[0].Tag == null)
                {
                    e.Node.Nodes.Clear();

                    //get the list of sub direcotires
                    IEnumerable<string> dirs = Directory.EnumerateDirectories((string)e.Node.Tag);

                    foreach (string dir in dirs)
                    {
                        DirectoryInfo di = new DirectoryInfo(dir);
                        TreeNode node = new TreeNode(di.Name, 0, 1);

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

        private string GetDriveLabel(DriveInfo drv)
        {
            string drvName;
            string drvLabel;
            string pvdr = "";

            //Start off with just the drive letter
            drvName = "(" + drv.Name.Substring(0, 2) + ")";

            //Use the volume label if it is not a network drive
            if (drv.DriveType != DriveType.Network)
            {
                drvLabel = drv.VolumeLabel;
                return drvLabel + " " + drvName;
            }

            //Try to get the network share name            
            try
            {
                var searcher = new ManagementObjectSearcher(
                    @"root\CIMV2",
                    "SELECT * FROM Win32_MappedLogicalDisk WHERE Name=\"" + drv.Name.Substring(0, 2) + "\"");

                foreach (ManagementObject queryObj in searcher.Get())
                {
                    pvdr = @queryObj["ProviderName"].ToString();
                }
            }
            catch (ManagementException)
            {
                pvdr = "";
            }

            //Try to get custom label from registry
            if (pvdr != "")
            {
                pvdr = pvdr.Replace(@"\", "#");
                drvLabel = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2\" + pvdr, "_LabelFromReg", "");
                if (string.IsNullOrEmpty(drvLabel))
                {
                    //If we didn't get the label from the registry, then extract the share name from the provider
                    drvLabel = pvdr.Substring(pvdr.LastIndexOf("#") + 1);
                }
                return drvLabel + " " + drvName;
            }
            else
            {
                //No point in trying the registry if we don't have a provider name
                return drvName;
            }
        }

        public string GetDriveLabels(string driveNameAsLetterColonBackslash)
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
        }

    }
}